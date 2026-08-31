using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Ecf.Submission;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Ecf.IssueEcf;

/// <summary>
/// Emite un e-CF: resuelve el emisor y el ambiente, aplica idempotencia y dedup,
/// asigna la secuencia (Módulo 7), arma el documento (Módulo 2+6), lo firma
/// (Módulo 3) y lo persiste como <c>signed</c>. No lo envía a la DGII (Módulo 4).
/// </summary>
public sealed class IssueEcfUseCase(
    ILoggerFactory loggerFactory,
    IValidator<IssueEcfCommand> validator,
    ICurrentTenant currentTenant,
    ITenantRepository tenants,
    IEmitterProfileRepository emitterProfiles,
    IIdempotencyStore idempotency,
    INcfSequenceAllocator allocator,
    IEcfSigner signer,
    IEcfRepository ecf,
    IEcfReadRepository ecfReads,
    IEcfSubmissionQueue submissionQueue,
    IEcfSubmissionFastPath submissionFastPath,
    EcfSubmissionSettings submissionSettings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : CommandUseCase<IssueEcfCommand, IssueEcfResult>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<IssueEcfResult>> ExecuteCore(IssueEcfCommand request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var type = EcfType.FromCodeOrDefault(request.Type)!; // el validador ya lo garantizó

        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            return Errors.Auth.TenantNotResolved;

        var profile = await emitterProfiles.GetByTenantAsync(tenantId, ct);
        if (profile is null)
            return NovaFE.Domain.Tenants.EmitterProfileErrors.NotConfigured;

        var environment = ResolveEnvironment(request.Environment, profile.DefaultEnvironment);
        if (environment is null)
            return EcfErrors.EnvironmentNotResolvable;

        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        var requestHash = HashRequest(request);

        if (key is not null)
        {
            var outcome = await idempotency.BeginAsync(tenantId, key, requestHash, ct);
            switch (outcome.Decision)
            {
                case IdempotencyDecision.Replay when outcome.EcfId is { } existing:
                    return await ReplayAsync(existing, tenantId, ct);
                case IdempotencyDecision.InProgress:
                    return EcfErrors.RequestInProgress;
                case IdempotencyDecision.Conflict:
                    return EcfErrors.IdempotencyKeyConflict;
            }
        }

        // Dedup de negocio: un comprobante por (tenant, NumeroFacturaInterna).
        var internalNumber = string.IsNullOrWhiteSpace(request.InternalNumber) ? null : request.InternalNumber.Trim();
        if (internalNumber is not null
            && await ecfReads.FindByInternalNumberAsync(tenantId, internalNumber, ct) is { } duplicateId)
        {
            if (key is not null)
                await idempotency.CompleteAsync(tenantId, key, duplicateId, ct);
            return await ReplayAsync(duplicateId, tenantId, ct);
        }

        var issueDate = request.IssueDate ?? timeProvider.GetDominicanToday();

        var allocation = await allocator.AllocateAsync(environment, type, ct);
        if (allocation.IsError)
            return allocation.Errors;

        var encf = allocation.Value.Encf;

        var issuer = EcfIssuerFactory.From(
            tenant, profile, request.SellerCode, internalNumber, request.AdditionalInfo?.Issuer);

        var document = EcfDocumentMapper.ToDocument(
            request, type, encf, allocation.Value.SequenceExpiresOn, issuer, issueDate);
        if (document.IsError)
        {
            Logger.LogError(
                "e-NCF {Encf} quemado: el payload no armó un documento válido tras asignar la secuencia — {Error}",
                encf.Value, document.FirstError.Description);
            return document.Errors;
        }

        var signed = await signer.SignAsync(document.Value, environment, ct);
        if (signed.IsError)
        {
            Logger.LogError(
                "e-NCF {Encf} quemado: falló la firma — {Error}", encf.Value, signed.FirstError.Description);
            return RemapSigningError(signed.Errors);
        }

        var expectConditional = EcfDtoAssembler.DeclaredHeaderTotalsOutOfTolerance(
            document.Value.Totals, request.DeclaredTotals);

        var issued = IssuedEcf.FromSigned(document.Value, signed.Value, environment, expectConditional);

        // Outbox transaccional: el comprobante y su fila de envío se guardan juntos.
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await ecf.AddAsync(issued, token);
            await submissionQueue.EnqueueSubmitAsync(issued.Id, tenantId, environment, token);
        }, ct);

        if (key is not null)
            await idempotency.CompleteAsync(tenantId, key, issued.Id, ct);

        // Fast-path síncrono: intenta resolver contra la DGII dentro del presupuesto.
        // Si no alcanza, el worker de fondo termina; nunca falla el POST por esto.
        if (submissionSettings.SyncWaitBudget > TimeSpan.Zero)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(submissionSettings.SyncWaitBudget);
            await submissionFastPath.TryResolveAsync(issued.Id, budget.Token);
        }

        var dto = await ecfReads.GetByIdAsync(issued.Id, tenantId, ct) ?? EcfDtoAssembler.From(issued);
        return new IssueEcfResult(dto, WasCreated: true);
    }

    private async Task<ErrorOr<IssueEcfResult>> ReplayAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var dto = await ecfReads.GetByIdAsync(id, tenantId, ct);
        return dto is null
            ? EcfErrors.NotFound(id)
            : new IssueEcfResult(dto, WasCreated: false);
    }

    private static DgiiEnvironment? ResolveEnvironment(string? requested, DgiiEnvironment fallback)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return fallback;

        return DgiiEnvironment.GetAll()
            .FirstOrDefault(e => string.Equals(e.Name, requested.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string HashRequest(IssueEcfCommand request)
    {
        // El hash no debe depender de la clave de idempotencia en sí.
        var canonical = request with { IdempotencyKey = null };
        var json = JsonSerializer.Serialize(canonical);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static List<Error> RemapSigningError(List<Error> errors)
    {
        // "sin certificado" / "certificado inutilizable" son un problema de configuración
        // del emisor, no un fallo interno: 400, no 500.
        return [.. errors.Select(error => error.Code is "Certificate.NoActiveCertificate" or "Certificate.NotUsable"
            ? Error.Validation(error.Code, error.Description)
            : error)];
    }
}
