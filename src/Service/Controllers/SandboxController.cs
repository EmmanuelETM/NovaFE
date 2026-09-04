using Asp.Versioning;
using NovaFE.Application.Certificates.UploadCertificate;
using NovaFE.Application.Sequences.RegisterSequenceRange;
using NovaFE.Application.Tenants.CreateApiKey;
using NovaFE.Application.Tenants.RegisterTenant;
using NovaFE.Application.Tenants.SetEmitterProfile;
using NovaFE.Service.Common;
using NovaFE.Service.DevTools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace NovaFE.Service.Controllers;

/// <summary>
/// <b>Solo Development.</b> Deja un contribuyente listo para emitir en un solo
/// paso: lo registra, le pone perfil de emisor, le carga rangos de secuencia y le
/// sube un certificado autofirmado. Pensado para probar el pipeline completo
/// contra el simulador de la DGII (<c>dev/dgii-sim</c>), sin un certificado real.
/// <para>
/// Fuera de Development este controller no existe
/// (<see cref="RemoveDevelopmentOnlyConvention"/>).
/// </para>
/// </summary>
[DevelopmentOnly]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/dev/[controller]")]
public sealed class SandboxController(
    RegisterTenantUseCase registerTenant,
    SetEmitterProfileUseCase setEmitterProfile,
    RegisterSequenceRangeUseCase registerSequence,
    UploadCertificateUseCase uploadCertificate,
    CreateApiKeyUseCase createApiKey) : ApiController
{
    private static readonly int[] DefaultSequenceTypes = [31, 32, 33, 34];

    /// <summary>
    /// Onboarding completo. Todo el body es opcional; con <c>{}</c> alcanza.
    /// Devuelve el <c>tenantId</c> — úsalo como header <c>X-Tenant-Id</c> en
    /// <c>POST /api/v1/ecf</c>.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SandboxRequest? body, CancellationToken ct)
    {
        var request = body ?? new SandboxRequest();
        var rnc = string.IsNullOrWhiteSpace(request.Rnc)
            ? Random.Shared.Next(100_000_000, 1_000_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : request.Rnc.Trim();
        var environment = string.IsNullOrWhiteSpace(request.Environment) ? "Test" : request.Environment.Trim();
        var types = request.SequenceTypes is { Count: > 0 } ? request.SequenceTypes : DefaultSequenceTypes;

        var tenant = await registerTenant.Execute(
            new RegisterTenantCommand(rnc, $"Sandbox {rnc} SRL", "Sandbox", "Business"), ct);
        if (tenant.IsError)
            return Problem(tenant.Errors);

        // Los pasos siguientes actúan como el tenant recién creado.
        HttpContext.RequestServices.GetRequiredService<CurrentTenant>().Set(tenant.Value);

        var profile = await setEmitterProfile.Execute(new SetEmitterProfileCommand(
            tenant.Value,
            Address: "Av. Winston Churchill 1099, Piantini",
            Municipality: "010100",
            Province: "010000",
            Phones: ["809-555-0100"],
            Email: "sandbox@novafe.local",
            EconomicActivity: "Pruebas de facturación electrónica",
            DefaultEnvironment: environment), ct);
        if (profile.IsError)
            return Problem(profile.Errors);

        foreach (var type in types)
        {
            var range = await registerSequence.Execute(
                new RegisterSequenceRangeCommand(environment, type, "E", 1, 1000), ct);
            if (range.IsError)
                return Problem(range.Errors);
        }

        var certificate = await uploadCertificate.Execute(new UploadCertificateCommand(
            DevCertificateFactory.Create(rnc), DevCertificateFactory.DefaultPassword, environment), ct);
        if (certificate.IsError)
            return Problem(certificate.Errors);

        var apiKey = await createApiKey.Execute(
            new CreateApiKeyCommand(tenant.Value, "Sandbox", environment, ExpiresAt: null), ct);
        if (apiKey.IsError)
            return Problem(apiKey.Errors);

        return Ok(new
        {
            tenantId = tenant.Value,
            rnc,
            environment,
            sequenceTypes = types,
            apiKey = apiKey.Value.Token,
            note = "Usa apiKey como header X-API-Key (o tenantId como X-Tenant-Id) en POST /api/v1/ecf.",
        });
    }

    /// <summary>El PKCS#12 autofirmado suelto, por si prefieres subirlo a mano.</summary>
    [HttpGet("certificate")]
    public IActionResult Certificate([FromQuery] string rnc, [FromQuery] string? password)
    {
        if (string.IsNullOrWhiteSpace(rnc))
            return Problem([NovaFE.Domain.Common.Errors.Validation.Required("rnc")]);

        var pkcs12 = DevCertificateFactory.Create(rnc.Trim(), password ?? DevCertificateFactory.DefaultPassword);
        return File(pkcs12, "application/x-pkcs12", $"sandbox-{rnc}.p12");
    }

    public sealed record SandboxRequest(
        string? Rnc = null,
        string? Environment = null,
        IReadOnlyList<int>? SequenceTypes = null);
}
