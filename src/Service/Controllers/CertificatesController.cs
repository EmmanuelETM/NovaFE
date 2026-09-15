using Asp.Versioning;
using NovaFE.Application.Certificates.Contracts;
using NovaFE.Application.Certificates.GetCertificate;
using NovaFE.Application.Certificates.ListCertificates;
using NovaFE.Application.Certificates.RevokeCertificate;
using NovaFE.Application.Certificates.UploadCertificate;
using NovaFE.Domain.Common;
using NovaFE.Service.Common;
using NovaFE.Service.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Certificados digitales del contribuyente. Recurso <b>por contribuyente</b>: la
/// petición se autentica con una API key (header <c>X-API-Key</c>) — salvo las
/// acciones <c>...ForTenant</c>, que son de <b>operador</b> (RF onboarding: antes
/// de que el tenant tenga su primera API key, alguien tiene que poder cargarle el
/// certificado). Ver <c>docs/api-auth.md</c> y el comentario de
/// <see cref="UploadForTenant"/>.
/// </summary>
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class CertificatesController(
    UploadCertificateUseCase upload,
    GetCertificateUseCase get,
    ListCertificatesUseCase list,
    RevokeCertificateUseCase revoke,
    CurrentTenant currentTenant) : ApiController
{
    /// <summary>Sube el .p12/.pfx (multipart/form-data: file, password, environment).</summary>
    [HttpPost]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadCertificateRequest request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
            return Problem([Errors.Validation.Required("file")]);

        using var buffer = new MemoryStream();
        await using (var stream = request.File.OpenReadStream())
            await stream.CopyToAsync(buffer, ct);

        var command = new UploadCertificateCommand(
            buffer.ToArray(),
            request.Password ?? string.Empty,
            request.Environment ?? string.Empty);

        return (await upload.Execute(command, ct)).Match(
            id => CreatedAtAction(nameof(GetById), new { id, version = "1" }, new { id }),
            Problem);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetCertificateQuery(id), ct)).Match(Ok, Problem);

    [HttpGet]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = SecurityPolicies.TenantConfig)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        => (await revoke.Execute(new RevokeCertificateCommand(id), ct)).Match(_ => NoContent(), Problem);

    /// <summary>
    /// Carga el certificado de un contribuyente, como <b>operador</b> — no exige
    /// que el tenant ya tenga una API key propia (no la puede tener todavía: para
    /// acuñar la primera, <see cref="Application.Tenants.CreateApiKey.CreateApiKeyUseCase"/>
    /// exige un certificado activo y un rango de secuencia, así que alguien tiene
    /// que poder cargarlos antes de que exista esa key). <c>currentTenant.Set</c>
    /// es <c>internal</c>, llamable porque este controller vive en el mismo
    /// ensamblado que <c>CurrentTenant</c>; con eso fijado, <see cref="Upload"/> no
    /// se entera de quién lo llamó — reusa exactamente la misma validación.
    /// <para>
    /// Asume que RLS todavía no se hace cumplir en ningún ambiente real (el rol
    /// restringido <c>novafe_app</c> sigue pendiente, ver <c>docs/roadmap.md</c>);
    /// hoy la única protección real es el filtro global de EF Core, que sí lee
    /// <c>ICurrentTenant.TenantId</c> en cada query. Cuando ese rol entre en
    /// producción, esto necesita revisarse: la sesión de Postgres fija
    /// <c>app.tenant_id</c> solo al abrir la conexión, no a mitad de una petición.
    /// </para>
    /// </summary>
    [HttpPost("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/certificates")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [RequestSizeLimit(256 * 1024)]
    public Task<IActionResult> UploadForTenant(
        Guid tenantid, [FromForm] UploadCertificateRequest request, CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return Upload(request, ct);
    }

    /// <summary>Los certificados de un contribuyente, como operador. Ver <see cref="UploadForTenant"/>.</summary>
    [HttpGet("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/certificates")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    [ProducesResponseType(typeof(IReadOnlyList<CertificateDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> ListForTenant(Guid tenantid, CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return List(ct);
    }

    /// <summary>Revoca el certificado de un contribuyente, como operador. Ver <see cref="UploadForTenant"/>.</summary>
    [HttpPost("~/api/v{version:apiVersion}/tenants/{tenantid:guid}/certificates/{id:guid}/revoke")]
    [Authorize(Policy = SecurityPolicies.Operator)]
    public Task<IActionResult> RevokeForTenant(Guid tenantid, Guid id, CancellationToken ct)
    {
        currentTenant.Set(tenantid);
        return Revoke(id, ct);
    }

    public sealed class UploadCertificateRequest
    {
        public IFormFile? File { get; init; }

        public string? Password { get; init; }

        public string? Environment { get; init; }
    }
}
