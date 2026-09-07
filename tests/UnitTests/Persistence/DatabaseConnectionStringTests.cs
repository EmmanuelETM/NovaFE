using Npgsql;
using NovaFE.Infrastructure.Persistence;

namespace NovaFE.UnitTests.Persistence;

public class DatabaseConnectionStringTests
{
    private const string Base = "Host=db.example.com;Database=novafe;Username=novafe_app;Password=secret";

    private static NpgsqlConnectionStringBuilder Normalize(string raw, DatabaseOptions? options = null)
        => new(DatabaseConnectionString.Normalize(raw, options ?? new DatabaseOptions()));

    [Fact]
    public void Applies_the_defaults_when_the_raw_string_has_none()
    {
        var b = Normalize(Base, new DatabaseOptions { MaxPoolSize = 20, CommandTimeoutSeconds = 30, ConnectTimeoutSeconds = 15 });

        b.Pooling.ShouldBeTrue();
        b.MaxPoolSize.ShouldBe(20);
        b.Timeout.ShouldBe(15);
        b.CommandTimeout.ShouldBe(30);
        b.KeepAlive.ShouldBe(30);
        b.SslMode.ShouldBe(SslMode.Prefer); // RequireSsl defaults to false (local/tests have no TLS)
        b.NoResetOnClose.ShouldBeFalse();
    }

    [Fact]
    public void RequireSsl_true_on_a_plain_postgres_provider_requires_tls()
    {
        var b = Normalize(Base, new DatabaseOptions { RequireSsl = true });

        b.SslMode.ShouldBe(SslMode.Require);
    }

    [Fact]
    public void Neon_provider_raises_the_connect_timeout_floor_and_verifies_tls()
    {
        var b = Normalize(Base, new DatabaseOptions { Provider = DatabaseOptions.ProviderNeon, ConnectTimeoutSeconds = 15 });

        b.Timeout.ShouldBe(30);
        b.SslMode.ShouldBe(SslMode.VerifyFull);
    }

    [Fact]
    public void Neon_provider_keeps_a_higher_explicit_connect_timeout()
    {
        var b = Normalize(Base, new DatabaseOptions { Provider = DatabaseOptions.ProviderNeon, ConnectTimeoutSeconds = 45 });

        b.Timeout.ShouldBe(45);
    }

    [Fact]
    public void Respects_values_already_present_in_the_raw_string()
    {
        var raw = Base + ";Maximum Pool Size=99;Timeout=7;SSL Mode=Disable;Command Timeout=120;Keepalive=5;No Reset On Close=true";

        var b = Normalize(raw, new DatabaseOptions { Provider = DatabaseOptions.ProviderNeon, MaxPoolSize = 20 });

        b.MaxPoolSize.ShouldBe(99);
        b.Timeout.ShouldBe(7);
        b.SslMode.ShouldBe(SslMode.Disable);
        b.CommandTimeout.ShouldBe(120);
        b.KeepAlive.ShouldBe(5);
        b.NoResetOnClose.ShouldBeTrue();
    }

    [Fact]
    public void Recognizes_the_no_space_spelling_of_a_key()
    {
        var b = Normalize(Base + ";MaxPoolSize=50;CommandTimeout=90", new DatabaseOptions { MaxPoolSize = 20, CommandTimeoutSeconds = 30 });

        b.MaxPoolSize.ShouldBe(50);
        b.CommandTimeout.ShouldBe(90);
    }

    [Fact]
    public void Is_idempotent()
    {
        var options = new DatabaseOptions { Provider = DatabaseOptions.ProviderNeon };
        var once = DatabaseConnectionString.Normalize(Base, options);
        var twice = DatabaseConnectionString.Normalize(once, options);

        twice.ShouldBe(once);
    }

    [Fact]
    public void RequireSsl_false_falls_back_to_prefer()
    {
        var b = Normalize(Base, new DatabaseOptions { RequireSsl = false });

        b.SslMode.ShouldBe(SslMode.Prefer);
    }

    [Theory]
    [InlineData("Host=ep-cool-name-123456-pooler.us-east-2.aws.neon.tech;Database=d;Username=u;Password=p", true)]
    [InlineData("Host=ep-cool-name-123456.us-east-2.aws.neon.tech;Database=d;Username=u;Password=p", false)]
    [InlineData("Host=my-pgbouncer.internal;Database=d;Username=u;Password=p", true)]
    [InlineData("", false)]
    public void Detects_a_transaction_pooler_host(string connectionString, bool expected)
        => DatabaseConnectionString.LooksLikeTransactionPooler(connectionString).ShouldBe(expected);
}
