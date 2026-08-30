namespace NovaFE.Application.Common.Interfaces;

/// <summary>
/// Envuelve y desenvuelve la clave de datos (DEK) con la que se cifra cada
/// secreto. La implementación local guarda la clave que protege (KEK) en
/// configuración; en producción será un KMS (AWS / GCP / Azure Key Vault) sin
/// tocar nada más. Es la costura que hace que el vault no dependa del proveedor.
/// </summary>
public interface IKeyProtector
{
    Task<byte[]> WrapAsync(byte[] dataKey, CancellationToken ct = default);

    Task<byte[]> UnwrapAsync(byte[] wrappedKey, CancellationToken ct = default);
}
