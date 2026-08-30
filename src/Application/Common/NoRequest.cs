namespace NovaFE.Application.Common;

/// <summary>Request vacío para casos de uso que no reciben parámetros.</summary>
public sealed record NoRequest
{
    public static readonly NoRequest Instance = new();
}
