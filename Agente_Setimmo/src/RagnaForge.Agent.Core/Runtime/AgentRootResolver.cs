namespace RagnaForge.Agent.Core.Runtime;

public sealed record AgentRootResolution(string AgentRoot, bool ConfigExists, string Source);

public static class AgentRootResolver
{
    public const string EnvironmentVariable = "RAGNAFORGE_AGENT_ROOT";
    public const string InstallMarkerFile = "ragnaforge.agentroot";

    public static AgentRootResolution Resolve(string startDir, string? currentDirectory = null)
    {
        var envRoot = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envRoot))
            return FromCandidate(envRoot, "environment");

        var markerRoot = ReadInstallMarker(startDir);
        if (!string.IsNullOrWhiteSpace(markerRoot))
            return FromCandidate(markerRoot, "install_marker");

        var foundFromStart = FindUpwards(startDir);
        if (foundFromStart is not null)
            return FromCandidate(foundFromStart, "start_dir");

        var cwd = currentDirectory ?? Directory.GetCurrentDirectory();
        var foundFromCwd = FindUpwards(cwd);
        if (foundFromCwd is not null)
            return FromCandidate(foundFromCwd, "current_directory");

        return FromCandidate(startDir, "unresolved");
    }

    public static bool HasRequiredConfig(string agentRoot) =>
        File.Exists(Path.Combine(agentRoot, "config", "paths.json"));

    private static AgentRootResolution FromCandidate(string candidate, string source)
    {
        var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate));
        return new AgentRootResolution(full, HasRequiredConfig(full), source);
    }

    private static string? ReadInstallMarker(string startDir)
    {
        var marker = Path.Combine(startDir, InstallMarkerFile);
        if (!File.Exists(marker)) return null;

        var value = File.ReadAllText(marker).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? FindUpwards(string startDir)
    {
        var dir = Path.GetFullPath(startDir);
        for (var i = 0; i < 10; i++)
        {
            if (HasRequiredConfig(dir)) return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null) return null;
            dir = parent.FullName;
        }

        return null;
    }
}
