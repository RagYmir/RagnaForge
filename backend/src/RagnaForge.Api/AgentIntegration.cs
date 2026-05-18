using System.Diagnostics;
using System.Text.Json;

namespace RagnaForge.Api;

public sealed record AgentHealthSummary(
    bool AgentReachable,
    bool StatusOk,
    bool DoctorOk,
    string ActiveProfile,
    string AgentVersion,
    string ConfigFingerprint,
    string DbMode,
    bool GrfProtected,
    bool LubEditingBlocked,
    bool CacheExists,
    bool CacheMatchesFingerprint,
    AgentSafetySummary Safety,
    AgentDoctorSummary Doctor,
    AgentIndexSummary? Index,
    AgentValidateSummary? Validation,
    AgentScanSummary? Scan,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    DateTimeOffset GeneratedAtUtc);

public sealed record AgentSafetySummary(
    bool RequireDryRunBeforeApply,
    bool RequireDiffBeforeApply,
    bool RequireExplicitConfirmation,
    bool BackupBeforeApply,
    bool BlockOriginalGrfWrite,
    bool BlockLubEditing,
    bool InvalidateCacheOnPathChange,
    bool CacheMustMatchActiveProfile,
    bool ApplyBlocked,
    bool RollbackRealBlocked);

public sealed record AgentDoctorSummary(
    int TotalChecks,
    int Passed,
    int Warnings,
    int Errors,
    IReadOnlyList<AgentDoctorCheck> FailedChecks);

public sealed record AgentDoctorCheck(string Check, string Severity, string Message);

public sealed record AgentIndexSummary(
    int ItemsFound,
    int MonstersFound,
    int NpcsFound,
    int MapsFound,
    int FilesScanned,
    int FilesParsed,
    int FilesSkipped,
    long DurationMs,
    DateTimeOffset? GeneratedAtUtc);

public sealed record AgentValidateSummary(
    int TotalIssues,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<AgentValidateCategory> TopCategories);

public sealed record AgentValidateCategory(string Code, int Count);

public sealed record AgentScanSummary(
    int FilesVisited,
    int FilesIndexed,
    int FilesSkipped,
    int DirectoriesVisited,
    long DurationMs);

public sealed record RagnaForgeAgentProcessResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut);

public interface IRagnaForgeAgentProcessExecutor
{
    Task<RagnaForgeAgentProcessResult?> ExecuteAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed class RagnaForgeAgentProcessExecutor : IRagnaForgeAgentProcessExecutor
{
    private readonly ILogger<RagnaForgeAgentProcessExecutor> _logger;

    public RagnaForgeAgentProcessExecutor(ILogger<RagnaForgeAgentProcessExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<RagnaForgeAgentProcessResult?> ExecuteAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                KillProcess(process);
                var timedOutStdout = await SafeReadAsync(stdoutTask);
                var timedOutStderr = await SafeReadAsync(stderrTask);
                return new RagnaForgeAgentProcessResult(-1, timedOutStdout, timedOutStderr, TimedOut: true);
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return new RagnaForgeAgentProcessResult(process.ExitCode, stdout, stderr, TimedOut: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to execute RagnaForge Agent command.");
            return null;
        }
    }

    private void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill timed out RagnaForge Agent process.");
        }
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task;
        }
        catch
        {
            return string.Empty;
        }
    }
}

public sealed class RagnaForgeAgentCommandRunner
{
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "status --json",
        "doctor --json",
        "scan --project --json",
        "index --entities --json",
        "validate --json"
    };

    private readonly string _agentExePath;
    private readonly TimeSpan _timeout;
    private readonly IRagnaForgeAgentProcessExecutor _processExecutor;
    private readonly ILogger<RagnaForgeAgentCommandRunner> _logger;

    public RagnaForgeAgentCommandRunner(
        string agentExePath,
        TimeSpan timeout,
        IRagnaForgeAgentProcessExecutor processExecutor,
        ILogger<RagnaForgeAgentCommandRunner> logger)
    {
        _agentExePath = agentExePath;
        _timeout = timeout;
        _processExecutor = processExecutor;
        _logger = logger;
    }

    public static bool IsCommandAllowed(string command) => AllowedCommands.Contains(command);

    public async Task<JsonDocument?> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!IsCommandAllowed(command))
        {
            _logger.LogWarning("Blocked non-allowlisted RagnaForge Agent command: {Command}", command);
            return null;
        }

        if (!File.Exists(_agentExePath))
        {
            _logger.LogWarning("RagnaForge Agent executable is unavailable.");
            return null;
        }

        var result = await _processExecutor.ExecuteAsync(_agentExePath, command, _timeout, cancellationToken);
        if (result is null)
        {
            return null;
        }

        if (result.TimedOut)
        {
            _logger.LogWarning("RagnaForge Agent command timed out: {Command}", command);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            _logger.LogInformation(
                "RagnaForge Agent command emitted stderr. command={Command} stderrLength={Length}",
                command,
                result.Stderr.Length);
        }

        if (string.IsNullOrWhiteSpace(result.Stdout))
        {
            _logger.LogWarning("RagnaForge Agent command returned empty stdout: {Command}", command);
            return null;
        }

        try
        {
            return JsonDocument.Parse(result.Stdout);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "RagnaForge Agent command returned invalid JSON: {Command}", command);
            return null;
        }
    }
}

public sealed class RagnaForgeAgentSummaryService
{
    private readonly RagnaForgeAgentCommandRunner _runner;
    private readonly string _agentCacheDir;
    private readonly ILogger<RagnaForgeAgentSummaryService> _logger;

    public RagnaForgeAgentSummaryService(
        RagnaForgeAgentCommandRunner runner,
        string agentCacheDir,
        ILogger<RagnaForgeAgentSummaryService> logger)
    {
        _runner = runner;
        _agentCacheDir = agentCacheDir;
        _logger = logger;
    }

    public async Task<AgentHealthSummary> GetHealthSummaryAsync(CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        var statusDoc = await _runner.ExecuteAsync("status --json", ct);
        var status = ReadStatus(statusDoc, errors);

        var doctorDoc = await _runner.ExecuteAsync("doctor --json", ct);
        var doctor = ReadDoctor(doctorDoc, errors, out var doctorOk);

        var index = ReadIndexSummaryFromCache(status.ActiveProfile, status.ConfigFingerprint, warnings);
        var scan = ReadScanSummaryFromCache(status.ActiveProfile, status.ConfigFingerprint, warnings);
        var validation = await ReadValidateSummaryAsync(ct);

        var agentVersion = index.AgentVersion ?? scan.AgentVersion ?? "unknown";

        return new AgentHealthSummary(
            AgentReachable: statusDoc is not null,
            StatusOk: status.StatusOk,
            DoctorOk: doctorOk,
            ActiveProfile: status.ActiveProfile,
            AgentVersion: agentVersion,
            ConfigFingerprint: status.ConfigFingerprint,
            DbMode: status.DbMode,
            GrfProtected: status.GrfProtected,
            LubEditingBlocked: status.LubEditingBlocked,
            CacheExists: status.CacheExists,
            CacheMatchesFingerprint: status.CacheMatchesFingerprint,
            Safety: status.Safety,
            Doctor: doctor,
            Index: index.Summary,
            Validation: validation,
            Scan: scan.Summary,
            Warnings: warnings,
            Errors: errors,
            GeneratedAtUtc: DateTimeOffset.UtcNow);
    }

    private AgentStatusState ReadStatus(JsonDocument? statusDoc, List<string> errors)
    {
        if (statusDoc is null)
        {
            errors.Add("Agent unavailable: status command failed or timed out.");
            return AgentStatusState.Unavailable;
        }

        var root = statusDoc.RootElement;
        var statusOk = GetBool(root, "ok");
        var activeProfile = GetString(root, "activeProfile", "unknown");
        var configFingerprint = GetString(root, "configFingerprint", string.Empty);
        var dbMode = "unknown";
        var grfProtected = false;
        var lubBlocked = false;
        var cacheExists = false;
        var cacheMatches = false;
        var safety = AgentStatusState.Unavailable.Safety;

        if (root.TryGetProperty("data", out var data))
        {
            dbMode = GetString(data, "dbMode", "unknown");
            grfProtected = GetBool(data, "grfProtected");
            lubBlocked = GetBool(data, "lubEditingBlocked");

            if (data.TryGetProperty("cache", out var cache))
            {
                cacheExists = GetBool(cache, "indexExists");
                cacheMatches = GetBool(cache, "matchesActiveFingerprint");
            }

            if (data.TryGetProperty("safety", out var safetyEl))
            {
                safety = new AgentSafetySummary(
                    GetBool(safetyEl, "requireDryRunBeforeApply"),
                    GetBool(safetyEl, "requireDiffBeforeApply"),
                    GetBool(safetyEl, "requireExplicitConfirmation"),
                    GetBool(safetyEl, "backupBeforeApply"),
                    GetBool(safetyEl, "blockOriginalGrfWrite"),
                    GetBool(safetyEl, "blockLubEditing"),
                    GetBool(safetyEl, "invalidateCacheOnPathChange"),
                    GetBool(safetyEl, "cacheMustMatchActiveProfile"),
                    ApplyBlocked: true,
                    RollbackRealBlocked: true);
            }
        }

        return new AgentStatusState(
            statusOk,
            activeProfile,
            configFingerprint,
            dbMode,
            grfProtected,
            lubBlocked,
            cacheExists,
            cacheMatches,
            safety);
    }

    private static AgentDoctorSummary ReadDoctor(JsonDocument? doctorDoc, List<string> errors, out bool doctorOk)
    {
        doctorOk = false;
        if (doctorDoc is null)
        {
            errors.Add("Agent unavailable: doctor command failed or timed out.");
            return new AgentDoctorSummary(0, 0, 0, 0, []);
        }

        var root = doctorDoc.RootElement;
        doctorOk = GetBool(root, "ok");
        var checks = new List<AgentDoctorCheck>();

        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("checks", out var checksEl))
        {
            foreach (var check in checksEl.EnumerateArray())
            {
                checks.Add(new AgentDoctorCheck(
                    GetString(check, "check", string.Empty),
                    GetString(check, "severity", string.Empty),
                    GetString(check, "message", string.Empty)));
            }
        }

        var passed = checks.Count(c => c.Severity.Equals("pass", StringComparison.OrdinalIgnoreCase));
        var warnings = checks.Count(c => c.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase));
        var errs = checks.Count(c =>
            c.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)
            || c.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase));
        var failed = checks.Where(c => !c.Severity.Equals("pass", StringComparison.OrdinalIgnoreCase)).ToList();
        return new AgentDoctorSummary(checks.Count, passed, warnings, errs, failed);
    }

    private async Task<AgentValidateSummary?> ReadValidateSummaryAsync(CancellationToken ct)
    {
        var validateDoc = await _runner.ExecuteAsync("validate --json", ct);
        if (validateDoc is null)
        {
            return null;
        }

        var root = validateDoc.RootElement;
        var data = root.TryGetProperty("data", out var dataEl) ? dataEl : root;
        var totalIssues = GetInt(data, "totalIssues");
        var errorCount = GetInt(data, "errors");
        var warningCount = GetInt(data, "warnings");

        var categories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (data.TryGetProperty("issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
        {
            foreach (var issue in issues.EnumerateArray())
            {
                var code = GetString(issue, "code", "UNKNOWN");
                categories[code] = categories.GetValueOrDefault(code) + 1;
            }
        }

        return new AgentValidateSummary(
            totalIssues,
            errorCount,
            warningCount,
            categories
                .Select(kvp => new AgentValidateCategory(kvp.Key, kvp.Value))
                .OrderByDescending(c => c.Count)
                .Take(10)
                .ToList());
    }

    private CacheReadResult<AgentIndexSummary> ReadIndexSummaryFromCache(
        string activeProfile,
        string configFingerprint,
        List<string> warnings)
    {
        var indexPath = Path.Combine(_agentCacheDir, "entities_index.json");
        return ReadCacheFile(indexPath, activeProfile, configFingerprint, warnings, root =>
        {
            if (!root.TryGetProperty("stats", out var stats))
            {
                return null;
            }

            var generated = TryReadGeneratedAt(root);
            return new AgentIndexSummary(
                GetInt(stats, "itemsFound"),
                GetInt(stats, "monstersFound"),
                GetInt(stats, "npcsFound"),
                GetInt(stats, "mapsFound"),
                GetInt(stats, "filesScanned"),
                GetInt(stats, "filesParsed"),
                GetInt(stats, "filesSkipped"),
                GetLong(stats, "durationMs"),
                generated);
        });
    }

    private CacheReadResult<AgentScanSummary> ReadScanSummaryFromCache(
        string activeProfile,
        string configFingerprint,
        List<string> warnings)
    {
        var scanPath = Path.Combine(_agentCacheDir, "project_index.json");
        return ReadCacheFile(scanPath, activeProfile, configFingerprint, warnings, root =>
        {
            if (!root.TryGetProperty("stats", out var stats))
            {
                return null;
            }

            return new AgentScanSummary(
                GetInt(stats, "filesVisited"),
                GetInt(stats, "filesIndexed"),
                GetInt(stats, "filesSkipped"),
                GetInt(stats, "directoriesVisited"),
                GetLong(stats, "durationMs"));
        });
    }

    private CacheReadResult<T> ReadCacheFile<T>(
        string cachePath,
        string activeProfile,
        string configFingerprint,
        List<string> warnings,
        Func<JsonElement, T?> read)
        where T : class
    {
        var fileName = Path.GetFileName(cachePath);
        if (!File.Exists(cachePath))
        {
            warnings.Add($"{fileName} not found. Run the matching RagnaForge Agent scan/index command.");
            return new CacheReadResult<T>(null, null);
        }

        try
        {
            using var stream = File.OpenRead(cachePath);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            var cacheProfile = GetString(root, "activeProfile", string.Empty);
            var cacheFingerprint = GetString(root, "configFingerprint", string.Empty);
            var agentVersion = GetString(root, "agentVersion", "unknown");

            if (!cacheProfile.Equals(activeProfile, StringComparison.Ordinal)
                || !cacheFingerprint.Equals(configFingerprint, StringComparison.Ordinal))
            {
                warnings.Add(
                    $"{fileName} is stale for the active profile/fingerprint. Run the matching RagnaForge Agent scan/index command before trusting counts.");
                return new CacheReadResult<T>(null, agentVersion);
            }

            return new CacheReadResult<T>(read(root), agentVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read RagnaForge Agent cache file {FileName}.", fileName);
            warnings.Add($"{fileName} could not be read safely.");
            return new CacheReadResult<T>(null, null);
        }
    }

    private static DateTimeOffset? TryReadGeneratedAt(JsonElement root)
    {
        if (root.TryGetProperty("generatedAtUtc", out var generated)
            && DateTimeOffset.TryParse(generated.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool GetBool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

    private static int GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var value) ? value : 0;

    private static long GetLong(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt64(out var value) ? value : 0L;

    private static string GetString(JsonElement el, string prop, string fallback) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback
            : fallback;

    private sealed record AgentStatusState(
        bool StatusOk,
        string ActiveProfile,
        string ConfigFingerprint,
        string DbMode,
        bool GrfProtected,
        bool LubEditingBlocked,
        bool CacheExists,
        bool CacheMatchesFingerprint,
        AgentSafetySummary Safety)
    {
        public static AgentStatusState Unavailable { get; } = new(
            false,
            "unknown",
            string.Empty,
            "unknown",
            false,
            false,
            false,
            false,
            new AgentSafetySummary(true, true, true, true, true, true, true, true, true, true));
    }

    private sealed record CacheReadResult<T>(T? Summary, string? AgentVersion)
        where T : class;
}
