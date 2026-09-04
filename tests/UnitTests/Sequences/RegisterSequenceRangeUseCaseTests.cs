using ErrorOr;
using NSubstitute;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Sequences.RegisterSequenceRange;
using NovaFE.Domain.Common;
using NovaFE.Domain.Sequences;
using NovaFE.UnitTests.Common;

namespace NovaFE.UnitTests.Sequences;

public class RegisterSequenceRangeUseCaseTests : UseCaseTestBase
{
    private readonly INcfSequenceRepository _sequences = Substitute.For<INcfSequenceRepository>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public RegisterSequenceRangeUseCaseTests()
    {
        _currentTenant.HasValue.Returns(true);
        _currentTenant.TenantId.Returns(Guid.CreateVersion7());
    }

    private RegisterSequenceRangeUseCase Sut() =>
        new(LoggerFactory, new RegisterSequenceRangeCommandValidator(Clock), Clock, _currentTenant, _sequences);

    private static RegisterSequenceRangeCommand Command(
        string environment = "Test",
        int type = 31,
        string series = "E",
        long from = 1,
        long to = 100,
        DateOnly? authorizedOn = null)
        => new(environment, type, series, from, to, authorizedOn);

    [Fact]
    public async Task Registers_a_new_range()
    {
        _sequences.HasActiveRangeAsync(
                Arg.Any<DgiiEnvironment>(), Arg.Any<EcfType>(), Arg.Any<char>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await Sut().Execute(Command(from: 1, to: 50));

        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotBe(Guid.Empty);
        await _sequences.Received(1).AddAsync(
            Arg.Is<NcfSequence>(s =>
                s.Type == EcfType.CreditoFiscal &&
                s.Series == 'E' &&
                s.RangeFrom == 1 &&
                s.RangeTo == 50 &&
                s.Next == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_second_active_range_for_the_same_series_with_conflict()
    {
        _sequences.HasActiveRangeAsync(
                Arg.Any<DgiiEnvironment>(), Arg.Any<EcfType>(), Arg.Any<char>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Sut().Execute(Command());

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Sequence.SeriesAlreadyActive");
        await _sequences.DidNotReceive().AddAsync(Arg.Any<NcfSequence>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_future_authorization_date_at_the_validator()
    {
        var result = await Sut().Execute(Command(authorizedOn: Clock.GetDominicanToday().AddDays(1)));

        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe(nameof(RegisterSequenceRangeCommand.AuthorizedOn));
        await _sequences.DidNotReceive().AddAsync(Arg.Any<NcfSequence>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_tenant_not_resolved_without_a_tenant()
    {
        _currentTenant.HasValue.Returns(false);
        _currentTenant.TenantId.Returns((Guid?)null);

        var result = await Sut().Execute(Command());

        result.FirstError.Code.ShouldBe("Auth.TenantNotResolved");
    }

    [Theory]
    [InlineData("Nope", 31, "E", 1, 100)]   // unknown environment
    [InlineData("Test", 99, "E", 1, 100)] // unknown type
    [InlineData("Test", 31, "P", 1, 100)] // excluded series
    [InlineData("Test", 31, "E", 0, 100)] // from < 1
    [InlineData("Test", 31, "E", 100, 1)] // to < from
    [InlineData("Cert", 31, "E", 5, 100)] // CerteCF must start at 1
    [InlineData("Cert", 31, "E", 1, 20_000_000)] // CerteCF over the 10M cap
    public async Task Rejects_invalid_input_with_validation_errors(
        string environment, int type, string series, long from, long to)
    {
        var result = await Sut().Execute(Command(environment, type, series, from, to));

        result.IsError.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Type == ErrorType.Validation);
        await _sequences.DidNotReceive().AddAsync(Arg.Any<NcfSequence>(), Arg.Any<CancellationToken>());
    }
}
