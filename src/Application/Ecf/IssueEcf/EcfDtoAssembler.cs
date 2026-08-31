using NovaFE.Application.Ecf.Contracts;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Fiscal;

namespace NovaFE.Application.Ecf.IssueEcf;

/// <summary>Arma la respuesta (<see cref="EcfDto"/>) desde el agregado recién emitido.</summary>
internal static class EcfDtoAssembler
{
    public static EcfDto From(IssuedEcf ecf) => new(
        Id: ecf.Id,
        Status: ecf.Status.PublicName,
        Encf: ecf.Encf.Value,
        Type: ecf.Type.Id,
        Environment: ecf.Environment.Name,
        SequenceExpiresOn: ecf.SequenceExpiresOn,
        IssueDate: ecf.IssueDate,
        IssuedAt: ecf.CreatedAt,
        SignedAt: ecf.SignedAt,
        SecurityCode: ecf.SecurityCode,
        QrUrl: ecf.QrUrl,
        SubmitsRfce: ecf.SubmitsRfce,
        InternalNumber: ecf.InternalInvoiceNumber,
        BuyerRnc: ecf.BuyerRnc,
        BuyerName: ecf.BuyerName,
        Totals: ecf.Totals,
        ToleranceWarning: ecf.ExpectedConditionalAcceptance
            ? "Los montos declarados no cuadran dentro de la tolerancia; la DGII podría aceptar el comprobante de forma condicional."
            : null);

    /// <summary>
    /// Los totales declarados por el cliente en el encabezado quedan fuera de la
    /// tolerancia respecto al cálculo de NovaFE (RF-06.6). Nunca bloquea.
    /// </summary>
    public static bool DeclaredHeaderTotalsOutOfTolerance(EcfTotals calculated, EcfDeclaredTotalsPayload? declared)
    {
        if (declared is null)
            return false;

        return OutOfTolerance(declared.MontoTotal, calculated.MontoTotal)
            || OutOfTolerance(declared.MontoGravadoTotal, calculated.MontoGravadoTotal)
            || OutOfTolerance(declared.MontoExento, calculated.MontoExento)
            || OutOfTolerance(declared.TotalItbis, calculated.TotalItbis)
            || OutOfTolerance(declared.MontoImpuestoAdicional, calculated.MontoImpuestoAdicional);
    }

    private static bool OutOfTolerance(decimal? declared, decimal calculated)
        => declared is { } value && Math.Abs(value - calculated) > 1m;
}
