using Asp.Versioning;
using NovaFE.Application.Certificates.GetCertificate;
using NovaFE.Application.Certificates.ListCertificates;
using NovaFE.Application.Certificates.RevokeCertificate;
using NovaFE.Application.Certificates.UploadCertificate;
using NovaFE.Domain.Common;
using NovaFE.Service.Common;
using Microsoft.AspNetCore.Mvc;

namespace NovaFE.Service.Controllers;

/// <summary>
/// Certificados digitales del contribuyente. Recurso <b>por tenant</b>: la
/// petición debe traer el header <c>X-Tenant-Id</c>.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class CertificatesController(
    UploadCertificateUseCase upload,
    GetCertificateUseCase get,
    ListCertificatesUseCase list,
    RevokeCertificateUseCase revoke) : ApiController
{
    /// <summary>Sube el .p12/.pfx (multipart/form-data: file, password, environment).</summary>
    [HttpPost]
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
            id => CreatedAtAction(nameof(GetById), new { id, version = "1.0" }, new { id }),
            Problem);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await get.Execute(new GetCertificateQuery(id), ct)).Match(Ok, Problem);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await list.Execute(ct)).Match(Ok, Problem);

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        => (await revoke.Execute(new RevokeCertificateCommand(id), ct)).Match(_ => NoContent(), Problem);

    public sealed class UploadCertificateRequest
    {
        public IFormFile? File { get; init; }

        public string? Password { get; init; }

        public string? Environment { get; init; }
    }
}
