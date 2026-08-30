using NovaFE.Domain.Common;

namespace NovaFE.UnitTests.Sequences;

public class EcfTypeTests
{
    [Fact]
    public void There_are_exactly_ten_types()
        => EcfType.GetAll().Count().ShouldBe(10);

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(34)]
    [InlineData(41)]
    [InlineData(43)]
    [InlineData(44)]
    [InlineData(45)]
    [InlineData(46)]
    [InlineData(47)]
    public void Every_known_code_resolves(int code)
        => EcfType.FromCodeOrDefault(code).ShouldNotBeNull();

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(42)]
    [InlineData(48)]
    public void Unknown_codes_return_null(int code)
        => EcfType.FromCodeOrDefault(code).ShouldBeNull();

    [Fact]
    public void Only_consumo_and_nota_credito_have_no_sequence_expiry()
    {
        EcfType.Consumo.HasSequenceExpiry.ShouldBeFalse();
        EcfType.NotaCredito.HasSequenceExpiry.ShouldBeFalse();

        foreach (var type in EcfType.GetAll().Where(t => t.Id is not 32 and not 34))
            type.HasSequenceExpiry.ShouldBeTrue();
    }
}
