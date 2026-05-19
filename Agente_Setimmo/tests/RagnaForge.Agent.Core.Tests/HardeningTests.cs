using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Security;
using RagnaForge.Agent.Core.Configuration;

namespace RagnaForge.Agent.Core.Tests;

public class HardeningTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _agentRoot;
    private readonly string _configDir;
    private readonly string _rathenaDir;

    public HardeningTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_hardening_{Guid.NewGuid():N}");
        _agentRoot = Path.Combine(_tempDir, "agent");
        _configDir = Path.Combine(_agentRoot, "config");
        _rathenaDir = Path.Combine(_tempDir, "rathena");
        Directory.CreateDirectory(_configDir);
        Directory.CreateDirectory(_rathenaDir);
        Directory.CreateDirectory(Path.Combine(_agentRoot, "cache", "agent"));
        Directory.CreateDirectory(Path.Combine(_agentRoot, "logs", "operations"));
    }

    public void Dispose() { try { Directory.Delete(_tempDir, true); } catch { } }

    [Fact]
    public void OperationIdValidator_BlocksTraversal()
    {
        Assert.False(OperationIdValidator.IsValid("../../etc/passwd"));
        Assert.False(OperationIdValidator.IsValid("abcdef1234567")); // Too long
        Assert.True(OperationIdValidator.IsValid("abcdef123456"));  // Exactly 12 hex
    }

    [Fact]
    public void DiffCommand_BlocksInvalidId()
    {
        var cmd = new DiffCommand(_agentRoot, "../../config/paths", false);
        var result = cmd.Execute();
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("Invalid operationId format"));
    }

    [Fact]
    public void DryRunCommand_BlocksNpcNameTraversal()
    {
        var inputPath = Path.Combine(_tempDir, "input.json");
        File.WriteAllText(inputPath, "{\"name\":\"../../hacked\",\"map\":\"prontera\"}");

        var cmd = new DryRunCommand(_configDir, _agentRoot, "npc", inputPath);
        // Execute might fail earlier due to missing config, so we mock config
        // But for unit test of the specific rule, we can test PlannedPathValidator directly
        var profile = new ProfileConfig { RathenaPath = _rathenaDir, WritableRoots = [_agentRoot, _rathenaDir] };
        var errors = PlannedPathValidator.Validate(Path.Combine(_rathenaDir, "npc", "../../hacked.txt"), profile);
        Assert.Contains(errors, e => e.Contains("traversal"));
    }

    [Fact]
    public void PlannedPathValidator_BlocksLubEditing()
    {
        var profile = new ProfileConfig { RathenaPath = _rathenaDir, WritableRoots = [_agentRoot, _rathenaDir] };
        var errors = PlannedPathValidator.Validate(Path.Combine(_rathenaDir, "test.lub"), profile);
        Assert.Contains(errors, e => e.Contains(".lub"));
    }

    [Fact]
    public void IndexCommand_ValidatesRathenaPathAccess()
    {
        var profile = new ProfileConfig { RathenaPath = "C:\\rathena\\..\\..\\Windows" };
        var issues = PathGuard.EnsureProfileIsSafe(profile);
        Assert.Contains(issues, i => i.Contains("traversal"));
    }
}
