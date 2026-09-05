using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Entities;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Credencial de acceso a la API de un contribuyente. La administra el operador
/// del SaaS (igual que <see cref="EmitterProfile"/>: <b>no</b> es
/// <c>ITenantOwned</c>, <b>no</b> lleva RLS). El token en claro se enseña una sola
/// vez, al crearla; después solo se guarda su hash SHA-256.
/// </summary>
public sealed class ApiKey : Entity<Guid>, IAuditableEntity, ISoftDeletable
{
    /// <summary>Largo máximo de la etiqueta libre que le pone el operador.</summary>
    public const int MaxLabelLength = 80;

    // Required by EF Core.
    private ApiKey()
    {
    }

    private ApiKey(
        Guid id,
        Guid tenantId,
        string keyHash,
        string prefix,
        string label,
        DgiiEnvironment environment,
        ApiKeyRole role,
        DateTimeOffset? expiresAt)
        : base(id)
    {
        TenantId = tenantId;
        KeyHash = keyHash;
        Prefix = prefix;
        Label = label;
        Environment = environment;
        Role = role;
        ExpiresAt = expiresAt;
    }

    /// <summary>El contribuyente al que pertenece esta credencial.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// El ambiente de la DGII en el que emite esta credencial. La key <b>es</b> el
    /// selector de ambiente: una petición autenticada con ella siempre va a este.
    /// </summary>
    public DgiiEnvironment Environment { get; private set; } = null!;

    /// <summary>
    /// Rol de la credencial (RF-14.5): qué puede hacer con el tenant. Igual que
    /// el ambiente, el rol lo fija la key — no hay concepto de usuario/login
    /// todavía, así que el permiso se asigna por credencial.
    /// </summary>
    public ApiKeyRole Role { get; private set; } = null!;

    /// <summary>SHA-256 del token en hex minúscula (64 caracteres). Único.</summary>
    public string KeyHash { get; private set; } = null!;

    /// <summary>Primeros caracteres del token, para reconocerla en un listado (<c>nfe_abc123…</c>).</summary>
    public string Prefix { get; private set; } = null!;

    /// <summary>Etiqueta libre del operador ("Producción", "ERP contable", …).</summary>
    public string Label { get; private set; } = null!;

    /// <summary>Vencimiento opcional; <c>null</c> = no vence.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Momento de revocación; <c>null</c> = vigente.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Último uso conocido (best-effort, lo toca el autenticador).</summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>Sirve para autenticar a <paramref name="asOf"/>: ni revocada, ni vencida.</summary>
    public bool IsUsableAt(DateTimeOffset asOf) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > asOf);

    /// <summary>
    /// Crea una credencial. El <paramref name="keyHash"/> y el
    /// <paramref name="prefix"/> los produce el caso de uso a partir del token
    /// generado (ver <c>ApiKeyToken</c>).
    /// </summary>
    public static ErrorOr<ApiKey> Create(
        Guid tenantId,
        string keyHash,
        string prefix,
        string? label,
        DgiiEnvironment environment,
        ApiKeyRole role,
        DateTimeOffset? expiresAt)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(role);

        if (tenantId == Guid.Empty)
            return ApiKeyErrors.TenantRequired;

        if (string.IsNullOrWhiteSpace(keyHash) || string.IsNullOrWhiteSpace(prefix))
            return ApiKeyErrors.MalformedToken;

        var cleanLabel = string.IsNullOrWhiteSpace(label) ? "Sin etiqueta" : label.Trim();
        if (cleanLabel.Length > MaxLabelLength)
            return ApiKeyErrors.LabelTooLong;

        return new ApiKey(Guid.CreateVersion7(), tenantId, keyHash, prefix, cleanLabel, environment, role, expiresAt);
    }

    /// <summary>Revoca la credencial. Idempotencia estricta: revocar dos veces es un error.</summary>
    public ErrorOr<Success> Revoke(DateTimeOffset at)
    {
        if (RevokedAt is not null)
            return ApiKeyErrors.AlreadyRevoked;

        RevokedAt = at;
        return Result.Success;
    }

    /// <summary>Registra el último uso. Lo llama el autenticador de forma best-effort.</summary>
    public void MarkUsed(DateTimeOffset at) => LastUsedAt = at;
}
