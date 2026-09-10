using ErrorOr;

namespace NovaFE.Domain.Settings;

/// <summary>
/// <see cref="SettingDefinition"/> con tipo: sabe parsear y formatear su propio
/// valor y expone el <see cref="Default"/> tipado. Es lo que consume
/// <c>ISettingsReader.Get&lt;T&gt;</c>.
/// <para>
/// Un valor corrupto en la base <b>no lanza</b>: el lector cae a
/// <see cref="Default"/> (ver <c>docs/configuration.md</c>).
/// </para>
/// </summary>
public abstract class SettingDefinition<T> : SettingDefinition
{
    private protected SettingDefinition(
        string key,
        string valueType,
        string group,
        string label,
        T @default,
        string? description,
        string? unit,
        SettingScope scope,
        bool killSwitch,
        bool sensitive,
        bool deprecated)
        : base(key, valueType, group, label, description, unit, scope, killSwitch, sensitive, deprecated)
    {
        Default = @default;
    }

    /// <summary>Valor de código. Es el piso de la resolución y siempre es válido.</summary>
    public T Default { get; }

    public sealed override string SerializedDefault => Format(Default);

    /// <summary>Convierte un valor tipado a su forma de texto persistible.</summary>
    public abstract string Format(T value);

    /// <summary>Intenta parsear el texto. Devuelve <c>false</c> si no tiene la forma del tipo.</summary>
    public abstract bool TryParse(string raw, out T value);

    /// <summary>
    /// Reglas sobre un valor ya parseado (rango, opción conocida…). Por defecto no
    /// hay ninguna: la sola capacidad de parsear basta.
    /// </summary>
    protected virtual ErrorOr<Success> ValidateTyped(T value) => Result.Success;

    public sealed override string Canonicalize(string raw)
        => raw is not null && TryParse(raw, out var value) ? Format(value) : raw ?? string.Empty;

    public sealed override ErrorOr<Success> Validate(string raw)
    {
        if (raw is null || !TryParse(raw, out var value))
            return Error.Validation(
                code: $"Setting.{Key}",
                description: Constraints is { } c
                    ? $"«{raw}» no es un valor válido para {Label}. {c}."
                    : $"«{raw}» no tiene el formato esperado ({ValueType}).");

        return ValidateTyped(value);
    }
}
