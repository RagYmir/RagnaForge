using System.Text.Json;
using RagnaForge.Agent.Core.Configuration;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Tests for ConfigLoader and ConfigFingerprint.
/// Uses temporary directories with valid/invalid JSON fixtures.
/// </summary>
public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_config_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private void WriteConfig(string fileName, object content)
    {
        var json = JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_tempDir, fileName), json);
    }

    private void WriteValidConfigs()
    {
        WriteConfig("ragnaforge.agent.json", new
        {
            agentName = "Test Agent",
            mode = "local-orchestrator",
            primaryOperators = new[] { "Codex", "Antigravity" },
            defaultOutputFormat = "json",
            cacheEnabled = true,
            logsEnabled = true
        });

        WriteConfig("paths.json", new
        {
            agentRoot = _tempDir,
            activeProfile = "test",
            profiles = new Dictionary<string, object>
            {
                ["test"] = new
                {
                    ragnaforgeMainProjectPath = Path.Combine(_tempDir, "project"),
                    rathenaPath = Path.Combine(_tempDir, "rathena"),
                    patchPath = Path.Combine(_tempDir, "patch"),
                    grfRepositoryPath = Path.Combine(_tempDir, "grfs"),
                    grfEditorPath = Path.Combine(_tempDir, "grfeditor"),
                    dbMode = "renewal",
                    writableRoots = new[] { _tempDir },
                    readOnlyRoots = new[] { Path.Combine(_tempDir, "grfs") }
                }
            }
        });

        WriteConfig("safety.json", new
        {
            requireDryRunBeforeApply = true,
            requireDiffBeforeApply = true,
            requireValidationBeforeApply = true,
            requireExplicitConfirmation = true,
            blockOriginalGrfWrite = true,
            blockLubEditing = true,
            invalidateCacheOnPathChange = true,
            cacheMustMatchActiveProfile = true
        });
    }

    // --- Test: ConfigLoader detects missing config file ---
    [Fact]
    public void LoadAgentConfig_ThrowsWhenMissing()
    {
        var loader = new ConfigLoader(_tempDir);
        Assert.Throws<FileNotFoundException>(() => loader.LoadAgentConfig());
    }

    // --- Test: ConfigLoader detects missing activeProfile ---
    [Fact]
    public void LoadPathsConfig_ThrowsWhenActiveProfileMissing()
    {
        WriteConfig("paths.json", new
        {
            agentRoot = _tempDir,
            activeProfile = "",
            profiles = new Dictionary<string, object>()
        });

        var loader = new ConfigLoader(_tempDir);
        Assert.Throws<InvalidOperationException>(() => loader.LoadPathsConfig());
    }

    // --- Test: ConfigLoader detects non-existent profile ---
    [Fact]
    public void LoadPathsConfig_ThrowsWhenProfileDoesNotExist()
    {
        WriteConfig("paths.json", new
        {
            agentRoot = _tempDir,
            activeProfile = "nonexistent",
            profiles = new Dictionary<string, object>
            {
                ["test"] = new { rathenaPath = "x" }
            }
        });

        var loader = new ConfigLoader(_tempDir);
        Assert.Throws<InvalidOperationException>(() => loader.LoadPathsConfig());
    }

    [Fact]
    public void LoadPathsConfig_ThrowsWhenDbModeInvalid()
    {
        WriteConfig("paths.json", new
        {
            agentRoot = _tempDir,
            activeProfile = "test",
            profiles = new Dictionary<string, object>
            {
                ["test"] = new { rathenaPath = "x", dbMode = "chaos" }
            }
        });

        var loader = new ConfigLoader(_tempDir);
        Assert.Throws<InvalidOperationException>(() => loader.LoadPathsConfig());
    }

    // --- Test: ConfigLoader successfully loads valid config ---
    [Fact]
    public void LoadAllConfigs_SucceedsWithValidFiles()
    {
        WriteValidConfigs();
        var loader = new ConfigLoader(_tempDir);

        var agent = loader.LoadAgentConfig();
        var paths = loader.LoadPathsConfig();
        var safety = loader.LoadSafetyConfig();

        Assert.Equal("Test Agent", agent.AgentName);
        Assert.Equal("test", paths.ActiveProfile);
        Assert.True(safety.BlockLubEditing);
    }

    // --- Test: Safety config requires all gates ---
    [Fact]
    public void SafetyConfig_AllGatesEnabled()
    {
        WriteValidConfigs();
        var loader = new ConfigLoader(_tempDir);
        var safety = loader.LoadSafetyConfig();

        Assert.True(safety.RequireDryRunBeforeApply);
        Assert.True(safety.RequireDiffBeforeApply);
        Assert.True(safety.RequireValidationBeforeApply);
        Assert.True(safety.RequireExplicitConfirmation);
    }

    // --- Test: ConfigLoader loads paths from activeProfile ---
    [Fact]
    public void GetActiveProfile_ReturnsCorrectProfile()
    {
        WriteValidConfigs();
        var loader = new ConfigLoader(_tempDir);
        var paths = loader.LoadPathsConfig();
        var profile = ConfigLoader.GetActiveProfile(paths);

        Assert.Equal(Path.Combine(_tempDir, "rathena"), profile.RathenaPath);
        Assert.Equal(Path.Combine(_tempDir, "patch"), profile.PatchPath);
        Assert.Equal(Path.Combine(_tempDir, "grfs"), profile.GrfRepositoryPath);
    }

    // --- Test: ConfigLoader does not use hardcoded fallback ---
    [Fact]
    public void ConfigLoader_NoHardcodedFallback()
    {
        // Using an empty directory should throw, not silently default
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var loader = new ConfigLoader(emptyDir);
        Assert.Throws<FileNotFoundException>(() => loader.LoadAgentConfig());
        Assert.Throws<FileNotFoundException>(() => loader.LoadPathsConfig());
        Assert.Throws<FileNotFoundException>(() => loader.LoadSafetyConfig());
    }
}
