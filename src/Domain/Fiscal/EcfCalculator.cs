using ErrorOr;

namespace NovaFE.Domain.Fiscal;

/// <summary>
/// Motor de cálculo fiscal del e-CF (Módulo 6). Función pura y determinística:
/// dadas las líneas de detalle, devuelve <c>&lt;MontoItem&gt;</c> por línea, todos
/// los totalizadores del Encabezado y el análisis de tolerancia de cuadratura.
/// Sin dependencias externas, sin reloj, sin estado.
/// <para>
/// Todo se calcula en <see cref="decimal"/> y se redondea con
/// <see cref="EcfRounding"/> (regla de la DGII, mitad hacia afuera del cero).
/// </para>
/// <para>
/// Alcance v1: ITBIS (tasas 18/16/0 y exento), ajustes de línea
/// (<c>DescuentoMonto</c>/<c>RecargoMonto</c>), "otros impuestos adicionales" que
/// el cliente ya trae calculados, el <b>ISC específico</b> por línea (monto fijo
/// que el cliente trae calculado) integrado a la base del ITBIS, la
/// <b>totalización</b> de las retenciones de ITBIS/ISR por línea (tipos 41/47), y
/// la <b>reconciliación mecánica</b> de los descuentos/recargos globales de la
/// Sección D (se aplican al bucket que indica
/// <c>IndicadorFacturacionDescuentooRecargo</c> y se recalcula su ITBIS; la
/// Norma 10-07 solo baja el valor a pagar). <b>Fuera de v1</b> (slices aparte, ver
/// <c>docs/fiscal.md</c>): la <b>derivación</b> del ISC específico y del ad valorem
/// desde <c>GradosAlcohol</c>/<c>CantidadReferencia</c> (RF-06.4/06.5), la
/// distribución proporcional de la Sección D a nivel de línea, y el cálculo de las
/// tasas de retención a partir de las normas de la DGII.
/// </para>
/// </summary>
public static class EcfCalculator
{
    /// <summary>
    /// Calcula el e-CF. <paramref name="montoNoFacturable"/> son montos que no
    /// forman parte de la factura (reembolsos, propina voluntaria); puede ser
    /// negativo y solo afecta a <c>&lt;MontoPeriodo&gt;</c>, no a
    /// <c>&lt;MontoTotal&gt;</c>.
    /// </summary>
    public static ErrorOr<EcfCalculationResult> Calculate(
        IReadOnlyList<EcfLineInput> lines,
        decimal montoNoFacturable = 0m,
        IReadOnlyList<EcfGlobalAdjustmentInput>? globalAdjustments = null)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var validation = Validate(lines);
        if (validation.Count > 0)
            return validation;

        var results = new List<EcfLineResult>(lines.Count);

        decimal gravadoI1 = 0m, gravadoI2 = 0m, gravadoI3 = 0m;
        decimal itbis1 = 0m, itbis2 = 0m, itbis3 = 0m;
        decimal isc1 = 0m, isc2 = 0m, isc3 = 0m;   // ISC específico por bucket (integra la base del ITBIS)
        decimal exento = 0m;
        decimal otrosImpuestos = 0m;
        decimal selectivoConsumo = 0m;
        decimal itbisRetenido = 0m, isrRetenido = 0m;

        foreach (var line in lines)
        {
            var result = CalculateLine(line);
            results.Add(result);

            itbisRetenido += line.ItbisWithheld;
            isrRetenido += line.IsrWithheld;
            selectivoConsumo += line.IscSpecific;

            switch (line.Rate.Id)
            {
                case 1:
                    gravadoI1 += result.TaxableBase;
                    itbis1 += result.TaxAmount;
                    isc1 += line.IscSpecific;
                    break;
                case 2:
                    gravadoI2 += result.TaxableBase;
                    itbis2 += result.TaxAmount;
                    isc2 += line.IscSpecific;
                    break;
                case 3:
                    gravadoI3 += result.TaxableBase;
                    itbis3 += result.TaxAmount;
                    isc3 += line.IscSpecific;
                    break;
                default: // 4 — Exento
                    exento += result.ExemptAmount;
                    break;
            }

            otrosImpuestos += result.AdditionalTaxes;
        }

        // --- Sección D: descuentos/recargos globales -----------------------
        decimal norma1007Discount = 0m;
        decimal globalAdjustmentNet = 0m;   // firmado: + recargo, − descuento
        var touched = new HashSet<int>();

        foreach (var adj in globalAdjustments ?? [])
        {
            if (adj.Amount < 0m)
                return FiscalErrors.NegativeGlobalAdjustment;

            var signed = adj.IsDiscount ? -adj.Amount : adj.Amount;

            // Norma 10-07: solo descuentos a la tasa 1 — no toca la base ni el ITBIS.
            if (adj.Norma1007 && adj.IsDiscount && adj.AffectsRate == ItbisRate.Eighteen)
            {
                norma1007Discount += adj.Amount;
                continue;
            }

            globalAdjustmentNet += signed;
            switch (adj.AffectsRate.Id)
            {
                case 1: gravadoI1 += signed; touched.Add(1); break;
                case 2: gravadoI2 += signed; touched.Add(2); break;
                case 3: gravadoI3 += signed; touched.Add(3); break;
                default: exento += signed; touched.Add(4); break;
            }
        }

        if (gravadoI1 < 0m || gravadoI2 < 0m || gravadoI3 < 0m || exento < 0m)
            return FiscalErrors.GlobalAdjustmentExceedsBucket;

        // El ITBIS de los buckets tocados se recalcula sobre la nueva base. El ISC
        // específico del bucket integra esa base (contexto §5.2 nota 12).
        if (touched.Contains(1)) itbis1 = (gravadoI1 + isc1) * ItbisRate.Eighteen.Rate;
        if (touched.Contains(2)) itbis2 = (gravadoI2 + isc2) * ItbisRate.Sixteen.Rate;
        // bucket 3 (0 %): el ITBIS es siempre 0.

        var gravadoTotal = EcfRounding.Money(gravadoI1 + gravadoI2 + gravadoI3);
        var totalItbis = EcfRounding.Money(itbis1 + itbis2 + itbis3);
        exento = EcfRounding.Money(exento);
        otrosImpuestos = EcfRounding.Money(otrosImpuestos);
        selectivoConsumo = EcfRounding.Money(selectivoConsumo);

        var impuestoAdicional = EcfRounding.Money(selectivoConsumo + otrosImpuestos);

        var montoTotal = EcfRounding.Money(gravadoTotal + exento + totalItbis + impuestoAdicional);
        var noFacturable = EcfRounding.Money(montoNoFacturable);
        var montoPeriodo = EcfRounding.Money(montoTotal + noFacturable);

        var totals = new EcfTotals(
            MontoGravadoI1: EcfRounding.Money(gravadoI1),
            MontoGravadoI2: EcfRounding.Money(gravadoI2),
            MontoGravadoI3: EcfRounding.Money(gravadoI3),
            MontoGravadoTotal: gravadoTotal,
            Itbis1: EcfRounding.Money(itbis1),
            Itbis2: EcfRounding.Money(itbis2),
            Itbis3: EcfRounding.Money(itbis3),
            TotalItbis: totalItbis,
            MontoExento: exento,
            TotalImpuestoSelectivoConsumo: selectivoConsumo,
            TotalOtrosImpuestosAdicionales: otrosImpuestos,
            MontoImpuestoAdicional: impuestoAdicional,
            MontoTotal: montoTotal,
            MontoNoFacturable: noFacturable,
            MontoPeriodo: montoPeriodo,
            TotalItbisWithheld: EcfRounding.Money(itbisRetenido),
            TotalIsrWithheld: EcfRounding.Money(isrRetenido),
            TotalGlobalAdjustment: EcfRounding.Money(globalAdjustmentNet),
            Norma1007Discount: EcfRounding.Money(norma1007Discount));

        var tolerance = BuildToleranceReport(lines, results);

        return new EcfCalculationResult(results, totals, tolerance);
    }

    private static EcfLineResult CalculateLine(EcfLineInput line)
    {
        // Precisión completa hasta redondear MontoItem.
        var raw = (line.UnitPrice * line.Quantity) - line.Discount + line.Surcharge;
        var lineAmount = EcfRounding.Money(raw);

        decimal taxableBase;
        decimal taxAmount;
        decimal exemptAmount;

        // El ISC específico integra la base imponible del ITBIS pero NO el
        // <MontoGravado> reportado: se suma antes de aplicar la tasa y luego vive
        // en <MontoImpuestoAdicional>, así <MontoTotal> no lo cuenta dos veces.
        var isc = line.IscSpecific;

        if (line.Rate.IsExempt)
        {
            taxableBase = 0m;
            taxAmount = 0m;
            exemptAmount = lineAmount;
        }
        else if (line.PriceIncludesTax)
        {
            // El precio ya trae ITBIS (+ ISC): base+ISC = precio / (1 + tasa); el
            // ITBIS es el resto y <MontoGravado> es esa base menos el ISC.
            var baseWithIsc = EcfRounding.Money(lineAmount / (1m + line.Rate.Rate));
            taxAmount = lineAmount - baseWithIsc;
            taxableBase = baseWithIsc - isc;
            exemptAmount = 0m;
        }
        else
        {
            taxableBase = lineAmount;
            taxAmount = EcfRounding.Money((lineAmount + isc) * line.Rate.Rate);
            exemptAmount = 0m;
        }

        return new EcfLineResult(
            LineNumber: line.LineNumber,
            BillingIndicator: line.Rate.Id,
            LineAmount: lineAmount,
            TaxableBase: taxableBase,
            TaxAmount: taxAmount,
            ExemptAmount: exemptAmount,
            AdditionalTaxes: EcfRounding.Money(line.AdditionalTaxes));
    }

    private static EcfToleranceReport BuildToleranceReport(
        IReadOnlyList<EcfLineInput> lines,
        List<EcfLineResult> results)
    {
        var diffs = new List<EcfLineToleranceDiff>();
        var totalDifference = 0m;

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].SuppliedLineAmount is not { } supplied)
                continue;

            var calculated = results[i].LineAmount;
            var difference = Math.Abs(calculated - supplied);
            totalDifference += difference;

            diffs.Add(new EcfLineToleranceDiff(
                LineNumber: lines[i].LineNumber,
                Calculated: calculated,
                Supplied: supplied,
                Difference: difference,
                WithinLineTolerance: difference <= 1m));
        }

        var globalTolerance = lines.Count;
        var withinTolerance = totalDifference <= globalTolerance && diffs.TrueForAll(d => d.WithinLineTolerance);

        return new EcfToleranceReport(diffs, totalDifference, globalTolerance, withinTolerance);
    }

    private static List<Error> Validate(IReadOnlyList<EcfLineInput> lines)
    {
        var errors = new List<Error>();

        if (lines.Count == 0)
        {
            errors.Add(FiscalErrors.NoLines);
            return errors;
        }

        var seen = new HashSet<int>();

        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line.Rate);

            if (line.LineNumber < 1)
                errors.Add(FiscalErrors.InvalidLineNumber(line.LineNumber));
            else if (!seen.Add(line.LineNumber))
                errors.Add(FiscalErrors.DuplicateLineNumber(line.LineNumber));

            if (line.Quantity < 0m)
                errors.Add(FiscalErrors.NegativeQuantity(line.LineNumber));

            if (line.UnitPrice < 0m)
                errors.Add(FiscalErrors.NegativeUnitPrice(line.LineNumber));

            if (line.Discount < 0m || line.Surcharge < 0m || line.AdditionalTaxes < 0m || line.IscSpecific < 0m)
                errors.Add(FiscalErrors.NegativeAdjustment(line.LineNumber));

            if (line.ItbisWithheld < 0m || line.IsrWithheld < 0m)
                errors.Add(FiscalErrors.NegativeRetention(line.LineNumber));

            var raw = (line.UnitPrice * line.Quantity) - line.Discount + line.Surcharge;
            var lineAmount = EcfRounding.Money(raw);
            if (lineAmount < 0m)
                errors.Add(FiscalErrors.NegativeLineAmount(line.LineNumber));

            // Con precio-con-ITBIS, el ISC específico se resta de la base extraída;
            // si la supera, <MontoGravado> daría negativo.
            if (line.IscSpecific > 0m && line.PriceIncludesTax && !line.Rate.IsExempt
                && EcfRounding.Money(lineAmount / (1m + line.Rate.Rate)) - line.IscSpecific < 0m)
                errors.Add(FiscalErrors.IscSpecificExceedsTaxableBase(line.LineNumber));
        }

        return errors;
    }
}
