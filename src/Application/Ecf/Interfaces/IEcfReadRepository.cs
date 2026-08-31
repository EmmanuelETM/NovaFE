using NovaFE.Application.Ecf.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Ecf.Interfaces;

/// <summary>Read side (Dapper) del comprobante emitido. Siempre filtra por el tenant.</summary>
public interface IEcfReadRepository
{
    Task<EcfDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);

    /// <summary>El XML firmado (<c>&lt;ECF&gt;</c> o, si <paramref name="rfce"/>, el <c>&lt;RFCE&gt;</c>).</summary>
    Task<string?> GetXmlAsync(Guid id, Guid tenantId, bool rfce, CancellationToken ct = default);

    /// <summary>El id del comprobante con ese <c>NumeroFacturaInterna</c>, si existe (dedup de negocio).</summary>
    Task<Guid?> FindByInternalNumberAsync(Guid tenantId, string internalNumber, CancellationToken ct = default);

    Task<PagedResult<EcfSummaryDto>> ListAsync(Guid tenantId, EcfListFilter filter, CancellationToken ct = default);
}
