using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Mcp.Safety;
using RagnaForge.Agent.Mcp.Tools;

namespace RagnaForge.Agent.Core.Tests;

public sealed class OperationalUxTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _agentRoot;
    private readonly string _configDir;
    private readonly string _projectDir;
    private readonly string _alternateProjectDir;
    private readonly string _invalidProjectDir;
    private readonly string _rathenaDir;
    private readonly string _patchDir;
    private readonly string _alternatePatchDir;
    private readonly string _grfDir;
    private readonly string _badGrfDir;

    public OperationalUxTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_operational_ux_{Guid.NewGuid():N}");
        _agentRoot = Path.Combine(_tempDir, "agent");
        _configDir = Path.Combine(_agentRoot, "config");
        _projectDir = Path.Combine(_tempDir, "project");
        _alternateProjectDir = Path.Combine(_tempDir, "project-alt");
        _invalidProjectDir = Path.Combine(_tempDir, "project-invalid");
        _rathenaDir = Path.Combine(_tempDir, "rathena");
        _patchDir = Path.Combine(_tempDir, "patch");
        _alternatePatchDir = Path.Combine(_tempDir, "patch-alt");
        _grfDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_operational_grf_{Guid.NewGuid():N}");
        _badGrfDir = Path.Combine(_tempDir, "bad-grf");

        Directory.CreateDirectory(_configDir);
        Directory.CreateDirectory(Path.Combine(_agentRoot, "cache", "agent"));
        Directory.CreateDirectory(_grfDir);
        Directory.CreateDirectory(_badGrfDir);

        CreateProject(_projectDir, includeFrontend: true);
        CreateProject(_alternateProjectDir, includeFrontend: true);
        CreateProject(_invalidProjectDir, includeFrontend: false);
        CreateRathenaFixture();
        Directory.CreateDirectory(Path.Combine(_patchDir, "data"));
        Directory.CreateDirectory(_alternatePatchDir);

        WriteAgentConfig();
        WriteSafetyConfig();
        WritePathsConfig();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        try { Directory.Delete(_grfDir, true); } catch { }
    }

    [Fact]
    public void Baseline_ReturnsCompositeSections_AndKeepsReadOnlyWorkSafe()
    {
        var result = new BaselineCommand(_configDir, _agentRoot).Execute();

        Assert.True(result.Ok);
        var data = ToElement(result.Data);
        Assert.True(data.TryGetProperty("status", out _));
        Assert.True(data.TryGetProperty("doctor", out _));
        Assert.True(data.TryGetProperty("scan", out _));
        Assert.True(data.TryGetProperty("index", out _));
        Assert.True(data.TryGetProperty("validate", out var validate));
        Assert.True(validate.GetProperty("safeForReadOnlyWork").GetBoolean());
        Assert.False(validate.GetProperty("safeForApply").GetBoolean());
    }

    [Fact]
    public void Baseline_FailsWhenDoctorFails()
    {
        WritePathsConfig(readOnlyRoots: []);

        var result = new BaselineCommand(_configDir, _agentRoot).Execute();

        Assert.False(result.Ok);
        Assert.Contains("Doctor checks failed", result.Summary);
    }

    [Fact]
    public void Health_ReturnsCountsAndSafetyFlags()
    {
        new ScanCommand(_configDir, _agentRoot).Execute();
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();

        var result = new HealthCommand(_configDir, _agentRoot).Execute();

        Assert.True(result.Ok);
        var data = ToElement(result.Data);
        Assert.True(data.GetProperty("project").GetProperty("cacheTrusted").GetBoolean());
        Assert.True(data.GetProperty("entities").GetProperty("trustedCounts").GetBoolean());
        Assert.True(data.GetProperty("safety").GetProperty("applyBlocked").GetBoolean());
        Assert.True(data.GetProperty("safety").GetProperty("rollbackRealBlocked").GetBoolean());
        Assert.Equal(2, data.GetProperty("entities").GetProperty("items").GetInt32());
    }

    [Fact]
    public void Health_MarksCountsUntrusted_WhenFingerprintChanges()
    {
        new ScanCommand(_configDir, _agentRoot).Execute();
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        WritePathsConfig(patchPath: _alternatePatchDir);

        var result = new HealthCommand(_configDir, _agentRoot).Execute();

        Assert.True(result.Ok);
        var data = ToElement(result.Data);
        Assert.False(data.GetProperty("project").GetProperty("cacheTrusted").GetBoolean());
        Assert.Equal("configFingerprint_mismatch", data.GetProperty("project").GetProperty("cacheStaleReason").GetString());
        Assert.False(data.GetProperty("entities").GetProperty("trustedCounts").GetBoolean());
        Assert.Equal("configFingerprint_mismatch", data.GetProperty("entities").GetProperty("cacheStaleReason").GetString());
    }

    [Fact]
    public void ConfigSet_RejectsInvalidProjectPath()
    {
        var result = new ConfigCommand(
            _configDir,
            _agentRoot,
            "set",
            "ragnaforgeMainProjectPath",
            _invalidProjectDir).Execute();

        Assert.False(result.Ok);
        Assert.Equal("choose_valid_path", result.NextRequiredAction);
    }

    [Fact]
    public void ConfigSet_AcceptsValidProjectPath_AndReturnsFingerprints()
    {
        var result = new ConfigCommand(
            _configDir,
            _agentRoot,
            "set",
            "ragnaforgeMainProjectPath",
            _alternateProjectDir).Execute();

        Assert.True(result.Ok);
        Assert.Equal("run_baseline", result.NextRequiredAction);
        var data = ToElement(result.Data);
        Assert.NotEqual(data.GetProperty("oldFingerprint").GetString(), data.GetProperty("newFingerprint").GetString());
    }

    [Fact]
    public void ConfigSet_RejectsUnknownKey()
    {
        var result = new ConfigCommand(_configDir, _agentRoot, "set", "unknownKey", _projectDir).Execute();

        Assert.False(result.Ok);
        Assert.Contains("not allowed", result.Errors.Single());
    }

    [Fact]
    public void ConfigSet_RejectsGrfRepositoryInsideWritableRoots()
    {
        var result = new ConfigCommand(
            _configDir,
            _agentRoot,
            "set",
            "grfRepositoryPath",
            _badGrfDir).Execute();

        Assert.False(result.Ok);
        Assert.Equal("choose_valid_path", result.NextRequiredAction);
    }

    [Fact]
    public void Validate_ExternalDataIssues_BlockApplyButNotReadOnlyAudit()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();

        var result = new ValidateCommand(_configDir, _agentRoot).Execute();

        Assert.True(result.Ok);
        var data = ToElement(result.Data);
        Assert.True(data.GetProperty("safeForReadOnlyWork").GetBoolean());
        Assert.False(data.GetProperty("safeForApply").GetBoolean());
        var issue = data.GetProperty("issues")[0];
        Assert.Equal("external-data", issue.GetProperty("scope").GetString());
        Assert.Contains("apply", issue.GetProperty("blockingFor").EnumerateArray().Select(x => x.GetString()));
        Assert.DoesNotContain("read-only-audit", issue.GetProperty("blockingFor").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void Validate_SecurityCriticalIssue_BlocksEverything()
    {
        var issue = ValidationOperationalClassifier.CreateSecurityIssue(
            "SECURITY_AGENT_RUNTIME",
            "Critical security issue.",
            "Stop and investigate.");

        ValidationOperationalClassifier.ApplyClassification([issue]);
        var summary = ValidationOperationalClassifier.BuildSummary([issue]);

        Assert.Equal("security", issue.Scope);
        Assert.False(summary.SafeForReadOnlyWork);
        Assert.False(summary.SafeForDryRun);
        Assert.False(summary.SafeForApply);
    }

    [Fact]
    public void Validate_StaleCache_IsClassifiedAsCache()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        WritePathsConfig(patchPath: _alternatePatchDir);

        var result = new ValidateCommand(_configDir, _agentRoot).Execute();

        Assert.False(result.Ok);
        var data = ToElement(result.Data);
        Assert.Equal("cache", data.GetProperty("issues")[0].GetProperty("scope").GetString());
    }

    [Fact]
    public void McpPolicy_AllowsBaselineAndHealth_AndStillBlocksApply()
    {
        Assert.True(McpToolPolicy.IsAllowed("ragnaforge_baseline"));
        Assert.True(McpToolPolicy.IsAllowed("ragnaforge_health"));
        Assert.True(McpToolPolicy.IsBlocked("ragnaforge_apply"));
        Assert.True(McpToolPolicy.IsBlocked("ragnaforge_rollback_confirm"));
    }

    [Fact]
    public void McpRegistry_ExecutesHealthTool()
    {
        new ScanCommand(_configDir, _agentRoot).Execute();
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();

        var registry = new McpToolRegistry(new McpToolContext(_agentRoot));
        var result = registry.Execute("ragnaforge_health", JsonDocument.Parse("{}").RootElement);

        Assert.True(result.Ok);
        Assert.Equal("health", result.Mode);
    }

    private void WriteAgentConfig()
    {
        File.WriteAllText(
            Path.Combine(_configDir, "ragnaforge.agent.json"),
            JsonSerializer.Serialize(
                new
                {
                    agentName = "Operational UX Test",
                    mode = "local-orchestrator",
                    primaryOperators = new[] { "Codex" },
                    defaultOutputFormat = "json",
                    cacheEnabled = true,
                    logsEnabled = true
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private void WriteSafetyConfig()
    {
        File.WriteAllText(
            Path.Combine(_configDir, "safety.json"),
            JsonSerializer.Serialize(
                new
                {
                    requireDryRunBeforeApply = true,
                    requireDiffBeforeApply = true,
                    requireValidationBeforeApply = true,
                    requireExplicitConfirmation = true,
                    backupBeforeApply = true,
                    blockOriginalGrfWrite = true,
                    blockLubEditing = true,
                    invalidateCacheOnPathChange = true,
                    cacheMustMatchActiveProfile = true
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private void WritePathsConfig(string? mainProjectPath = null, string? patchPath = null, string[]? readOnlyRoots = null)
    {
        File.WriteAllText(
            Path.Combine(_configDir, "paths.json"),
            JsonSerializer.Serialize(
                new
                {
                    agentRoot = _agentRoot,
                    activeProfile = "test",
                    profiles = new Dictionary<string, object>
                    {
                        ["test"] = new
                        {
                            ragnaforgeMainProjectPath = mainProjectPath ?? _projectDir,
                            rathenaPath = _rathenaDir,
                            patchPath = patchPath ?? _patchDir,
                            grfRepositoryPath = _grfDir,
                            grfEditorPath = _tempDir,
                            dbMode = "renewal",
                            writableRoots = new[] { _tempDir },
                            readOnlyRoots = readOnlyRoots ?? new[] { _grfDir }
                        }
                    }
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private void CreateProject(string path, bool includeFrontend)
    {
        Directory.CreateDirectory(Path.Combine(path, "backend"));
        if (includeFrontend)
        {
            Directory.CreateDirectory(Path.Combine(path, "frontend"));
        }

        File.WriteAllText(Path.Combine(path, "RagnaForge.slnx"), "<Solution />");
    }

    private void CreateRathenaFixture()
    {
        Directory.CreateDirectory(Path.Combine(_rathenaDir, "db", "re"));
        Directory.CreateDirectory(Path.Combine(_rathenaDir, "npc"));
        File.WriteAllText(
            Path.Combine(_rathenaDir, "db", "re", "item_db.yml"),
            """
              - Id: 501
                AegisName: Red_Potion
                Name: Red Potion
              - Id: 501
                AegisName: Red_Potion_Duplicate
                Name: Red Potion Duplicate
            """);
        File.WriteAllText(
            Path.Combine(_rathenaDir, "db", "re", "mob_db.yml"),
            """
              - Id: 1002
                AegisName: PORING
                Name: Poring
            """);
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "map_index.txt"), "prontera 1\n");
        File.WriteAllText(
            Path.Combine(_rathenaDir, "npc", "test.txt"),
            "prontera,1,1,4\tscript\tTest NPC\t4_F_KAFRA1,{\n}\n");
    }

    private static JsonElement ToElement(object? value) =>
        value is null ? JsonDocument.Parse("{}").RootElement : JsonSerializer.SerializeToElement(value);
}
