namespace NovaFE.Application.Sequences.Contracts;

/// <summary>La secuencia entregada, ya lista para armar el e-CF.</summary>
public sealed record AllocatedNcfDto(string Encf, int Type, string Series, long Sequential);
