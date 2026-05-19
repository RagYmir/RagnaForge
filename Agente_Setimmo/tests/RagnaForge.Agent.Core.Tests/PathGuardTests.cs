using RagnaForge.Agent.Core.Security;
using RagnaForge.Agent.Core.Configuration;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Tests for PathGuard — verifies filesystem protection is enforced.
/// Uses temporary fixtures, never touches real GRF or project directories.
/// </summary>
public class PathGuardTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _writableDir;
    private readonly string _readOnlyDir;
    private readonly PathGuard _guard;

    public PathGuardTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_test_{Guid.NewGuid():N}");
        _writableDir = Path.Combine(_tempDir, "writable");
        _readOnlyDir = Path.Combine(_tempDir, "readonly_grfs");

        Directory.CreateDirectory(_writableDir);
        Directory.CreateDirectory(_readOnlyDir);

        _guard = new PathGuard(
            [_writableDir],
            [_readOnlyDir],
            blockLubEditing: true
        );
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // --- Test 1: Block write to GRF (read-only) directory ---
    [Fact]
    public void EnsureCanWrite_BlocksReadOnlyRoot()
    {
        var path = Path.Combine(_readOnlyDir, "data.grf");
        var result = _guard.EnsureCanWrite(path);

        Assert.False(result.IsAllowed);
        Assert.Contains("read-only", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    // --- Test 2: Block path traversal ---
    [Fact]
    public void EnsureCanWrite_BlocksTraversal()
    {
        var path = Path.Combine(_writableDir, "..", "escape", "file.txt");
        var result = _guard.EnsureCanWrite(path);

        Assert.False(result.IsAllowed);
    }

    // --- Test 3: Accept valid Windows paths ---
    [Fact]
    public void Normalize_AcceptsValidWindowsPath()
    {
        var result = PathGuard.Normalize(_writableDir);
        Assert.False(string.IsNullOrEmpty(result));
        Assert.DoesNotContain("..", result);
    }

    // --- Test 4: Accept paths with spaces ---
    [Fact]
    public void Normalize_AcceptsPathsWithSpaces()
    {
        var pathWithSpaces = Path.Combine(_tempDir, "path with spaces");
        Directory.CreateDirectory(pathWithSpaces);

        var result = PathGuard.Normalize(pathWithSpaces);
        Assert.Contains("path with spaces", result);
    }

    // --- Test 5: Accept paths with accents ---
    [Fact]
    public void Normalize_AcceptsPathsWithAccents()
    {
        var pathWithAccents = Path.Combine(_tempDir, "conteúdo");
        Directory.CreateDirectory(pathWithAccents);

        var result = PathGuard.Normalize(pathWithAccents);
        Assert.Contains("conteúdo", result);
    }

    // --- Test 6: Accept paths with apostrophes ---
    [Fact]
    public void Normalize_AcceptsPathsWithApostrophes()
    {
        var pathWithApostrophe = Path.Combine(_tempDir, "GRF'S");
        Directory.CreateDirectory(pathWithApostrophe);

        var result = PathGuard.Normalize(pathWithApostrophe);
        Assert.Contains("GRF'S", result);
    }

    // --- Test 7: Block .lub editing ---
    [Fact]
    public void EnsureCanWriteFileType_BlocksLub()
    {
        var lubPath = Path.Combine(_writableDir, "itemInfo.lub");
        var result = _guard.EnsureCanWriteFileType(lubPath);

        Assert.False(result.IsAllowed);
        Assert.Contains(".lub", result.Reason!);
    }

    // --- Test 8: Allow writing to writable root ---
    [Fact]
    public void EnsureCanWrite_AllowsWritableRoot()
    {
        var path = Path.Combine(_writableDir, "test.yml");
        var result = _guard.EnsureCanWrite(path);

        Assert.True(result.IsAllowed);
    }

    // --- Test 9: Block write outside all roots ---
    [Fact]
    public void EnsureCanWrite_BlocksOutsideAllRoots()
    {
        var outsidePath = Path.Combine(Path.GetTempPath(), "some_other_dir", "file.txt");
        var result = _guard.EnsureCanWrite(outsidePath);

        Assert.False(result.IsAllowed);
    }

    // --- Test 10: Traversal detection ---
    [Fact]
    public void ContainsTraversal_DetectsDoubleDot()
    {
        Assert.True(PathGuard.ContainsTraversal(@"C:\Users\..\secret"));
        Assert.True(PathGuard.ContainsTraversal(@"data\..\escape"));
        Assert.False(PathGuard.ContainsTraversal(@"C:\Users\Normal\Path"));
    }

    // --- Test 11: EnsureCanRead allows writable and read-only roots ---
    [Fact]
    public void EnsureCanRead_AllowsBothRoots()
    {
        var writablePath = Path.Combine(_writableDir, "file.txt");
        var readOnlyPath = Path.Combine(_readOnlyDir, "data.grf");

        Assert.True(_guard.EnsureCanRead(writablePath).IsAllowed);
        Assert.True(_guard.EnsureCanRead(readOnlyPath).IsAllowed);
    }

    // --- Test 12: EnsureProfileIsSafe catches GRF in writable roots (exact match) ---
    [Fact]
    public void EnsureProfileIsSafe_CatchesGrfInWritableRoots()
    {
        var profile = new ProfileConfig
        {
            RagnaforgeMainProjectPath = _writableDir,
            RathenaPath = _writableDir,
            PatchPath = _writableDir,
            GrfRepositoryPath = _readOnlyDir,
            GrfEditorPath = _writableDir,
            WritableRoots = [_writableDir, _readOnlyDir], // GRF in writable = bad!
            ReadOnlyRoots = [_readOnlyDir]
        };

        var issues = PathGuard.EnsureProfileIsSafe(profile);
        Assert.Contains(issues, i => i.Contains("writableRoot", StringComparison.OrdinalIgnoreCase) ||
                                     i.Contains("overlap", StringComparison.OrdinalIgnoreCase) ||
                                     i.Contains("contained", StringComparison.OrdinalIgnoreCase));
    }

    // --- Test 13: EnsureProfileIsSafe catches GRF not in read-only roots ---
    [Fact]
    public void EnsureProfileIsSafe_CatchesGrfNotInReadOnlyRoots()
    {
        var profile = new ProfileConfig
        {
            RagnaforgeMainProjectPath = _writableDir,
            RathenaPath = _writableDir,
            PatchPath = _writableDir,
            GrfRepositoryPath = _readOnlyDir,
            GrfEditorPath = _writableDir,
            WritableRoots = [_writableDir],
            ReadOnlyRoots = [] // GRF not in read-only = bad!
        };

        var issues = PathGuard.EnsureProfileIsSafe(profile);
        Assert.Contains(issues, i => i.Contains("readOnlyRoots"));
    }

    // --- Test 14: EnsureGrfRepositoryIsReadOnly validates correctly ---
    [Fact]
    public void EnsureGrfRepositoryIsReadOnly_PassesWhenCorrect()
    {
        var profile = new ProfileConfig
        {
            GrfRepositoryPath = _readOnlyDir,
            ReadOnlyRoots = [_readOnlyDir]
        };

        var issues = PathGuard.EnsureGrfRepositoryIsReadOnly(profile);
        Assert.Empty(issues);
    }

    // --- Test 15: EnsureProfileIsSafe catches traversal in paths ---
    [Fact]
    public void EnsureProfileIsSafe_CatchesTraversalInPaths()
    {
        var profile = new ProfileConfig
        {
            RagnaforgeMainProjectPath = @"C:\Users\..\secret",
            RathenaPath = _writableDir,
            PatchPath = _writableDir,
            GrfRepositoryPath = _readOnlyDir,
            GrfEditorPath = _writableDir,
            WritableRoots = [_writableDir],
            ReadOnlyRoots = [_readOnlyDir]
        };

        var issues = PathGuard.EnsureProfileIsSafe(profile);
        Assert.Contains(issues, i => i.Contains("traversal"));
    }

    // --- Test 16: WritableRoot containing ReadOnlyRoot by hierarchy ---
    [Fact]
    public void EnsureProfileIsSafe_CatchesWritableContainingReadOnly()
    {
        // writable = parent, readOnly = child inside it → dangerous
        var parentDir = Path.Combine(_tempDir, "parent");
        var childDir = Path.Combine(parentDir, "child_readonly");
        Directory.CreateDirectory(parentDir);
        Directory.CreateDirectory(childDir);

        var profile = new ProfileConfig
        {
            RagnaforgeMainProjectPath = _writableDir,
            RathenaPath = _writableDir,
            PatchPath = _writableDir,
            GrfRepositoryPath = childDir,
            GrfEditorPath = _writableDir,
            WritableRoots = [_writableDir, parentDir], // parent covers child
            ReadOnlyRoots = [childDir]
        };

        var issues = PathGuard.EnsureProfileIsSafe(profile);
        Assert.Contains(issues, i => i.Contains("contains") && i.Contains("readOnlyRoot"));
    }

    // --- Test 17: ReadOnlyRoot containing WritableRoot ---
    [Fact]
    public void EnsureProfileIsSafe_CatchesReadOnlyContainingWritable()
    {
        var parentDir = Path.Combine(_tempDir, "ro_parent");
        var childDir = Path.Combine(parentDir, "wr_child");
        Directory.CreateDirectory(parentDir);
        Directory.CreateDirectory(childDir);

        var profile = new ProfileConfig
        {
            RagnaforgeMainProjectPath = _writableDir,
            RathenaPath = _writableDir,
            PatchPath = _writableDir,
            GrfRepositoryPath = parentDir,
            GrfEditorPath = _writableDir,
            WritableRoots = [_writableDir, childDir],
            ReadOnlyRoots = [parentDir] // read-only parent covers writable child
        };

        var issues = PathGuard.EnsureProfileIsSafe(profile);
        Assert.Contains(issues, i => i.Contains("contains") && i.Contains("writableRoot"));
    }

    // --- Test 18: GRF inside writable root by containment (not exact match) ---
    [Fact]
    public void EnsureProfileIsSafe_CatchesGrfInsideWritableByContainment()
    {
        var parentWritable = Path.Combine(_tempDir, "writable_parent");
        var grfChild = Path.Combine(parentWritable, "grfs_subdir");
        Directory.CreateDirectory(parentWritable);
        Directory.CreateDirectory(grfChild);

        var profile = new ProfileConfig
        {
            RagnaforgeMainProjectPath = _writableDir,
            RathenaPath = _writableDir,
            PatchPath = _writableDir,
            GrfRepositoryPath = grfChild, // child of a writable root
            GrfEditorPath = _writableDir,
            WritableRoots = [_writableDir, parentWritable],
            ReadOnlyRoots = [grfChild]
        };

        var issues = PathGuard.EnsureProfileIsSafe(profile);
        // Must detect GRF is contained inside a writable root
        Assert.Contains(issues, i =>
            i.Contains("grfRepositoryPath") && i.Contains("contained"));
    }
}
