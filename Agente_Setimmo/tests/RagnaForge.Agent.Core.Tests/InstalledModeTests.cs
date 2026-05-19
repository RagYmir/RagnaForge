using System.Text.Json;
using RagnaForge.Agent.Core.Runtime;

namespace RagnaForge.Agent.Core.Tests;

public sealed class InstalledModeTests : IDisposable
{
    private static readonly object EnvLock = new();
    private readonly string _tempDir;
    private readonly string _agentRoot;
    private readonly string _configDir;
    private readonly string _rathenaDir;
    private readonly string _patchDir;
    private readonly string _grfDir;

    public InstalledModeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_installed_test_{Guid.NewGuid():N}");
        _agentRoot = Path.Combine(_tempDir, "agent");
        _configDir = Path.Combine(_agentRoot, "config");
        _rathenaDir = Path.Combine(_tempDir, "rathena");
        _patchDir = Path.Combine(_tempDir, "patch");
        _grfDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_installed_grfs_{Guid.NewGuid():N}");

        foreach (var dir in new[]
        {
            _configDir,
            _rathenaDir,
            _patchDir,
            _grfDir,
            Path.Combine(_agentRoot, "cache", "agent"),
            Path.Combine(_agentRoot, "logs", "operations"),
            Path.Combine(_agentRoot, "docs"),
            Path.Combine(_agentRoot, ".agents", "rules"),
            Path.Combine(_agentRoot, ".agents", "workflows"),
            Path.Combine(_agentRoot, ".agents", "skills")
        })
            Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(_agentRoot, "AGENTS.md"), "# Test");
        File.WriteAllText(Path.Combine(_agentRoot, "docs", "AI_AGENT_CONTRACT.md"), "# Test");
        WriteConfig();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        try { Directory.Delete(_grfDir, true); } catch { }
    }

    [Fact]
    public void AgentRootResolver_UsesEnvironmentVariable()
    {
        WithEnv(_agentRoot, () =>
        {
            var result = AgentRootResolver.Resolve(Path.Combine(_tempDir, "elsewhere"));
            Assert.Equal(Path.GetFullPath(_agentRoot), result.AgentRoot);
            Assert.True(result.ConfigExists);
            Assert.Equal("environment", result.Source);
        });
    }

    [Fact]
    public void AgentRootResolver_UsesInstallMarker()
    {
        lock (EnvLock)
        {
            var old = Environment.GetEnvironmentVariable(AgentRootResolver.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(AgentRootResolver.EnvironmentVariable, null);
                var installDir = Path.Combine(_tempDir, "dist", "ragnaforge");
                Directory.CreateDirectory(installDir);
                File.WriteAllText(Path.Combine(installDir, AgentRootResolver.InstallMarkerFile), _agentRoot);

                var result = AgentRootResolver.Resolve(installDir, Path.Combine(_tempDir, "outside"));

                Assert.Equal(Path.GetFullPath(_agentRoot), result.AgentRoot);
                Assert.True(result.ConfigExists);
                Assert.Equal("install_marker", result.Source);
            }
            finally
            {
                Environment.SetEnvironmentVariable(AgentRootResolver.EnvironmentVariable, old);
            }
        }
    }

    [Fact]
    public void AgentRootResolver_ReportsMissingConfig()
    {
        lock (EnvLock)
        {
            var old = Environment.GetEnvironmentVariable(AgentRootResolver.EnvironmentVariable);
            try
            {
                var emptyRoot = Path.Combine(_tempDir, "empty-agent");
                Directory.CreateDirectory(emptyRoot);
                Environment.SetEnvironmentVariable(AgentRootResolver.EnvironmentVariable, emptyRoot);

                var result = AgentRootResolver.Resolve(Path.Combine(_tempDir, "elsewhere"));

                Assert.Equal(Path.GetFullPath(emptyRoot), result.AgentRoot);
                Assert.False(result.ConfigExists);
            }
            finally
            {
                Environment.SetEnvironmentVariable(AgentRootResolver.EnvironmentVariable, old);
            }
        }
    }

    [Fact]
    public void CliVersion_ReturnsCurrentVersion()
    {
        var (exitCode, output) = CaptureCli("--version");

        Assert.Equal(0, exitCode);
        Assert.Contains(RagnaForge.Agent.Core.AgentVersion.Current, output);
    }

    [Fact]
    public void CliHelp_ReturnsUsage()
    {
        var (exitCode, output) = CaptureCli("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("Agente Setimmo CLI", output);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public void CliStatus_ReturnsClearJsonErrorWhenConfigDoesNotExist()
    {
        lock (EnvLock)
        {
            var old = Environment.GetEnvironmentVariable(AgentRootResolver.EnvironmentVariable);
            try
            {
                var emptyRoot = Path.Combine(_tempDir, "missing-config-root");
                Directory.CreateDirectory(emptyRoot);
                Environment.SetEnvironmentVariable(AgentRootResolver.EnvironmentVariable, emptyRoot);

                var (exitCode, output) = CaptureCli("status", "--json");

                Assert.Equal(1, exitCode);
                Assert.Contains("configure_agent_root", output);
                Assert.Contains("paths.json", output);
            }
            finally
            {
                Environment.SetEnvironmentVariable(AgentRootResolver.EnvironmentVariable, old);
            }
        }
    }

    [Fact]
    public void CliDoctor_WorksOutsideCurrentDirectoryWhenAgentRootEnvIsSet()
    {
        WithEnv(_agentRoot, () =>
        {
            var oldCwd = Directory.GetCurrentDirectory();
            var outside = Path.Combine(_tempDir, "outside");
            Directory.CreateDirectory(outside);
            try
            {
                Directory.SetCurrentDirectory(outside);
                var (exitCode, output) = CaptureCli("doctor", "--json");

                Assert.True(exitCode == 0, output);
                Assert.Contains("\"ok\": true", output);
                Assert.Contains("\"mode\": \"doctor\"", output);
            }
            finally
            {
                Directory.SetCurrentDirectory(oldCwd);
            }
        });
    }

    private static void WithEnv(string agentRoot, Action action)
    {
        lock (EnvLock)
        {
            var old = Environment.GetEnvironmentVariable(AgentRootResolver.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(AgentRootResolver.EnvironmentVariable, agentRoot);
                action();
            }
            finally
            {
                Environment.SetEnvironmentVariable(AgentRootResolver.EnvironmentVariable, old);
            }
        }
    }

    private static (int ExitCode, string Output) CaptureCli(params string[] args)
    {
        var oldOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var exitCode = RagnaForge.Agent.Cli.Program.Main(args);
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
        }
    }

    private void WriteConfig()
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(_configDir, "ragnaforge.agent.json"),
            JsonSerializer.Serialize(new
            {
                agentName = "Installed Test",
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
}
