using NovaFE.Application.Settings.Contracts;

namespace NovaFE.Application.Settings.Interfaces;

/// <summary>Read side (Dapper) de los overrides: los usa la pantalla y el snapshot.</summary>
public interface IPlatformSettingReadRepository
{
    Task<IReadOnlyList<PlatformSettingRecord>> ListAsync(CancellationToken ct = default);
}
