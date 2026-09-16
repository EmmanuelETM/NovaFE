using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.IssueEcf;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Ecf.ValidateEcf;

/// <summary>
/// Valida un payload de e-CF sin emitirlo: corre la misma validación de forma
/// (<see cref="IssueEcfCommand"/>, reusa <c>IValidator&lt;IssueEcfCommand&gt;</c>) y la
/// misma matriz de validación estructural/fiscal por tipo que <c>IssueEcfUseCase</c>
/// (Módulo 2 + 6, <see cref="EcfDocumentMapper.ToDocument"/> → <c>EcfDocument.Create</c>),
/// pero se detiene ahí — sin asignar una secuencia real, sin firmar, sin persistir.
/// Un payload que no pasa devuelve el mismo error que devolvería <c>POST /ecf</c>
/// (validación de forma o de negocio); nunca hay un cuerpo "válido: false".
/// </summary>
public sealed class ValidateEcfUseCase(
    ILoggerFactory loggerFactory,
    IValidator<IssueEcfCommand> validator,
    ICurrentTenant currentTenant,
    ITenantRepository tenants,
    IEmitterProfileRepository emitterProfiles,
    TimeProvider timeProvider)
    : QueryUseCase<IssueEcfCommand, ValidateEcfResultDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<ValidateEcfResultDto>> ExecuteCore(IssueEcfCommand request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        var type = EcfType.FromCodeOrDefault(request.Type)!; // el validador ya lo garantizó

        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            return Errors.Auth.TenantNotResolved;

        var profile = await emitterProfiles.GetByTenantAsync(tenantId, ct);
        if (profile is null)
            return EmitterProfileErrors.NotConfigured;

        var issueDate = request.IssueDate ?? timeProvider.GetDominicanToday();

        // Placeholder: no se asigna ninguna secuencia real. El vencimiento se
        // estima con la misma regla de NcfSequence (31-dic del año siguiente)
        // pero sin una fila de secuencia detrás — es una vista previa, no un
        // compromiso.
        var encf = Encf.Build('E', type.Id, 0);
        var sequenceExpiresOn = type.HasSequenceExpiry ? new DateOnly(issueDate.Year + 1, 12, 31) : (DateOnly?)null;

        var issuer = EcfIssuerFactory.From(
            tenant, profile, request.SellerCode, request.InternalNumber, request.AdditionalInfo?.Issuer);

        var document = EcfDocumentMapper.ToDocument(request, type, encf, sequenceExpiresOn, issuer, issueDate);
        if (document.IsError)
            return document.Errors;

        var totals = document.Value.Totals;

        return new ValidateEcfResultDto(
            Type: type.Id,
            TypeName: type.DisplayName,
            SampleEncf: encf.Value,
            IssueDate: issueDate,
            SequenceExpiresOnEstimate: sequenceExpiresOn,
            MontoGravadoTotal: totals.MontoGravadoTotal,
            MontoExento: totals.MontoExento,
            TotalItbis: totals.TotalItbis,
            MontoImpuestoAdicional: totals.MontoImpuestoAdicional,
            MontoTotal: totals.MontoTotal,
            ExpectConditionalAcceptance: document.Value.Calculation.Tolerance.ExpectConditionalAcceptance);
    }
}
