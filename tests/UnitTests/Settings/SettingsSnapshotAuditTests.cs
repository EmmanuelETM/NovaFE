using NovaFE.Infrastructure.Settings;

namespace NovaFE.UnitTests.Settings;

public class SettingsSnapshotAuditTests
{
    [Fact]
    public void A_clean_snapshot_has_no_issues()
    {
        var overrides = new Dictionary<string, string> { ["platform.maintenance_mode"] = "true" };

        SettingsSnapshotAudit.Inspect(overrides).ShouldBeEmpty();
    }

    [Fact]
    public void An_orphan_key_is_reported()
    {
        var overrides = new Dictionary<string, string> { ["platform.was_removed"] = "true" };

        var issues = SettingsSnapshotAudit.Inspect(overrides);

        issues.Count.ShouldBe(1);
        issues[0].ShouldContain("platform.was_removed");
        issues[0].ShouldContain("huérfano");
    }

    [Fact]
    public void A_value_that_no_longer_parses_is_reported()
    {
        var overrides = new Dictionary<string, string> { ["platform.maintenance_mode"] = "quizás" };

        var issues = SettingsSnapshotAudit.Inspect(overrides);

        issues.Count.ShouldBe(1);
        issues[0].ShouldContain("no parsea");
    }
}
