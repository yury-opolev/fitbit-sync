using System.Text.Json;
using FitbitSync.Application;
using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Phase 8: the agent JSON envelope is the documented contract every agent verb emits. These tests pin the
// envelope shape (camelCase, schemaVersion, ok/exitCode/data/error), the enum-as-string serialization, and
// the SyncRunOutcome -> exit-code mapping (0 ok / 3 rate-limited / 2 otherwise) that sync-once and backfill
// translate their outcomes through.
public sealed class AgentResponseTests
{
    [Fact]
    public void Success_SerializesEnvelope_WithCamelCaseAndData()
    {
        var response = AgentResponse.Success("sync-once", 0, new { itemsCompleted = 3 });

        var json = AgentJson.Serialize(response);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("command").GetString().Should().Be("sync-once");
        root.GetProperty("ok").GetBoolean().Should().BeTrue();
        root.GetProperty("exitCode").GetInt32().Should().Be(0);
        root.GetProperty("data").GetProperty("itemsCompleted").GetInt32().Should().Be(3);
        root.TryGetProperty("error", out _).Should().BeFalse();
    }

    [Fact]
    public void Failure_SerializesError_AndOmitsData()
    {
        var response = AgentResponse.Failure("backfill", 1, "usage", "--from must be on or before --to.");

        var json = AgentJson.Serialize(response);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean().Should().BeFalse();
        root.GetProperty("exitCode").GetInt32().Should().Be(1);
        root.GetProperty("error").GetProperty("code").GetString().Should().Be("usage");
        root.GetProperty("error").GetProperty("message").GetString().Should().Contain("--from");
        root.TryGetProperty("data", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(SyncRunOutcome.Completed, 0)]
    [InlineData(SyncRunOutcome.RateLimited, 3)]
    [InlineData(SyncRunOutcome.Faulted, 2)]
    [InlineData(SyncRunOutcome.Cancelled, 2)]
    public void ToExitCode_MapsOutcome(SyncRunOutcome outcome, int expected)
    {
        AgentOutcome.ToExitCode(outcome).Should().Be(expected);
    }

    [Fact]
    public void ForResult_OkTracksExitCodeZero()
    {
        // An operational failure (non-zero exit) still carries data, but ok=false reflects the exit code,
        // so ok == (exitCode == 0) holds for every agent response.
        AgentResponse.ForResult("sync-once", 0, new { }).Ok.Should().BeTrue();
        AgentResponse.ForResult("sync-once", 2, new { }).Ok.Should().BeFalse();
        AgentResponse.ForResult("backfill", 3, new { }).Ok.Should().BeFalse();
    }

    [Fact]
    public void Envelope_NeverSerializesNullDataAsExplicitKey()
    {
        // WhenWritingNull keeps the envelope tidy: a successful command with no payload omits data rather
        // than emitting null, and errors omit data entirely.
        var json = AgentJson.Serialize(AgentResponse.Success("query", 0, null));

        json.Should().NotContain("\"data\"");
    }
}
