using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Common;

namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// <see cref="ICurrentTenant"/> stub for schema setup, where there is no request
/// and therefore no tenant. The API under test uses the real implementation.
/// </summary>
internal sealed class NullCurrentTenant : ICurrentTenant
{
    public static readonly NullCurrentTenant Instance = new();

    public Guid? TenantId => null;

    public DgiiEnvironment? Environment => null;

    public bool HasValue => false;

    public Guid Require() =>
        throw new InvalidOperationException("No tenant is available during schema setup.");
}
