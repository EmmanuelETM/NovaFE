namespace NovaFE.Application.Sequences.AllocateNcf;

/// <summary>La secuencia entregada, ya lista para armar el e-CF.</summary>
public sealed record AllocatedNcf(string Encf, int Type, string Series, long Sequential);
