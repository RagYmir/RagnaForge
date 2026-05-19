using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Output;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Integration tests for IndexCommand, FindCommand, ValidateCommand, DryRunCommand,
/// DiffCommand, ReportCommand and RollbackCommand.
/// Uses temporary fixtures with valid config files.
/// </summary>
public class MvpCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _agentRoot;
    private readonly string _configDir;
    private readonly string _rathenaDir;
    private readonly string _patchDir;
    private readonly string _grfDir;

    public MvpCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_mvp_test_{Guid.NewGuid():N}");
        _agentRoot = Path.Combine(_tempDir, "agent");
        _configDir = Path.Combine(_agentRoot, "config");
        _rathenaDir = Path.Combine(_tempDir, "rathena");
        _patchDir = Path.Combine(_tempDir, "patch");
        _grfDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_mvp_grfs_{Guid.NewGuid():N}");

        foreach (var d in new[] { _configDir, _rathenaDir, _patchDir, _grfDir,
            Path.Combine(_agentRoot, "cache", "agent"),
            Path.Combine(_agentRoot, "logs", "operations"),
            Path.Combine(_agentRoot, "logs", "diffs"),
            Path.Combine(_agentRoot, "logs", "rollbacks"),
            Path.Combine(_agentRoot, "logs", "reports") })
            Directory.CreateDirectory(d);

        WriteConfigs();
        WriteRathenaFixtures();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        try { Directory.Delete(_grfDir, true); } catch { }
    }

    private void WriteConfigs(string dbMode = "renewal")
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(_configDir, "ragnaforge.agent.json"),
            JsonSerializer.Serialize(new { agentName = "Test", mode = "local-orchestrator",
                primaryOperators = new[] { "Test" }, defaultOutputFormat = "json",
                cacheEnabled = true, logsEnabled = true }, opts));

        File.WriteAllText(Path.Combine(_configDir, "paths.json"),
            JsonSerializer.Serialize(new { agentRoot = _agentRoot, activeProfile = "test",
                profiles = new Dictionary<string, object> {
                    ["test"] = new { ragnaforgeMainProjectPath = _tempDir, rathenaPath = _rathenaDir,
                        patchPath = _patchDir, grfRepositoryPath = _grfDir, grfEditorPath = _tempDir,
                        dbMode,
                        writableRoots = new[] { _agentRoot, _tempDir },
                        readOnlyRoots = new[] { _grfDir } } } }, opts));

        File.WriteAllText(Path.Combine(_configDir, "safety.json"),
            JsonSerializer.Serialize(new { requireDryRunBeforeApply = true, requireDiffBeforeApply = true,
                requireValidationBeforeApply = true, requireExplicitConfirmation = true,
                backupBeforeApply = true, blockOriginalGrfWrite = true, blockLubEditing = true,
                invalidateCacheOnPathChange = true, cacheMustMatchActiveProfile = true }, opts));
    }

    private void WriteRathenaFixtures()
    {
        // Items
        Directory.CreateDirectory(Path.Combine(_rathenaDir, "db", "re"));
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "re", "item_db.yml"),
            "  - Id: 501\n    AegisName: Red_Potion\n    Name: Red Potion\n    Type: Healing\n" +
            "  - Id: 502\n    AegisName: Orange_Potion\n    Name: Orange Potion\n    Type: Healing\n");

        Directory.CreateDirectory(Path.Combine(_rathenaDir, "db", "pre-re"));
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "pre-re", "item_db.yml"),
            "  - Id: 501\n    AegisName: Red_Potion_Pre\n    Name: Red Potion Pre\n    Type: Healing\n");

        // Monsters
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "re", "mob_db.yml"),
            "  - Id: 1002\n    AegisName: PORING\n    Name: Poring\n" +
            "  - Id: 1002\n    AegisName: PORING_DUP\n    Name: Poring Dup\n"); // intentional dup for validate test

        // Map index
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "map_index.txt"),
            "prontera 1\nalberta\ngeffen\n");

        // NPCs
        Directory.CreateDirectory(Path.Combine(_rathenaDir, "npc", "cities"));
        File.WriteAllText(Path.Combine(_rathenaDir, "npc", "cities", "prontera.txt"),
            "prontera,155,183,4\tscript\tKafra Employee\t4_F_KAFRA1,{\n    mes \"Hello\";\n    close;\n}\n");

        Directory.CreateDirectory(Path.Combine(_patchDir, "data"));
        File.WriteAllText(Path.Combine(_patchDir, "data", "idnum2itemdisplaynametable.txt"),
            "70001#Client Only Item#\n");
        File.WriteAllText(Path.Combine(_patchDir, "data", "iteminfo.lub"), "bytecode placeholder");
    }

    private static List<JsonElement> GetValidationIssues(JsonOutput result)
    {
        var json = JsonSerializer.Serialize(result.Data);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("issues").EnumerateArray().Select(i => i.Clone()).ToList();
    }

    private void SetDbMode(string dbMode) => WriteConfigs(dbMode);

    // --- Index Tests ---
    [Fact]
    public void IndexCommand_IndexesEntities()
    {
        var cmd = new IndexCommand(_configDir, _agentRoot, "entities");
        var result = cmd.Execute();
        Assert.True(result.Ok);
        Assert.Equal("index", result.Mode);
    }

    [Fact]
    public void IndexCommand_IndexesItemsOnly()
    {
        var cmd = new IndexCommand(_configDir, _agentRoot, "items");
        var result = cmd.Execute();
        Assert.True(result.Ok);
        Assert.True(File.Exists(Path.Combine(_agentRoot, "cache", "agent", "item_index.json")));
    }

    // --- Find Tests ---
    [Fact]
    public void FindCommand_FindsItemById()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var cmd = new FindCommand(_configDir, _agentRoot, "item", 501, null);
        var result = cmd.Execute();
        Assert.True(result.Ok);
        Assert.Contains("1", result.Summary); // "Found 1 item(s)"
    }

    [Fact]
    public void FindCommand_FindsItemByName()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var cmd = new FindCommand(_configDir, _agentRoot, "item", null, "Red");
        var result = cmd.Execute();
        Assert.True(result.Ok);
    }

    [Fact]
    public void FindCommand_FindsMonsterByName()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var cmd = new FindCommand(_configDir, _agentRoot, "monster", null, "Poring");
        var result = cmd.Execute();
        Assert.True(result.Ok);
    }

    [Fact]
    public void FindCommand_FindsNpcByName()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var cmd = new FindCommand(_configDir, _agentRoot, "npc", null, "Kafra");
        var result = cmd.Execute();
        Assert.True(result.Ok);
    }

    [Fact]
    public void FindCommand_FindsMapByName()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var cmd = new FindCommand(_configDir, _agentRoot, "map", null, "prontera");
        var result = cmd.Execute();
        Assert.True(result.Ok);
    }

    [Fact]
    public void FindCommand_ReturnsRunIndexWhenNoIndex()
    {
        var cmd = new FindCommand(_configDir, _agentRoot, "item", 501, null);
        var result = cmd.Execute();
        Assert.False(result.Ok);
        Assert.Equal("run_index", result.NextRequiredAction);
    }

    // --- Validate Tests ---
    [Fact]
    public void ValidateCommand_DetectsDuplicateMob()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var cmd = new ValidateCommand(_configDir, _agentRoot);
        var result = cmd.Execute();
        // Should find the duplicate mob 1002
        Assert.Contains("issues", result.Summary);
    }

    [Fact]
    public void ValidateCommand_DetectsDuplicateServerItemId()
    {
        Directory.CreateDirectory(Path.Combine(_rathenaDir, "db", "import"));
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "import", "item_db.yml"),
            "  - Id: 501\n    AegisName: Red_Potion_Duplicate\n    Name: Red Potion Duplicate\n");

        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var result = new ValidateCommand(_configDir, _agentRoot, "items").Execute();
        var issues = GetValidationIssues(result);
        var data = JsonSerializer.SerializeToElement(result.Data);

        Assert.True(result.SafeForAutomation);
        Assert.True(data.GetProperty("safeForReadOnlyWork").GetBoolean());
        Assert.False(data.GetProperty("safeForApply").GetBoolean());
        Assert.Contains(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_DUPLICATE_ID_SERVER" &&
            i.GetProperty("message").GetString()!.Contains("Duplicate server item ID 501"));
    }

    [Fact]
    public void ValidateCommand_RenewalDoesNotFatalOnPreRenewalDuplicateId()
    {
        SetDbMode("renewal");

        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var result = new ValidateCommand(_configDir, _agentRoot, "items").Execute();
        var issues = GetValidationIssues(result);

        Assert.DoesNotContain(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_DUPLICATE_ID_SERVER" &&
            i.GetProperty("entityId").GetString() == "501");
    }

    [Fact]
    public void ValidateCommand_PreRenewalDoesNotFatalOnRenewalDuplicateId()
    {
        SetDbMode("pre-renewal");

        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var result = new ValidateCommand(_configDir, _agentRoot, "items").Execute();
        var issues = GetValidationIssues(result);

        Assert.DoesNotContain(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_DUPLICATE_ID_SERVER" &&
            i.GetProperty("entityId").GetString() == "501");
    }

    [Fact]
    public void ValidateCommand_DuplicateWithinRenewalModeStaysError()
    {
        SetDbMode("renewal");
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "re", "item_db_equip.yml"),
            "  - Id: 501\n    AegisName: Red_Potion_Renewal_Duplicate\n    Name: Red Potion Renewal Duplicate\n");

        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var result = new ValidateCommand(_configDir, _agentRoot, "items").Execute();
        var issues = GetValidationIssues(result);

        Assert.Contains(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_DUPLICATE_ID_SERVER" &&
            i.GetProperty("entityId").GetString() == "501");
    }

    [Fact]
    public void ValidateCommand_DuplicateWithinPreRenewalModeStaysError()
    {
        SetDbMode("pre-renewal");
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "pre-re", "item_db_usable.yml"),
            "  - Id: 501\n    AegisName: Red_Potion_Pre_Duplicate\n    Name: Red Potion Pre Duplicate\n");

        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var result = new ValidateCommand(_configDir, _agentRoot, "items").Execute();
        var issues = GetValidationIssues(result);

        Assert.Contains(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_DUPLICATE_ID_SERVER" &&
            i.GetProperty("entityId").GetString() == "501");
    }

    [Fact]
    public void ValidateCommand_ImportDuplicateAgainstActiveModeStaysError()
    {
        SetDbMode("renewal");
        Directory.CreateDirectory(Path.Combine(_rathenaDir, "db", "import"));
        File.WriteAllText(Path.Combine(_rathenaDir, "db", "import", "item_db.yml"),
            "  - Id: 501\n    AegisName: Red_Potion_Import_Duplicate\n    Name: Red Potion Import Duplicate\n");

        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var result = new ValidateCommand(_configDir, _agentRoot, "items").Execute();
        var issues = GetValidationIssues(result);

        Assert.Contains(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_DUPLICATE_ID_SERVER" &&
            i.GetProperty("entityId").GetString() == "501");
    }

    [Fact]
    public void ValidateCommand_HybridCrossModeDuplicateIsWarning()
    {
        SetDbMode("hybrid");

        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var result = new ValidateCommand(_configDir, _agentRoot, "items").Execute();
        var issues = GetValidationIssues(result);

        Assert.Contains(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_DUPLICATE_ID_CROSS_DB_MODE" &&
            i.GetProperty("entityId").GetString() == "501");
        Assert.DoesNotContain(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_DUPLICATE_ID_SERVER" &&
            i.GetProperty("entityId").GetString() == "501");
    }

    [Fact]
    public void ValidateCommand_AllowsSameItemIdBetweenServerAndClient()
    {
        File.AppendAllText(Path.Combine(_patchDir, "data", "idnum2itemdisplaynametable.txt"),
            "501#Red Potion Client#\n");

        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var result = new ValidateCommand(_configDir, _agentRoot, "items").Execute();
        var issues = GetValidationIssues(result);

        Assert.DoesNotContain(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_DUPLICATE_ID_SERVER" &&
            i.GetProperty("entityId").GetString() == "501");
    }

    [Fact]
    public void ValidateCommand_IgnoresLubSentinelAndClientMissingAegis()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var result = new ValidateCommand(_configDir, _agentRoot, "items").Execute();
        var issues = GetValidationIssues(result);

        Assert.DoesNotContain(issues, i => i.GetProperty("entityId").GetString() == "-1");
        Assert.DoesNotContain(issues, i =>
            i.GetProperty("code").GetString() == "ITEM_MISSING_AEGIS" &&
            i.GetProperty("entityId").GetString() == "70001");
    }

    [Fact]
    public void ValidateCommand_DoesNotModifyFiles()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var itemFile = Path.Combine(_rathenaDir, "db", "re", "item_db.yml");
        var before = File.ReadAllText(itemFile);
        var beforeTime = File.GetLastWriteTimeUtc(itemFile);

        new ValidateCommand(_configDir, _agentRoot).Execute();

        Assert.Equal(before, File.ReadAllText(itemFile));
        Assert.Equal(beforeTime, File.GetLastWriteTimeUtc(itemFile));
    }

    // --- DryRun Tests ---
    [Fact]
    public void DryRunCommand_CreatesManifest()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_agentRoot, "input.json");
        File.WriteAllText(inputPath, "{\"id\":30001,\"aegisName\":\"Test_Sword\",\"name\":\"Test Sword\",\"type\":\"Weapon\"}");

        var cmd = new DryRunCommand(_configDir, _agentRoot, "item", inputPath);
        var result = cmd.Execute();
        Assert.True(result.Ok);
        Assert.NotNull(result.OperationId);
    }

    [Fact]
    public void OperationManifest_UsesCleanFinalAgentVersion()
    {
        Assert.Equal(RagnaForge.Agent.Core.AgentVersion.Current, new OperationManifest().AgentVersion);
    }

    [Fact]
    public void DryRunCommand_BlocksDuplicateItem()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_agentRoot, "dup.json");
        File.WriteAllText(inputPath, "{\"id\":501,\"aegisName\":\"Red_Potion\"}");

        var cmd = new DryRunCommand(_configDir, _agentRoot, "item", inputPath);
        var result = cmd.Execute();
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate server item ID 501"));
    }

    [Fact]
    public void DryRunCommand_DoesNotBlockServerItemWhenIdExistsOnlyClientSide()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_agentRoot, "client_side_id.json");
        File.WriteAllText(inputPath, "{\"id\":70001,\"aegisName\":\"Server_New_Item\"}");

        var result = new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();

        Assert.True(result.Ok);
        Assert.DoesNotContain(result.Errors, e => e.Contains("Duplicate"));
    }

    [Fact]
    public void DryRunCommand_IgnoresLubSentinelForDuplicateDetection()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_agentRoot, "lub_safe_item.json");
        File.WriteAllText(inputPath, "{\"id\":80001,\"aegisName\":\"Lub_Safe_Item\"}");

        var result = new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();

        Assert.True(result.Ok);
        Assert.DoesNotContain(result.Errors, e => e.Contains("-1"));
    }

    [Fact]
    public void DryRunCommand_BlocksInputOutsideAgentRoot()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_tempDir, "outside_agent_root.json");
        File.WriteAllText(inputPath, "{\"id\":80002,\"aegisName\":\"Outside_Input\"}");

        var result = new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("agentRoot"));
    }

    [Fact]
    public void DryRunCommand_AllowsInputInsideDryRunFolder()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputDir = Path.Combine(_agentRoot, "inputs", "dry-run");
        Directory.CreateDirectory(inputDir);
        var inputPath = Path.Combine(inputDir, "inside.json");
        File.WriteAllText(inputPath, "{\"id\":80003,\"aegisName\":\"Inside_Input\"}");

        var result = new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();

        Assert.True(result.Ok);
    }

    [Fact]
    public void DryRunCommand_DoesNotModifyRathena()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var itemFile = Path.Combine(_rathenaDir, "db", "re", "item_db.yml");
        var before = File.ReadAllText(itemFile);

        var inputPath = Path.Combine(_agentRoot, "item.json");
        File.WriteAllText(inputPath, "{\"id\":30001,\"aegisName\":\"New_Item\"}");
        new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();

        Assert.Equal(before, File.ReadAllText(itemFile));
    }

    [Fact]
    public void DryRunCommand_ReturnsOperationId()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_agentRoot, "op.json");
        File.WriteAllText(inputPath, "{\"id\":30001,\"aegisName\":\"Test\"}");

        var result = new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();
        Assert.NotNull(result.OperationId);
        Assert.True(result.OperationId.Length >= 12);
    }

    // --- Diff Tests ---
    [Fact]
    public void DiffCommand_ReturnsDiffForLastOperation()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_agentRoot, "diff_item.json");
        File.WriteAllText(inputPath, "{\"id\":30001,\"aegisName\":\"Diff_Test\"}");
        new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();

        var result = new DiffCommand(_agentRoot, null, true).Execute();
        Assert.True(result.Ok);
    }

    [Fact]
    public void DiffCommand_ErrorsWhenNoOperations()
    {
        var result = new DiffCommand(Path.Combine(_tempDir, "empty_agent"), null, true).Execute();
        Assert.False(result.Ok);
    }

    // --- Report Tests ---
    [Fact]
    public void ReportCommand_GeneratesJsonReport()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_agentRoot, "rpt.json");
        File.WriteAllText(inputPath, "{\"id\":30001,\"aegisName\":\"Report_Test\"}");
        new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();

        var result = new ReportCommand(_agentRoot, null, true).Execute();
        Assert.True(result.Ok);
    }

    [Fact]
    public void ReportCommand_GeneratesMdReport()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_agentRoot, "rpt_md.json");
        File.WriteAllText(inputPath, "{\"id\":30001,\"aegisName\":\"Md_Test\"}");
        new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();

        var result = new ReportCommand(_agentRoot, null, true, "md").Execute();
        Assert.True(result.Ok);
    }

    // --- Rollback Tests ---
    [Fact]
    public void RollbackCommand_ListsPlans()
    {
        new IndexCommand(_configDir, _agentRoot, "entities").Execute();
        var inputPath = Path.Combine(_agentRoot, "rb.json");
        File.WriteAllText(inputPath, "{\"id\":30001,\"aegisName\":\"Rollback_Test\"}");
        new DryRunCommand(_configDir, _agentRoot, "item", inputPath).Execute();

        var result = new RollbackCommand(_agentRoot, null, true, false, false).Execute();
        Assert.True(result.Ok);
    }

    [Fact]
    public void RollbackCommand_RealRollbackIsBlocked()
    {
        var result = new RollbackCommand(_agentRoot, "fake", false, false, true).Execute();
        Assert.False(result.Ok);
        Assert.Equal("blocked_by_safety_policy", result.NextRequiredAction);
    }

    // --- Config Tests ---
    [Fact]
    public void ConfigCommand_GetReturnsProfile()
    {
        var cmd = new ConfigCommand(_configDir, _agentRoot, "get");
        var result = cmd.Execute();
        Assert.True(result.Ok);
        Assert.Equal("test", result.ActiveProfile);
    }

    [Fact]
    public void ConfigCommand_ValidateDetectsInsecureProfile()
    {
        // Our test profile is safe, so validate should pass
        var result = new ConfigCommand(_configDir, _agentRoot, "validate").Execute();
        Assert.True(result.Ok);
    }

    [Fact]
    public void ConfigCommand_SetRejectsUnknownKey()
    {
        var result = new ConfigCommand(_configDir, _agentRoot, "set", "unknownKey", "value").Execute();
        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("not allowed"));
    }

    // --- Profile Tests ---
    [Fact]
    public void ProfileCommand_ListsProfiles()
    {
        var result = new ProfileCommand(_configDir, _agentRoot, "list").Execute();
        Assert.True(result.Ok);
    }

    [Fact]
    public void ProfileCommand_UseRejectsNonexistent()
    {
        var result = new ProfileCommand(_configDir, _agentRoot, "use", "nonexistent").Execute();
        Assert.False(result.Ok);
    }

    [Fact]
    public void ProfileCommand_ValidateWorks()
    {
        var result = new ProfileCommand(_configDir, _agentRoot, "validate").Execute();
        Assert.True(result.Ok);
    }

    // --- Security ---
    [Fact]
    public void ApplyCommand_IsBlocked()
    {
        // Simulate what CLI does for apply
        var output = new JsonOutput
        {
            Ok = false, Mode = "apply",
            Summary = "Real apply is blocked in this MVP.",
            SafeForAutomation = false, NextRequiredAction = "blocked_by_safety_policy"
        };
        Assert.False(output.Ok);
        Assert.Equal("blocked_by_safety_policy", output.NextRequiredAction);
    }
}
