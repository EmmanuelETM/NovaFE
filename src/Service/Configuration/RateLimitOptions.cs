using System.ComponentModel.DataAnnotations;

namespace NovaFE.Service.Configuration;

/// <summary>
/// Límite de peticiones por ventana de tiempo. Se particiona por usuario
/// autenticado, y si no hay usuario, por dirección IP.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Si es false, el middleware sigue en el pipeline pero no limita nada.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Peticiones permitidas por ventana.</summary>
    [Range(1, 1_000_000)]
    public int PermitLimit { get; set; } = 100;

    /// <summary>Duración de la ventana en segundos.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Peticiones que esperan en cola al llenarse la ventana. 0 las rechaza de inmediato.</summary>
    [Range(0, 10_000)]
    public int QueueLimit { get; set; }
}
