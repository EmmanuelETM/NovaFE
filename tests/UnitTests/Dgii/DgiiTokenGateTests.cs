using NovaFE.Domain.Common;
using NovaFE.Infrastructure.Dgii;

namespace NovaFE.UnitTests.Dgii;

public class DgiiTokenGateTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    [Fact]
    public async Task Same_key_serializes_access()
    {
        var gate = new DgiiTokenGate();

        var first = await gate.EnterAsync(TenantA, DgiiEnvironment.TestEcf, CancellationToken.None);

        var second = gate.EnterAsync(TenantA, DgiiEnvironment.TestEcf, CancellationToken.None);
        second.IsCompleted.ShouldBeFalse();

        first.Dispose();

        (await second).Dispose();
    }

    [Fact]
    public async Task Different_keys_do_not_block_each_other()
    {
        var gate = new DgiiTokenGate();

        using var a = await gate.EnterAsync(TenantA, DgiiEnvironment.TestEcf, CancellationToken.None);
        using var b = await gate.EnterAsync(TenantB, DgiiEnvironment.TestEcf, CancellationToken.None);
        using var aCert = await gate.EnterAsync(TenantA, DgiiEnvironment.CertEcf, CancellationToken.None);

        // No debe colgarse.
    }

    [Fact]
    public async Task Releaser_is_idempotent()
    {
        var gate = new DgiiTokenGate();
        var entry = await gate.EnterAsync(TenantA, DgiiEnvironment.TestEcf, CancellationToken.None);

        entry.Dispose();
        entry.Dispose(); // no debe lanzar ni sobre-liberar

        using var reacquired = await gate.EnterAsync(TenantA, DgiiEnvironment.TestEcf, CancellationToken.None);
    }
}
