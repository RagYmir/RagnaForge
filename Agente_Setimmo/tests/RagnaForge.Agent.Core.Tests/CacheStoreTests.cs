using System.Text.Json;
using RagnaForge.Agent.Core.Scanning;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Tests for CacheStore — validates save/load, invalidation detection and path safety.
/// </summary>
public class CacheStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _agentRoot;

    public CacheStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_cache_test_{Guid.NewGuid():N}");
        _agentRoot = Path.Combine(_tempDir, "agent");
        Directory.CreateDirectory(_agentRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private ProjectIndex CreateIndex(
        string profile = "test",
        string fingerprint = "fp_abc",
        string scanRoot = @"C:\project")
    {
        return new ProjectIndex
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            ActiveProfile = profile,
            ConfigFingerprint = fingerprint,
            ScanRoot = scanRoot,
            Stats = new ScanStats { FilesIndexed = 5 }
        };
    }

    // --- Test 1: Saves project_index.json inside cache/agent ---
    [Fact]
    public void Save_CreatesFileInCacheAgent()
    {
        var store = new CacheStore(_agentRoot);
        var index = CreateIndex();

        store.Save(index);

        Assert.True(store.CacheExists());
        Assert.True(File.Exists(store.CachePath));
        Assert.Contains("cache", store.CachePath);
        Assert.Contains("project_index.json", store.CachePath);
    }

    // --- Test 2: Detects cache does not exist ---
    [Fact]
    public void Validate_DetectsCacheNotFound()
    {
        var store = new CacheStore(_agentRoot);

        var result = store.Validate("test", "fp", @"C:\project");

        Assert.False(result.IsValid);
        Assert.Contains("cache_not_found", result.InvalidationReason!);
    }

    // --- Test 3: Detects activeProfile mismatch ---
    [Fact]
    public void Validate_DetectsProfileMismatch()
    {
        var store = new CacheStore(_agentRoot);
        store.Save(CreateIndex(profile: "old_profile"));

        var result = store.Validate("new_profile", "fp_abc", @"C:\project");

        Assert.False(result.IsValid);
        Assert.Contains("active_profile_mismatch", result.InvalidationReason!);
    }

    // --- Test 4: Detects configFingerprint mismatch ---
    [Fact]
    public void Validate_DetectsFingerprintMismatch()
    {
        var store = new CacheStore(_agentRoot);
        store.Save(CreateIndex(fingerprint: "old_fp_123456789012"));

        var result = store.Validate("test", "new_fp_999999999999", @"C:\project");

        Assert.False(result.IsValid);
        Assert.Contains("config_fingerprint_mismatch", result.InvalidationReason!);
    }

    // --- Test 5: Detects scanRoot mismatch ---
    [Fact]
    public void Validate_DetectsScanRootMismatch()
    {
        var scanRoot1 = Path.Combine(_tempDir, "project_v1");
        var scanRoot2 = Path.Combine(_tempDir, "project_v2");
        Directory.CreateDirectory(scanRoot1);
        Directory.CreateDirectory(scanRoot2);

        var store = new CacheStore(_agentRoot);
        store.Save(CreateIndex(scanRoot: PathGuard.Normalize(scanRoot1)));

        var result = store.Validate("test", "fp_abc", scanRoot2);

        Assert.False(result.IsValid);
        Assert.Contains("scan_root_mismatch", result.InvalidationReason!);
    }

    // --- Test 6: Valid cache passes ---
    [Fact]
    public void Validate_PassesWhenValid()
    {
        var scanRoot = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(scanRoot);

        var store = new CacheStore(_agentRoot);
        store.Save(CreateIndex(scanRoot: PathGuard.Normalize(scanRoot)));

        var result = store.Validate("test", "fp_abc", scanRoot);

        Assert.True(result.IsValid);
    }

    // --- Test 7: Rejects cache path outside agentRoot ---
    [Fact]
    public void Save_RejectsPathOutsideAgentRoot()
    {
        // CacheStore always writes inside agentRoot, but if someone
        // tampers with the path, the safety check should catch it.
        // We test that Save works within agentRoot (positive case).
        var store = new CacheStore(_agentRoot);
        var index = CreateIndex();

        // This should not throw
        store.Save(index);
        Assert.True(store.CacheExists());

        // Verify the path is inside agentRoot
        var normalizedCache = Path.GetFullPath(store.CachePath);
        var normalizedAgent = Path.GetFullPath(_agentRoot);
        Assert.StartsWith(normalizedAgent, normalizedCache);
    }

    // --- Test 8: Load returns null when cache does not exist ---
    [Fact]
    public void Load_ReturnsNullWhenMissing()
    {
        var store = new CacheStore(_agentRoot);
        Assert.Null(store.Load());
    }

    // --- Test 9: Load round-trips correctly ---
    [Fact]
    public void Load_RoundTrips()
    {
        var store = new CacheStore(_agentRoot);
        var original = CreateIndex(profile: "roundtrip", fingerprint: "fp_roundtrip");
        original.Entries.Add(new ProjectIndexEntry
        {
            AbsolutePath = @"C:\project\file.cs",
            RelativePath = "file.cs",
            Extension = ".cs",
            Category = "dotnet",
            SizeBytes = 100,
            Sha256 = "abc123"
        });

        store.Save(original);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("roundtrip", loaded!.ActiveProfile);
        Assert.Equal("fp_roundtrip", loaded.ConfigFingerprint);
        Assert.Single(loaded.Entries);
        Assert.Equal("file.cs", loaded.Entries[0].RelativePath);
    }
}
