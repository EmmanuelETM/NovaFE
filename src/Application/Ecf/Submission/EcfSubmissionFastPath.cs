using Microsoft.Extensions.Logging;
using NovaFE.Application.Ecf.Interfaces;

namespace NovaFE.Application.Ecf.Submission;

/// <summary>
/// El "síncrono" del <c>POST /ecf</c>: intenta llevar el comprobante recién
/// encolado hasta un estado definitivo dentro del presupuesto de tiempo del
/// request. Todo fallo se traga — la fila queda en el outbox para el worker.
/// </summary>
public interface IEcfSubmissionFastPath
{
    Task TryResolveAsync(Guid ecfId, CancellationToken budget);
}

internal sealed class EcfSubmissionFastPath(
    IEcfSubmissionQueue queue,
    EcfSubmissionProcessor processor,
    EcfSubmissionSettings settings,
    ILogger<EcfSubmissionFastPath> logger) : IEcfSubmissionFastPath
{
    public async Task TryResolveAsync(Guid ecfId, CancellationToken budget)
    {
        try
        {
            var item = await queue.ClaimByEcfAsync(ecfId, budget);
            if (item is null)
                return; // el worker ya la tomó

            // Envío (o RFCE síncrono): deja el comprobante en 'submitted' o resuelto.
            await processor.ProcessAsync(item, budget);

            // Consultas rápidas mientras quede presupuesto; el ladder del worker no
            // se toca (PollOnceAsync no avanza intentos).
            for (var i = 0; i < settings.MaxInlinePolls && !budget.IsCancellationRequested; i++)
            {
                await Task.Delay(settings.InlinePollDelay, budget);

                if (await processor.PollOnceAsync(ecfId, budget))
                    return;
            }
        }
        catch (OperationCanceledException)
        {
            // Se agotó el presupuesto: normal. El worker termina el trabajo.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "El envío inline del e-CF {EcfId} falló; queda para el worker.", ecfId);
        }
    }
}
