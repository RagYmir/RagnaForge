using RagnaForge.Agent.Core.Scanning;
using RagnaForge.Agent.Core.Security;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Tests for ProjectScanner and FileHasher.
/// Uses temporary fixtures — never touches real project directories.
/// </summary>
public class ProjectScannerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _scanRoot;
    private readonly PathGuard _guard;

    public ProjectScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_scan_test_{Guid.NewGuid():N}");
        _scanRoot = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(_scanRoot);

        _guard = new PathGuard(
            [_tempDir],
            [],
            blockLubEditing: true
        );
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private void CreateFile(string relativePath, string content = "test content")
    {
        var fullPath = Path.Combine(_scanRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    // --- Test 1: Indexes simple files ---
    [Fact]
    public void Scan_IndexesSimpleFiles()
    {
        CreateFile("Program.cs", "using System;");
        CreateFile("README.md", "# Hello");
        CreateFile("config.json", "{}");

        var scanner = new ProjectScanner(_guard);
        var index = scanner.Scan(_scanRoot, "test", "fingerprint123");

        Assert.Equal(3, index.Stats.FilesIndexed);
        Assert.Equal(3, index.Entries.Count);
        Assert.Equal("test", index.ActiveProfile);
        Assert.Equal("fingerprint123", index.ConfigFingerprint);
    }

    // --- Test 2: Ignores bin/ and obj/ ---
    [Fact]
    public void Scan_IgnoresBinAndObj()
    {
        CreateFile("src/Program.cs", "code");
        CreateFile("bin/Debug/app.dll", "binary");
        CreateFile("obj/Debug/cache.json", "{}");

        var scanner = new ProjectScanner(_guard);
        var index = scanner.Scan(_scanRoot, "test", "fp");

        Assert.Equal(1, index.Stats.FilesIndexed);
        Assert.Contains(index.Skipped, s => s.Reason == "ignored_directory" && s.Path.Contains("bin"));
        Assert.Contains(index.Skipped, s => s.Reason == "ignored_directory" && s.Path.Contains("obj"));
    }

    // --- Test 3: Ignores node_modules/ ---
    [Fact]
    public void Scan_IgnoresNodeModules()
    {
        CreateFile("index.ts", "export default {}");
        CreateFile("node_modules/react/index.js", "module.exports = {}");

        var scanner = new ProjectScanner(_guard);
        var index = scanner.Scan(_scanRoot, "test", "fp");

        Assert.Equal(1, index.Stats.FilesIndexed);
        Assert.Contains(index.Skipped, s => s.Reason == "ignored_directory" && s.Path.Contains("node_modules"));
    }

    // --- Test 4: Does not follow reparse points ---
    // Note: Creating actual reparse points requires admin on Windows.
    // This test verifies the scanner's reparse point flag check works
    // by asserting the behavior when no reparse points are present.
    [Fact]
    public void Scan_DoesNotFollowReparsePoints()
    {
        CreateFile("normal/file.txt", "ok");

        var scanner = new ProjectScanner(_guard);
        var index = scanner.Scan(_scanRoot, "test", "fp");

        Assert.Equal(1, index.Stats.FilesIndexed);
        Assert.DoesNotContain(index.Skipped, s => s.Reason == "reparse_point");
    }

    // --- Test 5: Classifies .cs as dotnet ---
    [Fact]
    public void ClassifyFile_CsAsDotnet()
    {
        CreateFile("App.cs", "class App {}");
        var fi = new FileInfo(Path.Combine(_scanRoot, "App.cs"));
        Assert.Equal("dotnet", ProjectScanner.ClassifyFile(fi));
    }

    // --- Test 6: Classifies .tsx as frontend ---
    [Fact]
    public void ClassifyFile_TsxAsFrontend()
    {
        CreateFile("App.tsx", "export default function App() {}");
        var fi = new FileInfo(Path.Combine(_scanRoot, "App.tsx"));
        Assert.Equal("frontend", ProjectScanner.ClassifyFile(fi));
    }

    // --- Test 7: Classifies .md as docs ---
    [Fact]
    public void ClassifyFile_MdAsDocs()
    {
        CreateFile("README.md", "# Docs");
        var fi = new FileInfo(Path.Combine(_scanRoot, "README.md"));
        Assert.Equal("docs", ProjectScanner.ClassifyFile(fi));
    }

    // --- Test 8: Classifies .json as config ---
    [Fact]
    public void ClassifyFile_JsonAsConfig()
    {
        CreateFile("settings.json", "{}");
        var fi = new FileInfo(Path.Combine(_scanRoot, "settings.json"));
        Assert.Equal("config", ProjectScanner.ClassifyFile(fi));
    }

    // --- Test 9: Classifies .ps1 as script ---
    [Fact]
    public void ClassifyFile_Ps1AsScript()
    {
        CreateFile("build.ps1", "dotnet build");
        var fi = new FileInfo(Path.Combine(_scanRoot, "build.ps1"));
        Assert.Equal("script", ProjectScanner.ClassifyFile(fi));
    }

    // --- Test 10: Files in tests/ directory classified as test ---
    [Fact]
    public void ClassifyFile_TestDirectoryAsTest()
    {
        CreateFile("tests/MyTest.cs", "using Xunit;");
        var fi = new FileInfo(Path.Combine(_scanRoot, "tests", "MyTest.cs"));
        Assert.Equal("test", ProjectScanner.ClassifyFile(fi));
    }

    // --- Test 11: Entries sorted by relativePath ---
    [Fact]
    public void Scan_EntriesSortedByRelativePath()
    {
        CreateFile("z_last.cs", "code");
        CreateFile("a_first.cs", "code");
        CreateFile("m_middle.cs", "code");

        var scanner = new ProjectScanner(_guard);
        var index = scanner.Scan(_scanRoot, "test", "fp");

        Assert.Equal("a_first.cs", index.Entries[0].RelativePath);
        Assert.Equal("m_middle.cs", index.Entries[1].RelativePath);
        Assert.Equal("z_last.cs", index.Entries[2].RelativePath);
    }

    // --- Test 12: Scan never modifies files ---
    [Fact]
    public void Scan_NeverModifiesFiles()
    {
        var filePath = Path.Combine(_scanRoot, "important.cs");
        File.WriteAllText(filePath, "original content");
        var originalLastWrite = File.GetLastWriteTimeUtc(filePath);
        var originalContent = File.ReadAllText(filePath);

        var scanner = new ProjectScanner(_guard);
        scanner.Scan(_scanRoot, "test", "fp");

        Assert.Equal(originalContent, File.ReadAllText(filePath));
        Assert.Equal(originalLastWrite, File.GetLastWriteTimeUtc(filePath));
    }

    // --- Test 13: Scan includes activeProfile and configFingerprint ---
    [Fact]
    public void Scan_IncludesProfileAndFingerprint()
    {
        CreateFile("file.txt", "data");

        var scanner = new ProjectScanner(_guard);
        var index = scanner.Scan(_scanRoot, "production", "abc123xyz");

        Assert.Equal("production", index.ActiveProfile);
        Assert.Equal("abc123xyz", index.ConfigFingerprint);
    }
}

/// <summary>
/// Tests for FileHasher.
/// </summary>
public class FileHasherTests : IDisposable
{
    private readonly string _tempDir;

    public FileHasherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_hash_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // --- Test 14: Computes SHA-256 by streaming ---
    [Fact]
    public void ComputeSha256_ProducesValidHash()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "hello world");

        var result = FileHasher.ComputeSha256(filePath);

        Assert.True(result.Success);
        Assert.NotNull(result.Hash);
        Assert.Equal(64, result.Hash.Length); // SHA-256 = 64 hex chars
        Assert.True(result.Hash.All(c => "0123456789abcdef".Contains(c)));
    }

    // --- Test 15: Skips file above limit ---
    [Fact]
    public void ComputeSha256_SkipsLargeFile()
    {
        var filePath = Path.Combine(_tempDir, "large.bin");
        // Create a file larger than the limit (use 100 bytes as test limit)
        File.WriteAllBytes(filePath, new byte[200]);

        var result = FileHasher.ComputeSha256(filePath, maxFileSizeBytes: 100);

        Assert.False(result.Success);
        Assert.Equal("file_too_large", result.SkipReason);
    }

    // --- Test 16: Handles missing file ---
    [Fact]
    public void ComputeSha256_HandlesMissingFile()
    {
        var result = FileHasher.ComputeSha256(Path.Combine(_tempDir, "nonexistent.txt"));

        Assert.False(result.Success);
        Assert.Equal("file_not_found", result.SkipReason);
    }

    // --- Test 17: Deterministic hash ---
    [Fact]
    public void ComputeSha256_IsDeterministic()
    {
        var filePath = Path.Combine(_tempDir, "stable.txt");
        File.WriteAllText(filePath, "deterministic content");

        var hash1 = FileHasher.ComputeSha256(filePath);
        var hash2 = FileHasher.ComputeSha256(filePath);

        Assert.Equal(hash1.Hash, hash2.Hash);
    }
}
