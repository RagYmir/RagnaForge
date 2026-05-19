using System.Text.Json;
using RagnaForge.Agent.Core.Configuration;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Tests for ConfigFingerprint — ensures fingerprint changes when config changes.
/// </summary>
public class ConfigFingerprintTests
{
    private static PathsConfig CreatePathsConfig(
        string rathenaPath = @"E:\rathena",
        string patchPath = @"E:\patch",
        string grfPath = @"E:\grfs",
        string activeProfile = "test")
    {
        return new PathsConfig
        {
            AgentRoot = @"C:\Agent",
            ActiveProfile = activeProfile,
            Profiles = new Dictionary<string, ProfileConfig>
            {
                [activeProfile] = new()
                {
                    RagnaforgeMainProjectPath = @"C:\Project",
                    RathenaPath = rathenaPath,
                    PatchPath = patchPath,
                    GrfRepositoryPath = grfPath,
                    GrfEditorPath = @"C:\GrfEditor",
                    WritableRoots = [@"C:\Agent", @"C:\Project", rathenaPath, patchPath],
                    ReadOnlyRoots = [grfPath]
                }
            }
        };
    }

    private static SafetyConfig CreateSafetyConfig() => new()
    {
        BlockOriginalGrfWrite = true,
        BlockLubEditing = true,
        RequireDryRunBeforeApply = true,
        RequireDiffBeforeApply = true,
        RequireExplicitConfirmation = true,
        CacheMustMatchActiveProfile = true,
        InvalidateCacheOnPathChange = true
    };

    // --- Test: Fingerprint changes when rathenaPath changes ---
    [Fact]
    public void Fingerprint_ChangesWhenRathenaPathChanges()
    {
        var safety = CreateSafetyConfig();
        var fp1 = ConfigFingerprint.Generate(CreatePathsConfig(rathenaPath: @"E:\rathena"), safety);
        var fp2 = ConfigFingerprint.Generate(CreatePathsConfig(rathenaPath: @"E:\rathena_v2"), safety);

        Assert.NotEqual(fp1, fp2);
    }

    // --- Test: Fingerprint changes when patchPath changes ---
    [Fact]
    public void Fingerprint_ChangesWhenPatchPathChanges()
    {
        var safety = CreateSafetyConfig();
        var fp1 = ConfigFingerprint.Generate(CreatePathsConfig(patchPath: @"E:\patch"), safety);
        var fp2 = ConfigFingerprint.Generate(CreatePathsConfig(patchPath: @"E:\patch_v2"), safety);

        Assert.NotEqual(fp1, fp2);
    }

    // --- Test: Fingerprint changes when grfRepositoryPath changes ---
    [Fact]
    public void Fingerprint_ChangesWhenGrfPathChanges()
    {
        var safety = CreateSafetyConfig();
        var fp1 = ConfigFingerprint.Generate(CreatePathsConfig(grfPath: @"E:\grfs"), safety);
        var fp2 = ConfigFingerprint.Generate(CreatePathsConfig(grfPath: @"E:\grfs_v2"), safety);

        Assert.NotEqual(fp1, fp2);
    }

    // --- Test: Same config produces same fingerprint ---
    [Fact]
    public void Fingerprint_IsDeterministic()
    {
        var paths = CreatePathsConfig();
        var safety = CreateSafetyConfig();

        var fp1 = ConfigFingerprint.Generate(paths, safety);
        var fp2 = ConfigFingerprint.Generate(paths, safety);

        Assert.Equal(fp1, fp2);
    }

    // --- Test: Fingerprint changes with different active profile name ---
    [Fact]
    public void Fingerprint_ChangesWhenProfileNameChanges()
    {
        var safety = CreateSafetyConfig();
        var fp1 = ConfigFingerprint.Generate(CreatePathsConfig(activeProfile: "test"), safety);
        var fp2 = ConfigFingerprint.Generate(CreatePathsConfig(activeProfile: "prod"), safety);

        Assert.NotEqual(fp1, fp2);
    }

    // --- Test: Fingerprint is a hex string ---
    [Fact]
    public void Fingerprint_IsHexString()
    {
        var fp = ConfigFingerprint.Generate(CreatePathsConfig(), CreateSafetyConfig());

        Assert.NotEmpty(fp);
        Assert.Equal(64, fp.Length); // SHA-256 = 64 hex chars
        Assert.True(fp.All(c => "0123456789abcdef".Contains(c)));
    }
}
