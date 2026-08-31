using Microsoft.Extensions.Logging;
using NovaFE.Application.Dgii.Interfaces;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;
using NovaFE.Domain.Ecf;

namespace NovaFE.Application.Ecf.Submission;

/// <summary>
/// Procesa una fila de la cola de envío: la lleva un paso adelante en el ciclo de
/// vida del comprobante contra la DGII. Es el <b>único</b> code path del envío —
/// lo usan el worker de fondo (<see cref="ProcessAsync"/>, con el ladder de
/// polling) y el fast-path inline del <c>POST /ecf</c>
/// (<see cref="PollOnceAsync"/>, sin ladder). Servicio plano (no <c>IUseCase</c>).
/// </summary>
public sealed class EcfSubmissionProcessor(
    IEcfRepository ecfRepo,
    IEcfSubmissionQueue queue,
    IDgiiTokenProvider tokenProvider,
    IDgiiSubmissionClient client,
    EcfSubmissionSettings settings,
    TimeProvider timeProvider,
    ILogger<EcfSubmissionProcessor> logger)
{
    public async Task ProcessAsync(EcfSubmissionWorkItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var ecf = await ecfRepo.GetByIdAsync(item.EcfId, ct);
        if (ecf is null || ecf.Status.IsTerminal)
        {
            await queue.CompleteAsync(item.Id, ct);
            return;
        }

        var token = await tokenProvider.GetTokenAsync(item.Environment, ct);
        if (token.IsError)
        {
            await OnTransportFailureAsync(item, ecf, $"token de la DGII: {token.FirstError.Description}", ct);
            return;
        }

        if (item.Kind == EcfSubmissionKind.Submit)
            await SubmitAsync(item, ecf, token.Value.Value, ct);
        else
            await PollAsync(item, ecf, token.Value.Value, ct);
    }

    /// <summary>
    /// Una sola consulta de estado para el fast-path inline: aplica un resultado
    /// definitivo si lo hay y devuelve <c>true</c>; no toca el outbox ni avanza el
    /// ladder (de eso se encarga el worker).
    /// </summary>
    public async Task<bool> PollOnceAsync(Guid ecfId, CancellationToken ct = default)
    {
        var ecf = await ecfRepo.GetByIdAsync(ecfId, ct);
        if (ecf is null || ecf.Status.IsTerminal || ecf.Status == EcfStatus.Failed)
            return true;

        if (ecf.Status != EcfStatus.Submitted || ecf.TrackId is null)
            return false;

        var token = await tokenProvider.GetTokenAsync(ecf.Environment, ct);
        if (token.IsError)
            return false;

        var result = await client.GetResultAsync(ecf.Environment, token.Value.Value, ecf.TrackId, ct);
        if (result.IsError || result.Value.Codigo is not (1 or 2 or 4))
            return false;

        await ApplyTerminalAsync(ecf, result.Value.Codigo, result.Value.Mensajes, result.Value.SecuenciaUtilizada, ct);
        return true;
    }

    // --- submit ---------------------------------------------------------

    private async Task SubmitAsync(EcfSubmissionWorkItem item, IssuedEcf ecf, string bearer, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();

        if (ecf.SubmitsRfce)
        {
            var rfce = await client.SubmitRfceAsync(item.Environment, bearer, ecf.RfceXml!, ecf.Encf.Value, ct);
            if (rfce.IsError)
            {
                await OnSubmitErrorAsync(item, ecf, rfce.FirstError, ct);
                return;
            }

            // El RFCE resuelve síncrono: no hay TrackId ni polling.
            if (rfce.Value.Codigo is 1 or 2 or 4)
            {
                await ApplyTerminalAsync(ecf, rfce.Value.Codigo, rfce.Value.Mensajes, rfce.Value.SecuenciaUtilizada, ct);
                await queue.CompleteAsync(item.Id, ct);
            }
            else
            {
                await GiveUpSubmitAsync(item, ecf,
                    $"la DGII devolvió un estado no definitivo para el RFCE: {rfce.Value.Estado} ({rfce.Value.Codigo})", ct);
            }

            return;
        }

        var ack = await client.SubmitEcfAsync(item.Environment, bearer, ecf.EcfXml, ecf.Encf.Value, ct);
        if (ack.IsError)
        {
            await OnSubmitErrorAsync(item, ecf, ack.FirstError, ct);
            return;
        }

        var marked = ecf.MarkSubmitted(ack.Value.TrackId, now);
        if (marked.IsError)
        {
            logger.LogError("e-NCF {Encf}: transición a 'submitted' inválida ({Error})",
                ecf.Encf.Value, marked.FirstError.Description);
            await queue.CompleteAsync(item.Id, ct);
            return;
        }

        await ecfRepo.UpdateAsync(ecf, ct);
        await queue.RescheduleAsync(
            item.Id, EcfSubmissionKind.Poll, now + settings.FirstPollDelay, attempts: 0, trackId: ack.Value.TrackId, ct: ct);
    }

    private Task OnSubmitErrorAsync(EcfSubmissionWorkItem item, IssuedEcf ecf, ErrorOr.Error error, CancellationToken ct)
        => IsTransport(error)
            ? OnTransportFailureAsync(item, ecf, error.Description, ct)
            : GiveUpSubmitAsync(item, ecf, error.Description, ct);

    // --- poll (worker, con ladder) ------------------------------------

    private async Task PollAsync(EcfSubmissionWorkItem item, IssuedEcf ecf, string bearer, CancellationToken ct)
    {
        var result = await client.GetResultAsync(item.Environment, bearer, item.TrackId!, ct);
        if (result.IsError)
        {
            await ReschedulePollAsync(item, ecf, $"consulta de estado: {result.FirstError.Description}", ct);
            return;
        }

        if (result.Value.Codigo is 1 or 2 or 4)
        {
            await ApplyTerminalAsync(ecf, result.Value.Codigo, result.Value.Mensajes, result.Value.SecuenciaUtilizada, ct);
            await queue.CompleteAsync(item.Id, ct);
            return;
        }

        // 3 en proceso / 0 no encontrado (puede seguir en proceso).
        await ReschedulePollAsync(item, ecf, $"la DGII aún no resolvió ({result.Value.Estado})", ct);
    }

    private async Task ReschedulePollAsync(EcfSubmissionWorkItem item, IssuedEcf ecf, string reason, CancellationToken ct)
    {
        if (item.Attempts >= settings.PollLadder.Count)
        {
            if (!ecf.MarkForReview(reason).IsError)
                await ecfRepo.UpdateAsync(ecf, ct);
            await queue.CompleteAsync(item.Id, ct);
            logger.LogWarning(
                "e-NCF {Encf} (TrackId {TrackId}) pasa a revisión manual: {Reason}", ecf.Encf.Value, item.TrackId, reason);
            return;
        }

        var next = timeProvider.GetUtcNow() + settings.PollLadder[item.Attempts];
        await queue.RescheduleAsync(item.Id, EcfSubmissionKind.Poll, next, item.Attempts + 1, item.TrackId, reason, ct);
    }

    // --- resultados y fallos -----------------------------------------

    private async Task ApplyTerminalAsync(
        IssuedEcf ecf, int codigo, IReadOnlyList<DgiiMessage> messages, bool? sequenceUsable, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();

        var transition = codigo switch
        {
            1 => ecf.MarkAccepted(now, conditional: false, messages, sequenceUsable),
            4 => ecf.MarkAccepted(now, conditional: true, messages, sequenceUsable),
            _ => ecf.MarkRejected(now, messages, sequenceUsable),
        };

        if (transition.IsError)
        {
            logger.LogError("e-NCF {Encf}: no se pudo aplicar el resultado {Codigo} de la DGII ({Error})",
                ecf.Encf.Value, codigo, transition.FirstError.Description);
            return;
        }

        await ecfRepo.UpdateAsync(ecf, ct);

        if (codigo == 2)
            logger.LogWarning("e-NCF {Encf} rechazado por la DGII (secuencia reutilizable: {Usable})",
                ecf.Encf.Value, sequenceUsable);
    }

    private async Task OnTransportFailureAsync(EcfSubmissionWorkItem item, IssuedEcf ecf, string reason, CancellationToken ct)
    {
        if (item.Kind == EcfSubmissionKind.Poll)
        {
            await ReschedulePollAsync(item, ecf, reason, ct);
            return;
        }

        if (item.Attempts >= settings.SubmitBackoff.Count)
        {
            await GiveUpSubmitAsync(item, ecf, reason, ct);
            return;
        }

        var next = timeProvider.GetUtcNow() + settings.SubmitBackoff[item.Attempts];
        await queue.RescheduleAsync(item.Id, EcfSubmissionKind.Submit, next, item.Attempts + 1, item.TrackId, reason, ct);
    }

    private async Task GiveUpSubmitAsync(EcfSubmissionWorkItem item, IssuedEcf ecf, string reason, CancellationToken ct)
    {
        if (!ecf.MarkFailed(reason).IsError)
            await ecfRepo.UpdateAsync(ecf, ct);
        await queue.MarkDeadAsync(item.Id, reason, ct);
        logger.LogError("e-NCF {Encf} no se pudo enviar a la DGII: {Reason}", ecf.Encf.Value, reason);
    }

    private static bool IsTransport(ErrorOr.Error error) => error.Code.StartsWith("Http.", StringComparison.Ordinal);
}
