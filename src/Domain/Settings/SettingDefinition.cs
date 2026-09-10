using ErrorOr;

namespace NovaFE.Domain.Settings;

/// <summary>
/// Declaración de un parámetro de configuración runtime. Se declara <b>una vez</b>
/// como campo estático en <see cref="SettingDefinitions"/> y funciona desde el
/// despliegue: si no hay una fila de override en la base, rige
/// <see cref="SerializedDefault"/>.
/// <para>
/// Esta base no genérica es la que recorren el registro por reflexión y la
/// pantalla de administración. La lectura tipada va por
/// <see cref="SettingDefinition{T}"/>.
/// </para>
/// </summary>
public abstract class SettingDefinition
{
    private protected SettingDefinition(
        string key,
        string valueType,
        string group,
        string label,
        string? description,
        string? unit,
        SettingScope scope,
        bool killSwitch,
        bool sensitive,
        bool deprecated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueType);
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Key = key;
        ValueType = valueType;
        Group = group;
        Label = label;
        Description = description;
        Unit = unit;
        Scope = scope;
        KillSwitch = killSwitch;
        Sensitive = sensitive;
        Deprecated = deprecated;
    }

    /// <summary>Clave namespaced y estable: <c>platform.maintenance_mode</c>.</summary>
    public string Key { get; }

    /// <summary>Discriminador de tipo para la pantalla: <c>boolean</c>, <c>integer</c>, <c>duration</c>…</summary>
    public string ValueType { get; }

    /// <summary>Grupo bajo el que se muestra en la pantalla de administración.</summary>
    public string Group { get; }

    /// <summary>Etiqueta corta para la pantalla.</summary>
    public string Label { get; }

    /// <summary>Texto de ayuda para la pantalla.</summary>
    public string? Description { get; }

    /// <summary>Unidad a mostrar junto al valor (<c>segundos</c>, <c>días</c>…), si aplica.</summary>
    public string? Unit { get; }

    /// <summary>Capas de resolución que aplican. En este slice solo <see cref="SettingScope.Platform"/>.</summary>
    public SettingScope Scope { get; }

    /// <summary>
    /// Interruptor de emergencia (modo mantenimiento, contingencia, apagar un
    /// worker): se lee con menor latencia y su cambio debe propagarse rápido.
    /// </summary>
    public bool KillSwitch { get; }

    /// <summary>Cambiarlo exige confirmación explícita en el <c>PUT</c>.</summary>
    public bool Sensitive { get; }

    /// <summary>Oculto en la pantalla; sigue resolviendo. Rechaza nuevos overrides.</summary>
    public bool Deprecated { get; }

    /// <summary>El <see cref="Default"/> serializado a texto (lo que se compara con la fila de override).</summary>
    public abstract string SerializedDefault { get; }

    /// <summary>
    /// Restricciones legibles para la pantalla (rango, opciones, patrón), o null
    /// si el tipo no tiene ninguna.
    /// </summary>
    public virtual string? Constraints => null;

    /// <summary>
    /// Valida un valor crudo (el que llega en el <c>PUT</c> o el que se lee de la
    /// base). No lanza: los errores se devuelven.
    /// </summary>
    public abstract ErrorOr<Success> Validate(string raw);

    /// <summary>
    /// Devuelve la forma canónica de un valor válido (<c>1</c> → <c>true</c>,
    /// <c>POS</c> → <c>pos</c>); si no parsea, lo devuelve tal cual.
    /// </summary>
    public abstract string Canonicalize(string raw);
}
