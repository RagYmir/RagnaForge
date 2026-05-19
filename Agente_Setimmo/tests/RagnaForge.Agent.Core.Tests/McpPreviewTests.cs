using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Mcp.Prompts;
using RagnaForge.Agent.Mcp.Resources;
using RagnaForge.Agent.Mcp.Safety;
using RagnaForge.Agent.Mcp.Tools;

namespace RagnaForge.Agent.Core.Tests;

public sealed class McpPreviewTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _agentRoot;
    private readonly string _configDir;
    private readonly string _rathenaDir;
    private readonly string _patchDir;
    private readonly string _grfDir;
    private readonly McpToolRegistry _registry;

    public McpPreviewTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_mcp_test_{Guid.NewGuid():N}");
        _agentRoot = Path.Combine(_tempDir, "agent");
        _configDir = Path.Combine(_agentRoot, "config");
        _rathenaDir = Path.Combine(_tempDir, "rathena");
        _patchDir = Path.Combine(_tempDir, "patch");
        _grfDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_mcp_grfs_{Guid.NewGuid():N}");

        foreach (var d in new[] { _configDir, _rathenaDir, _patchDir, _grfDir, Path.Combine(_agentRoot, "cache", "agent") })
            Directory.CreateDirectory(d);

        WriteConfigs();
        WriteFixtures();
        _registry = new McpToolRegistry(new McpToolContext(_agentRoot));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        try { Directory.Delete(_grfDir, true); } catch { }
    }

    [Fact]
    public void McpToolPolicy_BlocksApply()
    {
        var result = _registry.Execute("ragnaforge_apply", EmptyArgs());

        Assert.False(result.Ok);
        Assert.Equal("apply", result.Mode);
        Assert.Equal("blocked_by_safety_policy", result.NextRequiredAction);
    }

    [Fact]
    public void McpToolPolicy_BlocksRollbackReal()
    {
        var result = _registry.Execute("ragnaforge_rollback_confirm", EmptyArgs());

        Assert.False(result.Ok);
        Assert.Equal("rollback", result.Mode);
        Assert.Equal("blocked_by_safety_policy", result.NextRequiredAction);
    }

    [Fact]
    public void McpToolPolicy_AllowsStatusAndDoctor()
    {
        Assert.True(McpToolPolicy.IsAllowed("ragnaforge_status"));
        Assert.True(McpToolPolicy.IsAllowed("ragnaforge_doctor"));
    }

    [Fact]
    public void McpStatus_ReturnsJsonOutput()
    {
        var result = _registry.Execute("ragnaforge_status", EmptyArgs());

        Assert.True(result.Ok);
        Assert.Equal("status", result.Mode);
        Assert.NotNull(result.ConfigFingerprint);
    }

    [Fact]
    public void McpDoctor_ReturnsJsonOutput()
    {
        var result = _registry.Execute("ragnaforge_doctor", EmptyArgs());

        Assert.True(result.Ok);
        Assert.Equal("doctor", result.Mode);
        Assert.NotNull(result.ConfigFingerprint);
    }

    [Fact]
    public void McpFindItem_ValidatesParameters()
    {
        var result = _registry.Execute("ragnaforge_find_item", EmptyArgs());

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("Provide either id or name"));
    }

    [Fact]
    public void McpDiff_RejectsInvalidOperationId()
    {
        var args = JsonDocument.Parse("""{"operationId":"../../bad","last":false}""").RootElement;
        var result = _registry.Execute("ragnaforge_diff", args);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("Invalid operationId"));
    }

    [Fact]
    public void McpRollbackDryRun_RejectsInvalidRollbackId()
    {
        var args = JsonDocument.Parse("""{"rollbackId":"../../bad"}""").RootElement;
        var result = _registry.Execute("ragnaforge_rollback_dry_run", args);

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("Invalid operationId"));
    }

    [Fact]
    public void McpDryRun_SavesInputOnlyInsideInputsDryRun()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var args = JsonDocument.Parse("""{"id":90001,"aegisName":"Mcp_Test_Item","name":"MCP Test Item"}""").RootElement;

        var result = _registry.Execute("ragnaforge_dry_run_item", args);

        Assert.True(result.Ok);
        var inputFiles = Directory.GetFiles(Path.Combine(_agentRoot, "inputs", "dry-run"), "mcp-*.json");
        Assert.Single(inputFiles);
        Assert.StartsWith(Path.Combine(_agentRoot, "inputs", "dry-run"), inputFiles[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void McpDryRun_NeverWritesOutsideAgentRoot()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var before = File.ReadAllText(Path.Combine(_rathenaDir, "db", "re", "item_db.yml"));
        var args = JsonDocument.Parse("""{"id":90002,"aegisName":"Mcp_No_Write"}""").RootElement;

        _registry.Execute("ragnaforge_dry_run_item", args);

        Assert.Equal(before, File.ReadAllText(Path.Combine(_rathenaDir, "db", "re", "item_db.yml")));
    }

    [Fact]
    public void McpResponseLimiter_TruncatesLargeResponses()
    {
        var output = new RagnaForge.Agent.Core.Output.JsonOutput
        {
            Ok = true,
            Mode = "test",
            SafeForAutomation = true,
            Summary = "large",
            Data = new { text = new string('x', 5_000) }
        };

        var limited = McpResponseLimiter.Limit(output, maxJsonChars: 500);

        Assert.False(limited.SafeForAutomation);
        Assert.Contains(limited.Warnings, w => w.Contains("truncated"));
    }

    [Fact]
    public void McpResources_ListExpectedReadOnlyResources()
    {
        var resources = new McpResourceRegistry(_registry, _agentRoot);

        var json = JsonSerializer.Serialize(resources.ListResources());

        Assert.Contains("ragnaforge://status", json);
        Assert.Contains("ragnaforge://safety", json);
        Assert.Contains("ragnaforge://reports/{id}", json);
        Assert.Contains("ragnaforge://inputs/dry-run", json);
    }

    [Fact]
    public void McpResources_BlockTraversalAndAbsolutePaths()
    {
        var resources = new McpResourceRegistry(_registry, _agentRoot);

        var traversal = JsonSerializer.Serialize(resources.ReadResource("ragnaforge://reports/../../bad"));
        var absolute = JsonSerializer.Serialize(resources.ReadResource("C:/Windows/win.ini"));

        Assert.Contains("blocked", traversal, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blocked", absolute, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void McpPrompts_ListExpectedSafePrompts()
    {
        var prompts = new McpPromptRegistry();

        var json = JsonSerializer.Serialize(prompts.ListPrompts());

        Assert.Contains("ragnaforge_validate_project", json);
        Assert.Contains("ragnaforge_prepare_dry_run", json);
        Assert.Contains("ragnaforge_mcp_safety_briefing", json);
        Assert.DoesNotContain("ragnaforge_apply", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rollback_execute", json, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement EmptyArgs() => JsonDocument.Parse("{}").RootElement;

    private void WriteConfigs()
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(_configDir, "ragnaforge.agent.json"),
            JsonSerializer.Serialize(new
            {
                agentName = "MCP Test",
                mode = "local-orchestrator",
                primaryOperators = new[] { "Test" },
                defaultOutputFormat = "json",
                cacheEnabled = true,
                logsEnabled = true
            }, opts));

        File.WriteAllText(Path.Combine(_configDir, "paths.json"),
            JsonSerializer.Serialize(new
            {
                agentRoot = _agentRoot,
                activeProfile = "test",
                profiles = new Dictionary<string, object>
                {
                    ["test"] = new
                    {
                        ragnaforgeMainProjectPath = _tempDir,
                        rathenaPath = _rathenaDir,
                        patchPath = _patchDir,
                        grfRepositoryPath = _grfDir,
                        grfEditorPath = _tempDir,
                        dbMode = "renewal",
                        writableRoots = new[] { _agentRoot, _tempDir },
                        readOnlyRoots = new[] { _grfDir }
                    }
                }
            }, opts));

        File.WriteAllText(Path.Combine(_configDir, "safety.json"),
            JsonSerializer.Serialize(new
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
            }, opts));
    }

    private void WriteFixtures()
    {
        Directory.CreateDirectory(Path.Combine(_rathenaDir, "db", "re"));
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "re", "item_db.yml"),
            "  - Id: 501\n    AegisName: Red_Potion\n    Name: Red Potion\n");
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "re", "mob_db.yml"),
            "  - Id: 1002\n    AegisName: PORING\n    Name: Poring\n");
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "map_index.txt"), "prontera 1\n");
        Directory.CreateDirectory(Path.Combine(_rathenaDir, "npc"));
        File.WriteAllText(Path.Combine(_rathenaDir, "npc", "test.txt"),
            "prontera,1,1,4\tscript\tTest NPC\t4_F_KAFRA1,{\n}\n");
    }
}
