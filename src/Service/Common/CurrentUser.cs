using System.Security.Claims;
using NovaFE.Application.Common.Interfaces;

namespace NovaFE.Service.Common;

/// <summary>
/// Lee el usuario actual de los claims de la petición HTTP.
/// <para>
/// Funciona <b>con o sin autenticación configurada</b>: mientras no haya un
/// esquema de autenticación, <see cref="IsAuthenticated"/> devuelve false y el
/// resto de las propiedades vienen en null, sin lanzar. Al habilitar JWT en
/// <c>Program.cs</c>, empieza a devolver datos reales sin tocar nada más.
/// </para>
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? Id =>
        Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? Principal?.FindFirst("sub")?.Value;

    public string? UserName =>
        Principal?.FindFirst(ClaimTypes.Name)?.Value
        ?? Principal?.FindFirst("preferred_username")?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
