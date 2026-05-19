using RagnaForge.Agent.Core.Logging;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Tests for AgentLogger — verifies category validation and traversal protection.
/// Uses temporary directories only.
/// </summary>
public class AgentLoggerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AgentLogger _logger;

    public AgentLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_logger_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _logger = new AgentLogger(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // --- Category validation: known categories ---
    [Theory]
    [InlineData("agent")]
    [InlineData("operations")]
    [InlineData("validations")]
    [InlineData("diffs")]
    [InlineData("rollbacks")]
    public void IsCategorySafe_AcceptsKnownCategories(string category)
    {
        Assert.True(AgentLogger.IsCategorySafe(category));
    }

    // --- Category validation: case-insensitive ---
    [Theory]
    [InlineData("Agent")]
    [InlineData("OPERATIONS")]
    [InlineData("Validations")]
    public void IsCategorySafe_IsCaseInsensitive(string category)
    {
        Assert.True(AgentLogger.IsCategorySafe(category));
    }

    // --- Category validation: unknown categories ---
    [Theory]
    [InlineData("unknown")]
    [InlineData("custom")]
    [InlineData("")]
    [InlineData("  ")]
    public void IsCategorySafe_RejectsUnknownCategories(string category)
    {
        Assert.False(AgentLogger.IsCategorySafe(category));
    }

    // --- Category validation: path traversal ---
    [Theory]
    [InlineData("..")]
    [InlineData(@"..\secret")]
    [InlineData("agent/../../escape")]
    [InlineData(@"agent\..\..\escape")]
    [InlineData("agent/subdir")]
    [InlineData(@"agent\subdir")]
    public void IsCategorySafe_RejectsTraversalAndSeparators(string category)
    {
        Assert.False(AgentLogger.IsCategorySafe(category));
    }

    // --- Log: rejects unsafe category ---
    [Fact]
    public void Log_ThrowsOnUnsafeCategory()
    {
        Assert.Throws<ArgumentException>(() =>
            _logger.Log("..", new { test = true }));
    }

    // --- Log: rejects category with traversal ---
    [Fact]
    public void Log_ThrowsOnTraversalCategory()
    {
        Assert.Throws<ArgumentException>(() =>
            _logger.Log(@"..\escape", new { test = true }));
    }

    // --- Log: rejects unknown category ---
    [Fact]
    public void Log_ThrowsOnUnknownCategory()
    {
        Assert.Throws<ArgumentException>(() =>
            _logger.Log("custom_category", new { test = true }));
    }

    // --- Log: accepts known category and writes file ---
    [Fact]
    public void Log_WritesFileForKnownCategory()
    {
        _logger.Log("agent", new { test = true, message = "test entry" });

        var logDir = Path.Combine(_tempDir, "logs", "agent");
        Assert.True(Directory.Exists(logDir));

        var files = Directory.GetFiles(logDir, "*.json");
        Assert.Single(files);
    }

    // --- EnsureDirectories creates all subdirs ---
    [Fact]
    public void EnsureDirectories_CreatesAllSubdirectories()
    {
        _logger.EnsureDirectories();

        foreach (var category in AgentLogger.AllowedCategories)
        {
            var dir = Path.Combine(_tempDir, "logs", category);
            Assert.True(Directory.Exists(dir), $"Directory not created: {dir}");
        }
    }
}
