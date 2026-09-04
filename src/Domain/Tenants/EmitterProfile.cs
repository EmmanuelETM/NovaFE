using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Entities;

namespace NovaFE.Domain.Tenants;

/// <summary>
/// Los datos fiscales del emisor que exige el e-CF y que no viven en el
/// <see cref="Tenant"/>: dirección, ubicación, teléfonos, actividad económica y el
/// ambiente de la DGII en el que opera. Es 1:1 con el contribuyente y lo administra
/// el operador del SaaS (igual que el <see cref="Tenant"/>: no está sujeto a RLS por
/// tenant).
/// <para>
/// Alimenta el bloque <c>&lt;Emisor&gt;</c> del e-CF — el cliente nunca lo manda en el
/// payload de emisión.
/// </para>
/// </summary>
public sealed class EmitterProfile : Entity<Guid>, IAuditableEntity, ISoftDeletable
{
    /// <summary>Máximo de teléfonos del emisor (<c>&lt;TablaTelefonoEmisor&gt;</c>).</summary>
    public const int MaxPhones = 3;

    // Required by EF Core.
    private EmitterProfile()
    {
    }

    private EmitterProfile(
        Guid id,
        Guid tenantId,
        string address,
        string? municipality,
        string? province,
        string[] phones,
        string? email,
        string? economicActivity,
        DgiiEnvironment defaultEnvironment)
        : base(id)
    {
        TenantId = tenantId;
        Address = address;
        Municipality = municipality;
        Province = province;
        Phones = phones;
        Email = email;
        EconomicActivity = economicActivity;
        DefaultEnvironment = defaultEnvironment;
    }

    /// <summary>El contribuyente al que pertenece este perfil.</summary>
    public Guid TenantId { get; private set; }

    /// <summary><c>&lt;DireccionEmisor&gt;</c> — obligatoria en casi todos los tipos de e-CF.</summary>
    public string Address { get; private set; } = null!;

    /// <summary><c>&lt;Municipio&gt;</c> — código Tabla III.</summary>
    public string? Municipality { get; private set; }

    /// <summary><c>&lt;Provincia&gt;</c> — código Tabla III.</summary>
    public string? Province { get; private set; }

    /// <summary><c>&lt;TablaTelefonoEmisor&gt;</c> — hasta <see cref="MaxPhones"/>.</summary>
    public string[] Phones { get; private set; } = [];

    /// <summary><c>&lt;CorreoEmisor&gt;</c>.</summary>
    public string? Email { get; private set; }

    /// <summary><c>&lt;CodigoDGIIEmisor&gt;</c> / actividad económica declarada.</summary>
    public string? EconomicActivity { get; private set; }

    /// <summary>
    /// El ambiente de la DGII en el que emite este contribuyente por defecto
    /// (<c>Test</c> durante el onboarding, <c>Production</c> tras certificar). El
    /// payload de emisión puede sobrescribirlo por petición.
    /// </summary>
    public DgiiEnvironment DefaultEnvironment { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>Crea el perfil fiscal de un contribuyente. La unicidad por tenant es un concern del repositorio.</summary>
    public static ErrorOr<EmitterProfile> Create(
        Guid tenantId,
        string address,
        string? municipality,
        string? province,
        IReadOnlyList<string>? phones,
        string? email,
        string? economicActivity,
        DgiiEnvironment defaultEnvironment)
    {
        ArgumentNullException.ThrowIfNull(defaultEnvironment);

        var normalizedPhones = NormalizePhones(phones);

        var validation = Validate(address, normalizedPhones);
        if (validation.IsError)
            return validation.Errors;

        return new EmitterProfile(
            Guid.CreateVersion7(),
            tenantId,
            address.Trim(),
            Clean(municipality),
            Clean(province),
            normalizedPhones,
            Clean(email),
            Clean(economicActivity),
            defaultEnvironment);
    }

    /// <summary>Reemplaza todos los datos del perfil (semántica de <c>PUT</c>).</summary>
    public ErrorOr<Success> Update(
        string address,
        string? municipality,
        string? province,
        IReadOnlyList<string>? phones,
        string? email,
        string? economicActivity,
        DgiiEnvironment defaultEnvironment)
    {
        ArgumentNullException.ThrowIfNull(defaultEnvironment);

        var normalizedPhones = NormalizePhones(phones);

        var validation = Validate(address, normalizedPhones);
        if (validation.IsError)
            return validation.Errors;

        Address = address.Trim();
        Municipality = Clean(municipality);
        Province = Clean(province);
        Phones = normalizedPhones;
        Email = Clean(email);
        EconomicActivity = Clean(economicActivity);
        DefaultEnvironment = defaultEnvironment;

        return Result.Success;
    }

    private static ErrorOr<Success> Validate(string address, string[] phones)
    {
        if (string.IsNullOrWhiteSpace(address))
            return EmitterProfileErrors.AddressRequired;

        if (phones.Length > MaxPhones)
            return EmitterProfileErrors.TooManyPhones;

        return Result.Success;
    }

    private static string[] NormalizePhones(IReadOnlyList<string>? phones)
        => phones is null
            ? []
            : [.. phones.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim())];

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
