using ErrorOr;
using NovaFE.Domain.Common;
using NovaFE.Domain.Common.Entities;

namespace NovaFE.Domain.Sequences;

/// <summary>
/// Un rango de e-NCF autorizado por la DGII a un contribuyente para un tipo de
/// comprobante, una serie y un ambiente. Es la unidad de inventario de secuencias:
/// entrega números de forma atómica y ordenada mientras le quede stock y no haya
/// vencido.
/// <para>
/// El puntero <see cref="Next"/> solo avanza. Los huecos que dejan las secuencias
/// no usadas o quemadas por un rechazo son aceptables: la DGII no exige uso
/// contiguo. Reclamar secuencias liberadas es un slice posterior.
/// </para>
/// </summary>
public sealed class NcfSequence : Entity<Guid>, ITenantOwned, IAuditableEntity, ISoftDeletable
{
    /// <summary>Umbral de stock bajo: 20 % del rango autorizado (RF-07.3).</summary>
    private const double LowStockFraction = 0.20;

    /// <summary>Tope de secuencias por tipo en CerteCF (RF-07 tabla por ambiente).</summary>
    private const long CertMaxSequential = 10_000_000;

    // Requerido por EF Core.
    private NcfSequence()
    {
    }

    private NcfSequence(
        Guid id,
        DgiiEnvironment environment,
        EcfType type,
        char series,
        long rangeFrom,
        long rangeTo,
        DateOnly? expiresOn)
        : base(id)
    {
        Environment = environment;
        Type = type;
        Series = series;
        RangeFrom = rangeFrom;
        RangeTo = rangeTo;
        Next = rangeFrom;
        ExpiresOn = expiresOn;
        Active = true;
    }

    public Guid TenantId { get; private set; }

    public DgiiEnvironment Environment { get; private set; } = null!;

    public EcfType Type { get; private set; } = null!;

    public char Series { get; private set; }

    /// <summary>Primer secuencial del rango (inclusive).</summary>
    public long RangeFrom { get; private set; }

    /// <summary>Último secuencial del rango (inclusive).</summary>
    public long RangeTo { get; private set; }

    /// <summary>Próximo secuencial a entregar. Solo avanza; nunca retrocede.</summary>
    public long Next { get; private set; }

    /// <summary>
    /// Fecha de vencimiento de la secuencia (calendario dominicano), o null para
    /// los tipos 32 y 34.
    /// </summary>
    public DateOnly? ExpiresOn { get; private set; }

    /// <summary>Un rango inactivo no entrega secuencias aunque le quede stock.</summary>
    public bool Active { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>Cantidad total de secuencias del rango autorizado.</summary>
    public long Capacity => RangeTo - RangeFrom + 1;

    /// <summary>Secuencias que todavía se pueden entregar.</summary>
    public long Remaining => IsExhausted ? 0 : RangeTo - Next + 1;

    /// <summary>Ya se entregaron todas las secuencias del rango.</summary>
    public bool IsExhausted => Next > RangeTo;

    /// <summary>El stock cayó al 20 % o menos del rango autorizado (RF-07.3).</summary>
    public bool IsLowStock => Remaining <= (long)Math.Ceiling(Capacity * LowStockFraction);

    /// <summary>El rango venció respecto a <paramref name="today"/> (calendario dominicano).</summary>
    public bool IsExpired(DateOnly today) => ExpiresOn is { } expiry && today > expiry;

    /// <summary>
    /// Registra un rango autorizado. <paramref name="authorizedOn"/> es la fecha de
    /// la autorización de la DGII (calendario dominicano); de ella se deriva el
    /// vencimiento: 31 de diciembre del año siguiente, salvo los tipos sin
    /// vencimiento de secuencia.
    /// </summary>
    public static ErrorOr<NcfSequence> Authorize(
        DgiiEnvironment environment,
        EcfType type,
        char series,
        long rangeFrom,
        long rangeTo,
        DateOnly authorizedOn)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(type);

        if (!Encf.IsValidSeries(series))
            return SequenceErrors.InvalidSeries(series);

        if (rangeFrom < 1 || rangeTo < rangeFrom)
            return SequenceErrors.InvalidRange(rangeFrom, rangeTo);

        if (environment == DgiiEnvironment.Cert)
        {
            if (rangeFrom != 1)
                return SequenceErrors.CertMustStartAtOne;

            if (rangeTo > CertMaxSequential)
                return SequenceErrors.CertRangeTooLarge(CertMaxSequential);
        }

        var expiresOn = type.HasSequenceExpiry
            ? new DateOnly(authorizedOn.Year + 1, 12, 31)
            : (DateOnly?)null;

        return new NcfSequence(Guid.CreateVersion7(), environment, type, series, rangeFrom, rangeTo, expiresOn);
    }

    /// <summary>
    /// Entrega la siguiente secuencia del rango y avanza el puntero. El chequeo de
    /// vencimiento va antes que el de stock (RF-07.4). Quien llama es responsable
    /// de tomar el lock pesimista sobre la fila antes de invocar esto.
    /// </summary>
    public ErrorOr<Encf> Allocate(DateOnly today)
    {
        if (!Active)
            return SequenceErrors.RangeInactive;

        if (IsExpired(today))
            return SequenceErrors.RangeExpired(ExpiresOn!.Value);

        if (IsExhausted)
            return SequenceErrors.RangeExhausted;

        var allocated = Next;
        Next = allocated + 1;

        return Encf.Build(Series, Type.Id, allocated);
    }

    /// <summary>Desactiva el rango. Un rango inactivo no vuelve a entregar secuencias.</summary>
    public ErrorOr<Success> Deactivate()
    {
        if (!Active)
            return SequenceErrors.RangeInactive;

        Active = false;

        return Result.Success;
    }
}
