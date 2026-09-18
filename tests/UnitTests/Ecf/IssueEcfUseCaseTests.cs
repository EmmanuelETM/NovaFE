using ErrorOr;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Application.Ecf.IssueEcf;
using NovaFE.Application.Ecf.Submission;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Ecf;
using NovaFE.Domain.Sequences;
using NovaFE.Domain.Settings;
using NovaFE.Domain.Tenants;
using NovaFE.Domain.Webhooks;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Ecf;

public class IssueEcfUseCaseTests : UseCaseTestBase
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly ITenantRepository _tenants = Substitute.For<ITenantRepository>();
    private readonly IEmitterProfileRepository _profiles = Substitute.For<IEmitterProfileRepository>();
    private readonly IIdempotencyStore _idempotency = Substitute.For<IIdempotencyStore>();
    private readonly INcfSequenceAllocator _allocator = Substitute.For<INcfSequenceAllocator>();
    private readonly IEcfSigner _signer = Substitute.For<IEcfSigner>();
    private readonly IEcfRepository _ecf = Substitute.For<IEcfRepository>();
    private readonly IEcfReadRepository _ecfReads = Substitute.For<IEcfReadRepository>();
    private readonly IEcfSubmissionQueue _queue = Substitute.For<IEcfSubmissionQueue>();
    private readonly IEcfSubmissionFastPath _fastPath = Substitute.For<IEcfSubmissionFastPath>();
    private readonly IWebhookOutbox _webhookOutbox = Substitute.For<IWebhookOutbox>();
    private readonly ISettingsReader _settingsReader = Substitute.For<ISettingsReader>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public IssueEcfUseCaseTests()
    {
        _tenant.TenantId.Returns(TenantId);

        // La UoW de prueba solo ejecuta la operación (sin transacción real).
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((Func<CancellationToken, Task>)call[0]).Invoke(call.Arg<CancellationToken>()));

        _tenants.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(Tenant.Register(Rnc.FromStorage("132786262"), "AlMax Solutions EIRL", "AlMax"));

        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(EmitterProfile.Create(TenantId, "Av. 27 de Febrero 100", "010100", "01",
                ["809-555-0100"], "f@almax.do", "Comercio", DgiiEnvironment.Test).Value);

        _idempotency.BeginAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyOutcome(IdempotencyDecision.Proceed));

        _allocator.AllocateAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<EcfType>(), Arg.Any<CancellationToken>())
            .Returns(new NcfAllocation(Encf.Build('E', 31, 42), new DateOnly(2027, 12, 31)));

        _signer.SignAsync(Arg.Any<EcfDocument>(), Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(call => SignOf(call.Arg<EcfDocument>()));

        // Default real del setting: apagado — las pruebas existentes no deben
        // ni consultar la huella.
        _settingsReader.GetValue(SettingDefinitions.EcfDuplicateDetectionMode).Returns("off");
        _settingsReader.GetValue(SettingDefinitions.EcfDuplicateDetectionWindow).Returns(TimeSpan.FromMinutes(5));
    }

    private static ErrorOr<SignedEcf> SignOf(EcfDocument document) => new SignedEcf(
        SignedAt: new DateTimeOffset(2026, 2, 21, 14, 30, 5, TimeSpan.Zero),
        EcfXml: $"<ECF><enc>{document.Header.Encf.Value}</enc><Signature/></ECF>",
        RfceXml: document.QualifiesForRfce ? "<RFCE><Signature/></RFCE>" : null,
        SignatureValue: "aB3xZ9KkLlMm",
        SecurityCode: "aB3xZ9",
        DocumentHash: new string('a', 64),
        QrUrl: "https://ecf.dgii.gov.do/testecf/consultatimbre?x=1");

    private IssueEcfUseCase Sut() => new(
        LoggerFactory,
        new IssueEcfCommandValidator(Clock),
        _tenant, _tenants, _profiles, _idempotency, _allocator, _signer, _ecf, _ecfReads, _queue,
        _fastPath, new EcfSubmissionSettings { SyncWaitBudget = TimeSpan.Zero },
        _webhookOutbox, _settingsReader, _uow, Clock);

    private static IssueEcfCommand Command() => new()
    {
        Type = 31,
        IncomeType = "01",
        IssueDate = new DateOnly(2026, 1, 10),
        Buyer = new EcfBuyerPayload(Name: "Cliente SRL", Rnc: "131880681"),
        Payment = new EcfPaymentPayload("credit", new DateOnly(2026, 2, 10),
            [new EcfPaymentMethodPayload("check_transfer", 2360m)]),
        Lines = [new EcfLinePayload("Servicio", Kind: "service", Quantity: 1, UnitPrice: 2000m, ItbisRate: 1, UnitOfMeasure: "43")],
    };

    [Fact]
    public async Task Issues_signs_and_persists_a_new_ecf()
    {
        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeFalse();
        result.Value.WasCreated.ShouldBeTrue();
        var dto = result.Value.Ecf;
        dto.Status.ShouldBe("signed");
        dto.Encf.ShouldBe("E310000000042");
        dto.Type.ShouldBe(31);
        dto.SequenceExpiresOn.ShouldBe(new DateOnly(2027, 12, 31));
        dto.SecurityCode.ShouldBe("aB3xZ9");
        dto.QrUrl.ShouldContain("consultatimbre");
        dto.Links.Xml.ShouldBe($"/api/v1/ecf/{dto.Id}/xml");

        await _ecf.Received(1).AddAsync(
            Arg.Is<IssuedEcf>(e => e.Encf.Value == "E310000000042" && e.Status == EcfStatus.Signed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enqueues_the_comprobante_for_submission_to_the_dgii()
    {
        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeFalse();
        await _queue.Received(1).EnqueueSubmitAsync(
            Arg.Is<Guid>(id => id == result.Value.Ecf.Id),
            TenantId,
            Arg.Is<DgiiEnvironment>(e => e == DgiiEnvironment.Test),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_the_emitter_profile_is_not_configured()
    {
        _profiles.GetByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns((EmitterProfile?)null);

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("EmitterProfile.NotConfigured");
        await _allocator.DidNotReceive().AllocateAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<EcfType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Replays_the_original_response_for_a_repeated_idempotency_key()
    {
        var existingId = Guid.CreateVersion7();
        _idempotency.BeginAsync(TenantId, "key-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyOutcome(IdempotencyDecision.Replay, existingId));
        _ecfReads.GetByIdAsync(existingId, TenantId, Arg.Any<CancellationToken>())
            .Returns(StubDto(existingId));

        var result = await Sut().Execute(Command() with { IdempotencyKey = "key-1" });

        result.IsError.ShouldBeFalse();
        result.Value.WasCreated.ShouldBeFalse();
        result.Value.Ecf.Id.ShouldBe(existingId);
        await _allocator.DidNotReceive().AllocateAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<EcfType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_request_in_progress_is_a_conflict()
    {
        _idempotency.BeginAsync(TenantId, "key-2", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new IdempotencyOutcome(IdempotencyDecision.InProgress));

        var result = await Sut().Execute(Command() with { IdempotencyKey = "key-2" });

        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Ecf.RequestInProgress");
    }

    [Fact]
    public async Task A_repeated_internal_number_returns_the_existing_ecf()
    {
        var existingId = Guid.CreateVersion7();
        _ecfReads.FindByInternalNumberAsync(TenantId, "FAC-42", Arg.Any<CancellationToken>()).Returns(existingId);
        _ecfReads.GetByIdAsync(existingId, TenantId, Arg.Any<CancellationToken>()).Returns(StubDto(existingId));

        var result = await Sut().Execute(Command() with { InternalNumber = "FAC-42" });

        result.IsError.ShouldBeFalse();
        result.Value.WasCreated.ShouldBeFalse();
        result.Value.Ecf.Id.ShouldBe(existingId);
    }

    [Fact]
    public async Task Propagates_an_allocation_error()
    {
        _allocator.AllocateAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<EcfType>(), Arg.Any<CancellationToken>())
            .Returns(Error.NotFound("Sequence.NoAuthorizedRange", "sin rango"));

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("Sequence.NoAuthorizedRange");
        await _ecf.DidNotReceive().AddAsync(Arg.Any<IssuedEcf>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remaps_a_missing_certificate_to_a_client_error()
    {
        _signer.SignAsync(Arg.Any<EcfDocument>(), Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Certificate.NoActiveCertificate", "sin certificado"));

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("Certificate.NoActiveCertificate");
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Duplicate_detection_off_never_queries_for_a_match()
    {
        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeFalse();
        await _ecfReads.DidNotReceive().FindRecentDuplicateAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<decimal>(), Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_buyer_without_rnc_skips_duplicate_detection_even_in_blocking_mode()
    {
        _settingsReader.GetValue(SettingDefinitions.EcfDuplicateDetectionMode).Returns("bloquear");
        _allocator.AllocateAsync(Arg.Any<DgiiEnvironment>(), EcfType.Consumo, Arg.Any<CancellationToken>())
            .Returns(new NcfAllocation(Encf.Build('E', 32, 1), null));

        // Consumo bajo DOP 250,000: no necesita identificar al comprador — el
        // caso real que motiva la regla (facturas sin RNC, mismo monto, en minutos).
        var command = new IssueEcfCommand
        {
            Type = 32,
            IncomeType = "01",
            Payment = new EcfPaymentPayload("cash", Methods: [new EcfPaymentMethodPayload("cash", 1180m)]),
            Lines = [new EcfLinePayload("Inscripción", Kind: "service", Quantity: 1, UnitPrice: 1000m, ItbisRate: 1, UnitOfMeasure: "43")],
        };

        var result = await Sut().Execute(command);

        result.IsError.ShouldBeFalse();
        await _ecfReads.DidNotReceive().FindRecentDuplicateAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<decimal>(), Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Blocking_mode_rejects_a_recent_duplicate_without_persisting_anything()
    {
        _settingsReader.GetValue(SettingDefinitions.EcfDuplicateDetectionMode).Returns("bloquear");
        _ecfReads.FindRecentDuplicateAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
                Arg.Any<decimal>(), Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(((Guid Id, string Encf)?)(Guid.CreateVersion7(), "E310000000041"));

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("Ecf.DuplicateSuspected");
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        await _signer.DidNotReceive().SignAsync(Arg.Any<EcfDocument>(), Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>());
        await _ecf.DidNotReceive().AddAsync(Arg.Any<IssuedEcf>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Observing_mode_issues_normally_and_enqueues_a_duplicate_suspected_webhook()
    {
        _settingsReader.GetValue(SettingDefinitions.EcfDuplicateDetectionMode).Returns("observar");
        _ecfReads.FindRecentDuplicateAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
                Arg.Any<decimal>(), Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(((Guid Id, string Encf)?)(Guid.CreateVersion7(), "E310000000041"));

        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeFalse();
        await _ecf.Received(1).AddAsync(Arg.Any<IssuedEcf>(), Arg.Any<CancellationToken>());
        await _webhookOutbox.Received(1).EnqueueAsync(
            Arg.Is<WebhookEventEnvelope>(e => e.Type == WebhookEventType.EcfDuplicateSuspected),
            TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_credit_note_that_exceeds_the_original_it_modifies()
    {
        _allocator.AllocateAsync(Arg.Any<DgiiEnvironment>(), EcfType.NotaCredito, Arg.Any<CancellationToken>())
            .Returns(new NcfAllocation(Encf.Build('E', 34, 1), null));
        _ecfReads.FindTotalByEncfAsync(TenantId, "E310000000001", Arg.Any<CancellationToken>())
            .Returns(1000m);

        var command = Command() with
        {
            Type = 34,
            Reference = new EcfReferencePayload("E310000000001", new DateOnly(2026, 1, 5)),
        };

        var result = await Sut().Execute(command);

        result.FirstError.Code.ShouldBe("Ecf.CreditNoteExceedsOriginal");
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        await _signer.DidNotReceive().SignAsync(Arg.Any<EcfDocument>(), Arg.Any<DgiiEnvironment>(), Arg.Any<CancellationToken>());
        await _ecf.DidNotReceive().AddAsync(Arg.Any<IssuedEcf>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_credit_note_against_a_paper_ncf_is_not_checked_against_any_amount()
    {
        _allocator.AllocateAsync(Arg.Any<DgiiEnvironment>(), EcfType.NotaCredito, Arg.Any<CancellationToken>())
            .Returns(new NcfAllocation(Encf.Build('E', 34, 1), null));
        // FindTotalByEncfAsync sin configurar → null: no hay nada nuestro contra qué comparar.

        var command = Command() with
        {
            Type = 34,
            Reference = new EcfReferencePayload("B1500000001", new DateOnly(2026, 1, 5)),
        };

        var result = await Sut().Execute(command);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task Rejects_an_invalid_payload_before_touching_the_pipeline()
    {
        var result = await Sut().Execute(Command() with { Type = 99 });

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
        await _allocator.DidNotReceive().AllocateAsync(Arg.Any<DgiiEnvironment>(), Arg.Any<EcfType>(), Arg.Any<CancellationToken>());
    }

    private static EcfDto StubDto(Guid id) => new(
        id, "signed", "E310000000001", 31, "Test", null, new DateOnly(2026, 1, 10),
        new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
        "aB3xZ9", "https://x", false, null, null, false, 2360m, "131880681", "Mi Cliente SRL",
        "3f8a1c9e2b7d4f60a5c8e1d2b3a4f5061c7e8d9f0a1b2c3d4e5f60718293a4b5");
}
