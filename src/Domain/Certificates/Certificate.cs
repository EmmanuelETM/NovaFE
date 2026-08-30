using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Entities;

namespace NovaFE.Domain.Certificates;

/// <summary>
/// El certificado digital de un contribuyente para un ambiente de la DGII. Guarda
/// solo metadatos; el PKCS#12 vive cifrado en el vault
/// (<see cref="VaultReference"/> es la referencia opaca).
/// <para>
/// Regla: un contribuyente tiene a lo sumo un certificado activo por ambiente.
/// </para>
/// </summary>
public sealed class Certificate : Entity<Guid>, ITenantOwned, IAuditableEntity, ISoftDeletable
{
    // Required by EF Core.
    private Certificate()
    {
    }

    private Certificate(Guid id, DgiiEnvironment environment, CertificateDetails details, string vaultReference)
        : base(id)
    {
        Environment = environment;
        HolderIdentifier = details.HolderIdentifier;
        Subject = details.Subject;
        Issuer = details.Issuer;
        Thumbprint = details.Thumbprint;
        ValidFrom = details.ValidFrom;
        ValidTo = details.ValidTo;
        VaultReference = vaultReference;
        Status = CertificateStatus.Active;
    }

    public Guid TenantId { get; private set; }

    public DgiiEnvironment Environment { get; private set; } = null!;

    /// <summary>RNC o cédula del titular, leído del certificado (Subject SERIALNUMBER).</summary>
    public string HolderIdentifier { get; private set; } = null!;

    public string Subject { get; private set; } = null!;

    public string Issuer { get; private set; } = null!;

    /// <summary>Huella SHA-1 en hex. Identifica el certificado sin exponer su contenido.</summary>
    public string Thumbprint { get; private set; } = null!;

    public DateTimeOffset ValidFrom { get; private set; }

    public DateTimeOffset ValidTo { get; private set; }

    /// <summary>Referencia opaca al PKCS#12 en el vault. La aplicación no la interpreta.</summary>
    public string VaultReference { get; private set; } = null!;

    public CertificateStatus Status { get; private set; } = null!;

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Valida el certificado contra las reglas de la DGII y lo emite. El RNC del
    /// contribuyente (<paramref name="tenantRnc"/>) debe coincidir con el titular
    /// del certificado. <paramref name="now"/> viene del <c>TimeProvider</c>.
    /// </summary>
    public static ErrorOr<Certificate> Issue(
        string tenantRnc,
        DgiiEnvironment environment,
        CertificateDetails details,
        string vaultReference,
        DateTimeOffset now)
    {
        if (!details.HasPrivateKey)
            return CertificateErrors.NoPrivateKey;

        if (now < details.ValidFrom)
            return CertificateErrors.NotYetValid(details.ValidFrom);

        if (now >= details.ValidTo)
            return CertificateErrors.Expired(details.ValidTo);

        if (!HolderMatchesRnc(details.HolderIdentifier, tenantRnc))
            return CertificateErrors.RncMismatch(details.HolderIdentifier, tenantRnc);

        return new Certificate(Guid.CreateVersion7(), environment, details, vaultReference);
    }

    public ErrorOr<Success> Revoke(DateTimeOffset now)
    {
        if (Status == CertificateStatus.Revoked)
            return CertificateErrors.AlreadyRevoked;

        Status = CertificateStatus.Revoked;
        RevokedAt = now;

        return Result.Success;
    }

    /// <summary>Utilizable para firmar: activo y dentro de su ventana de validez.</summary>
    public bool IsUsable(DateTimeOffset now)
        => Status == CertificateStatus.Active && now >= ValidFrom && now < ValidTo;

    private static bool HolderMatchesRnc(string holderIdentifier, string tenantRnc)
    {
        // El SERIALNUMBER del certificado puede traer prefijos ("RNC", "CE") o
        // guiones. Se comparan solo los dígitos.
        var holderDigits = Digits(holderIdentifier);
        var rncDigits = Digits(tenantRnc);

        return holderDigits.Length > 0 && holderDigits == rncDigits;
    }

    private static string Digits(string value)
        => new([.. value.Where(char.IsAsciiDigit)]);
}
