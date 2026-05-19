using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Configuration;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Tests for ScanCommand — validates end-to-end scan behavior.
/// Uses temporary fixtures with valid config files.
/// </summary>
public class ScanCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _agentRoot;
    private readonly string _configDir;
    private readonly string _scanTarget;

    public ScanCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_scancmd_test_{Guid.NewGuid():N}");
        _agentRoot = Path.Combine(_tempDir, "agent");
        _configDir = Path.Combine(_agentRoot, "config");
        _scanTarget = Path.Combine(_tempDir, "project");

        Directory.CreateDirectory(_configDir);
        Directory.CreateDirectory(_scanTarget);
        Directory.CreateDirectory(Path.Combine(_agentRoot, "cache", "agent"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private void WriteConfig(string fileName, object content)
    {
        var json = JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_configDir, fileName), json);
    }

    private void WriteValidConfigs()
    {
        WriteConfig("ragnaforge.agent.json", new
        {
            agentName = "Test Agent",
            mode = "local-orchestrator",
            primaryOperators = new[] { "Test" },
            defaultOutputFormat = "json",
            cacheEnabled = true,
            logsEnabled = true
        });

        WriteConfig("paths.json", new
        {
            agentRoot = _agentRoot,
            activeProfile = "test",
            profiles = new Dictionary<string, object>
            {
                ["test"] = new
                {
                    ragnaforgeMainProjectPath = _scanTarget,
                    rathenaPath = _scanTarget,
                    patchPath = _scanTarget,
                    grfRepositoryPath = Path.Combine(_tempDir, "grfs"),
                    grfEditorPath = _scanTarget,
                    writableRoots = new[] { _agentRoot, _scanTarget },
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
            backupBeforeApply = true,
            blockOriginalGrfWrite = true,
            blockLubEditing = true,
            invalidateCacheOnPathChange = true,
            cacheMustMatchActiveProfile = true
        });

        // Create GRF directory
        Directory.CreateDirectory(Path.Combine(_tempDir, "grfs"));
    }

    private void CreateProjectFile(string relativePath, string content = "test")
    {
        var fullPath = Path.Combine(_scanTarget, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    // --- Test 1: ScanCommand returns ok=true ---
    [Fact]
    public void Execute_ReturnsOkOnValidFixture()
    {
        WriteValidConfigs();
        CreateProjectFile("Program.cs", "using System;");
        CreateProjectFile("README.md", "# Hello");

        var cmd = new ScanCommand(_configDir, _agentRoot);
        var result = cmd.Execute();

        Assert.True(result.Ok);
        Assert.Equal("scan", result.Mode);
        Assert.NotNull(result.ActiveProfile);
        Assert.NotNull(result.ConfigFingerprint);
    }

    // --- Test 2: ScanCommand returns ok=false if scanRoot missing ---
    [Fact]
    public void Execute_FailsIfScanRootMissing()
    {
        var missingTarget = Path.Combine(_tempDir, "nonexistent_project");

        WriteConfig("ragnaforge.agent.json", new
        {
            agentName = "Test",
            mode = "local-orchestrator",
            primaryOperators = new[] { "Test" },
            defaultOutputFormat = "json",
            cacheEnabled = true,
            logsEnabled = true
        });

        WriteConfig("paths.json", new
        {
            agentRoot = _agentRoot,
            activeProfile = "test",
            profiles = new Dictionary<string, object>
            {
                ["test"] = new
                {
                    ragnaforgeMainProjectPath = missingTarget,
                    rathenaPath = _scanTarget,
                    patchPath = _scanTarget,
                    grfRepositoryPath = Path.Combine(_tempDir, "grfs"),
                    grfEditorPath = _scanTarget,
                    writableRoots = new[] { _agentRoot, _scanTarget },
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
            backupBeforeApply = true,
            blockOriginalGrfWrite = true,
            blockLubEditing = true,
            invalidateCacheOnPathChange = true,
            cacheMustMatchActiveProfile = true
        });

        Directory.CreateDirectory(Path.Combine(_tempDir, "grfs"));

        var cmd = new ScanCommand(_configDir, _agentRoot);
        var result = cmd.Execute();

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Contains("does not exist"));
    }

    // --- Test 3: ScanCommand includes profile and fingerprint ---
    [Fact]
    public void Execute_IncludesProfileAndFingerprint()
    {
        WriteValidConfigs();
        CreateProjectFile("file.txt", "data");

        var cmd = new ScanCommand(_configDir, _agentRoot);
        var result = cmd.Execute();

        Assert.Equal("test", result.ActiveProfile);
        Assert.False(string.IsNullOrWhiteSpace(result.ConfigFingerprint));
    }

    // --- Test 4: ScanCommand creates cache file ---
    [Fact]
    public void Execute_CreatesCacheFile()
    {
        WriteValidConfigs();
        CreateProjectFile("app.cs", "code");

        var cmd = new ScanCommand(_configDir, _agentRoot);
        cmd.Execute();

        var cachePath = Path.Combine(_agentRoot, "cache", "agent", "project_index.json");
        Assert.True(File.Exists(cachePath));

        var json = File.ReadAllText(cachePath);
        Assert.Contains("configFingerprint", json);
        Assert.Contains("activeProfile", json);
    }

    // --- Test 5: ScanCommand never modifies scanned files ---
    [Fact]
    public void Execute_NeverModifiesScannedFiles()
    {
        WriteValidConfigs();
        var filePath = Path.Combine(_scanTarget, "important.cs");
        File.WriteAllText(filePath, "original");
        var originalModTime = File.GetLastWriteTimeUtc(filePath);

        var cmd = new ScanCommand(_configDir, _agentRoot);
        cmd.Execute();

        Assert.Equal("original", File.ReadAllText(filePath));
        Assert.Equal(originalModTime, File.GetLastWriteTimeUtc(filePath));
    }
}
