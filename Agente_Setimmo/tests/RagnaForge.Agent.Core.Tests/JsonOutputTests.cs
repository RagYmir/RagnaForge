using RagnaForge.Agent.Core.Output;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Tests for JsonOutput — ensures output is serializable and well-formed.
/// </summary>
public class JsonOutputTests
{
    [Fact]
    public void Success_IsSerializable()
    {
        var output = JsonOutput.Success("status", "All good");
        var json = output.ToJson();

        Assert.Contains("\"ok\": true", json);
        Assert.Contains("\"mode\": \"status\"", json);
        Assert.Contains("\"summary\": \"All good\"", json);
    }

    [Fact]
    public void Error_IsSerializable()
    {
        var output = JsonOutput.Error("doctor", "Config missing");
        var json = output.ToJson();

        Assert.Contains("\"ok\": false", json);
        Assert.Contains("Config missing", json);
    }

    [Fact]
    public void MultipleErrors_AreSerializable()
    {
        var output = JsonOutput.Error("doctor", ["Error 1", "Error 2"]);
        var json = output.ToJson();

        Assert.Contains("Error 1", json);
        Assert.Contains("Error 2", json);
    }

    [Fact]
    public void Output_RoundTrips()
    {
        var output = JsonOutput.Success("status");
        output.ActiveProfile = "teste";
        output.ConfigFingerprint = "abc123";
        output.Warnings.Add("some warning");

        var json = output.ToJson();
        Assert.Contains("\"activeProfile\": \"teste\"", json);
        Assert.Contains("\"configFingerprint\": \"abc123\"", json);
        Assert.Contains("some warning", json);
    }

    // --- operationId tests ---

    [Fact]
    public void Success_HasNonEmptyOperationId()
    {
        var output = JsonOutput.Success("status");

        Assert.False(string.IsNullOrWhiteSpace(output.OperationId));
        Assert.NotEqual("unknown", output.OperationId);
        Assert.Equal(12, output.OperationId.Length);
    }

    [Fact]
    public void Error_HasNonEmptyOperationId()
    {
        var output = JsonOutput.Error("doctor", "some error");

        Assert.False(string.IsNullOrWhiteSpace(output.OperationId));
        Assert.NotEqual("unknown", output.OperationId);
    }

    [Fact]
    public void OperationId_IsUniquePerInstance()
    {
        var a = JsonOutput.Success("status");
        var b = JsonOutput.Success("status");

        Assert.NotEqual(a.OperationId, b.OperationId);
    }

    [Fact]
    public void Fatal_HasCorrectShape()
    {
        var output = JsonOutput.Fatal("Unhandled exception");
        var json = output.ToJson();

        Assert.False(output.Ok);
        Assert.Equal("fatal", output.Mode);
        Assert.Equal("fix_errors", output.NextRequiredAction);
        Assert.False(output.SafeForAutomation);
        Assert.Contains("Unhandled exception", json);
        Assert.False(string.IsNullOrWhiteSpace(output.OperationId));
    }
}
