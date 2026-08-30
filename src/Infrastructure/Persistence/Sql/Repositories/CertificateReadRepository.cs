using Dapper;
using NovaFE.Application.Certificates;
using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;

namespace NovaFE.Infrastructure.Persistence.Sql.Repositories;

/// <summary>
/// Lectura de certificados con Dapper. Filtra por <c>tenant_id</c> explícitamente
/// (defensa en profundidad: en local/tests la app corre como superusuario y RLS
/// no aplica). Columnas con alias entre comillas para casar con el record.
/// </summary>
internal sealed class CertificateReadRepository(
    IDbSession session,
    ICurrentTenant currentTenant) : ICertificateReadRepository
{
    private const string Columns =
        """
        id                AS "Id",
        environment       AS "Environment",
        holder_identifier AS "HolderIdentifier",
        subject           AS "Subject",
        issuer            AS "Issuer",
        thumbprint        AS "Thumbprint",
        valid_from        AS "ValidFrom",
        valid_to          AS "ValidTo",
        status            AS "Status",
        revoked_at        AS "RevokedAt",
        created_at        AS "CreatedAt"
        """;

    public async Task<CertificateView?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sql =
            $"""
            SELECT {Columns}
            FROM certificates
            WHERE id = @id AND tenant_id = @tenantId AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<CertificateView>(
            new CommandDefinition(
                sql,
                new { id, tenantId = currentTenant.TenantId },
                session.Transaction,
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CertificateView>> ListAsync(CancellationToken ct = default)
    {
        var sql =
            $"""
            SELECT {Columns}
            FROM certificates
            WHERE tenant_id = @tenantId AND is_deleted = false
            ORDER BY environment, created_at DESC
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<CertificateView>(
            new CommandDefinition(
                sql,
                new { tenantId = currentTenant.TenantId },
                session.Transaction,
                cancellationToken: ct));

        return rows.AsList();
    }
}
