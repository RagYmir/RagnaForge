using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RagnaForge.Agent.Core.Commands;
using RagnaForge.Agent.Core.Knowledge;
using RagnaForge.Agent.Mcp.Tools;
using Xunit;

namespace RagnaForge.Agent.Core.Tests;

public sealed class KnowledgeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _agentRoot;

    public KnowledgeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_knowledge_tests_{Guid.NewGuid():N}");
        _agentRoot = _tempDir;
        Directory.CreateDirectory(_agentRoot);

        // Setup baseline directories
        Directory.CreateDirectory(Path.Combine(_agentRoot, "knowledge", "sources"));
        Directory.CreateDirectory(Path.Combine(_agentRoot, "knowledge", "packs"));
        Directory.CreateDirectory(Path.Combine(_agentRoot, "knowledge", "index"));

        // Provision a dummy source
        var dummySource = """
        {
          "id": "rathena-test",
          "name": "rAthena Test Source",
          "sourceType": "GitHubRepository",
          "url": "https://github.com/rathena/rathena",
          "license": "GPL-3.0",
          "permissionNote": "Test permission note.",
          "lastReviewedUtc": "2026-05-19T00:00:00Z",
          "trustLevel": "authoritative",
          "allowedUse": "documentation references",
          "notes": "Test notes."
        }
        """;
        File.WriteAllText(Path.Combine(_agentRoot, "knowledge", "sources", "rathena-test.source.json"), dummySource);

        // Provision a dummy pack
        var dummyPack = """
        {
          "id": "rathena-test-pack",
          "name": "rAthena Test Pack",
          "version": "1.0",
          "description": "Test Pack Description.",
          "sourceIds": ["rathena-test"],
          "generatedBy": "RagnaForge Test",
          "generatedAtUtc": "2026-05-19T00:00:00Z",
          "entries": [
            {
              "id": "rathena.test.item_db",
              "title": "Item Database Reference",
              "category": "item",
              "topic": "item database",
              "summary": "This details the item database.",
              "details": "Details of item_db mapping on server side.",
              "appliesTo": "item_db.yml",
              "entityTypes": ["item"],
              "filePatterns": ["**/item_db.yml"],
              "sourceIds": ["rathena-test"],
              "sourceRefs": ["https://github.com/rathena/rathena"],
              "confidence": "authoritative",
              "warnings": ["Warning about duplicate item IDs."],
              "relatedEntries": [],
              "tags": ["item", "yaml"],
              "version": "1.0",
              "lastReviewedUtc": "2026-05-19T00:00:00Z"
            },
            {
              "id": "rathena.test.map_trio",
              "title": "Map Trio",
              "category": "map",
              "topic": "map dependencies",
              "summary": "This details map dependencies.",
              "details": "Map relies on rsw, gnd, gat.",
              "appliesTo": "client maps",
              "entityTypes": ["map"],
              "filePatterns": ["data/*.rsw"],
              "sourceIds": ["rathena-test"],
              "sourceRefs": ["https://github.com/rathena/rathena"],
              "confidence": "authoritative",
              "warnings": [],
              "relatedEntries": [],
              "tags": ["map", "trio"],
              "version": "1.0",
              "lastReviewedUtc": "2026-05-19T00:00:00Z"
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(_agentRoot, "knowledge", "packs", "rathena-test-pack.v1.json"), dummyPack);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void KnowledgeService_LoadsSources()
    {
        var service = new KnowledgeService(_agentRoot);
        var sources = service.LoadSources();
        Assert.Single(sources);
        Assert.Equal("rathena-test", sources[0].Id);
    }

    [Fact]
    public void KnowledgeService_LoadsPacks()
    {
        var service = new KnowledgeService(_agentRoot);
        var packs = service.LoadPacks();
        Assert.Single(packs);
        Assert.Equal("rathena-test-pack", packs[0].Id);
        Assert.Equal(2, packs[0].Entries.Count);
    }

    [Fact]
    public void KnowledgeService_SearchFindsItemDb()
    {
        DeleteIndex();
        var service = new KnowledgeService(_agentRoot);
        var query = new KnowledgeQuery { Query = "item database" };
        var results = service.Search(query);
        Assert.NotEmpty(results);
        var first = results[0];
        Assert.Equal("rathena.test.item_db", first.EntryId);
        Assert.Contains("item", first.MatchedTags);
        Assert.False(File.Exists(IndexPath), "Search must not persist knowledge/index/knowledge.index.json.");
        Assert.NotNull(service.LastReadOnlyIndexWarning);
    }

    [Fact]
    public void KnowledgeService_SearchFindsMapDependencies()
    {
        var service = new KnowledgeService(_agentRoot);
        var query = new KnowledgeQuery { Query = "map dependencies" };
        var results = service.Search(query);
        Assert.NotEmpty(results);
        Assert.Equal("rathena.test.map_trio", results[0].EntryId);
    }

    [Fact]
    public void KnowledgeService_ExplainReturnsSourceRefs()
    {
        DeleteIndex();
        var service = new KnowledgeService(_agentRoot);
        var results = service.Explain("map dependencies");
        Assert.NotEmpty(results);
        Assert.Equal("rathena.test.map_trio", results[0].EntryId);
        Assert.NotEmpty(results[0].SourceRefs);
        Assert.False(File.Exists(IndexPath), "Explain must not persist knowledge/index/knowledge.index.json.");
    }

    [Fact]
    public void KnowledgePathGuard_BlocksEscape()
    {
        Assert.True(KnowledgePathGuard.IsWithinBoundary(_agentRoot, Path.Combine(_agentRoot, "knowledge", "packs", "test.json")));
        Assert.False(KnowledgePathGuard.IsWithinBoundary(_agentRoot, Path.Combine(_agentRoot, "..", "escape.json")));
        Assert.Throws<UnauthorizedAccessException>(() => KnowledgePathGuard.EnforceBoundary(_agentRoot, Path.Combine(_agentRoot, "..", "escape.json")));
    }

    [Fact]
    public void KnowledgePathGuard_BlocksPrefixAttackAndSanitizesError()
    {
        var root = Path.Combine(_tempDir, "safe");
        var target = Path.Combine(_tempDir, "safe_evil", "knowledge.index.json");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        Assert.False(KnowledgePathGuard.IsWithinBoundary(root, target));
        var ex = Assert.Throws<UnauthorizedAccessException>(() => KnowledgePathGuard.EnforceBoundary(root, target));
        Assert.DoesNotContain(root, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(target, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KnowledgeService_ValidateCatchesDuplicateId()
    {
        // Add a pack containing duplicate ID
        var duplicatePack = """
        {
          "id": "duplicate-pack",
          "name": "Duplicate Pack",
          "entries": [
            {
              "id": "rathena.test.item_db",
              "title": "Duplicate Item Db",
              "summary": "Dup summary.",
              "confidence": "authoritative",
              "sourceIds": ["rathena-test"]
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(_agentRoot, "knowledge", "packs", "duplicate-pack.json"), duplicatePack);

        var service = new KnowledgeService(_agentRoot);
        var issues = service.ValidatePacks();
        Assert.Contains(issues, i => i.Contains("Duplicate entry ID detected"));
    }

    [Fact]
    public void KnowledgeService_ValidateCatchesMissingSource()
    {
        // Add a pack containing missing source reference
        var missingSourcePack = """
        {
          "id": "missing-source-pack",
          "name": "Missing Source Pack",
          "entries": [
            {
              "id": "entry.with.missing.source",
              "title": "Missing Source",
              "summary": "Summary.",
              "confidence": "authoritative",
              "sourceIds": ["non-existent-source-id"]
            }
          ]
        }
        """;
        File.WriteAllText(Path.Combine(_agentRoot, "knowledge", "packs", "missing-source-pack.json"), missingSourcePack);

        var service = new KnowledgeService(_agentRoot);
        var issues = service.ValidatePacks();
        Assert.Contains(issues, i => i.Contains("references an undefined source ID"));
    }

    [Fact]
    public void KnowledgeService_DoesNotWriteExternalFiles()
    {
        var service = new KnowledgeService(_agentRoot);
        var index = service.BuildIndex();

        var indexPath = Path.Combine(_agentRoot, "knowledge", "index", "knowledge.index.json");
        Assert.True(File.Exists(indexPath));

        // Ensure index cannot be saved outside boundary
        Assert.Throws<UnauthorizedAccessException>(() => KnowledgePathGuard.EnforceBoundary(_agentRoot, "C:\\Windows\\hacked.json"));
    }

    [Fact]
    public void KnowledgeService_BuildWritesOnlyControlledIndex()
    {
        DeleteIndex();
        var service = new KnowledgeService(_agentRoot);
        var index = service.BuildIndex();

        Assert.Equal(2, index.Entries.Count);
        Assert.True(File.Exists(IndexPath));
        Assert.True(KnowledgePathGuard.IsWithinBoundary(Path.Combine(_agentRoot, "knowledge", "index"), IndexPath));
    }

    [Fact]
    public void KnowledgeService_ValidateDoesNotWriteIndex()
    {
        DeleteIndex();
        var service = new KnowledgeService(_agentRoot);

        var issues = service.ValidatePacks();

        Assert.Empty(issues);
        Assert.False(File.Exists(IndexPath));
    }

    [Fact]
    public void KnowledgeMcpSearchDoesNotWriteIndex()
    {
        DeleteIndex();
        var tool = new KnowledgeSearchTool(new McpToolContext(_agentRoot));
        using var doc = JsonDocument.Parse(@"{""query"":""item_db""}");

        var output = tool.Execute(doc.RootElement);

        Assert.True(output.Ok);
        Assert.False(File.Exists(IndexPath));
        Assert.Contains(output.Warnings, warning => warning.Contains("transient in-memory index", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KnowledgeCommand_RejectsLargeQueryAndId()
    {
        var longQuery = new string('a', 513);
        var search = new KnowledgeCommand(Path.Combine(_agentRoot, "config"), _agentRoot, "search", new() { ["query"] = longQuery }).Execute();
        Assert.False(search.Ok);
        Assert.Contains("maximum length", search.Errors[0], StringComparison.OrdinalIgnoreCase);

        var longId = new string('b', 129);
        var entry = new KnowledgeCommand(Path.Combine(_agentRoot, "config"), _agentRoot, "entry", new() { ["id"] = longId }).Execute();
        Assert.False(entry.Ok);
        Assert.Contains("maximum length", entry.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KnowledgeCommand_RejectsPathLikeInputAndInvalidEntity()
    {
        var search = new KnowledgeCommand(Path.Combine(_agentRoot, "config"), _agentRoot, "search", new() { ["query"] = "..\\secret" }).Execute();
        Assert.False(search.Ok);
        Assert.Contains("path-like", search.Errors[0], StringComparison.OrdinalIgnoreCase);

        var schema = new KnowledgeCommand(Path.Combine(_agentRoot, "config"), _agentRoot, "schema", new() { ["entity"] = "item;apply" }).Execute();
        Assert.False(schema.Ok);
        Assert.Contains("Unsupported entity type", schema.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KnowledgeCommand_ErrorDoesNotLeakAbsolutePath()
    {
        var command = new KnowledgeCommand(Path.Combine(_agentRoot, "config"), _agentRoot, "entry", new() { ["id"] = "C:\\secret\\entry" });

        var output = command.Execute();

        Assert.False(output.Ok);
        Assert.DoesNotContain(_agentRoot, string.Join(' ', output.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", string.Join(' ', output.Errors), StringComparison.OrdinalIgnoreCase);
    }

    private string IndexPath => Path.Combine(_agentRoot, "knowledge", "index", "knowledge.index.json");

    private void DeleteIndex()
    {
        if (File.Exists(IndexPath))
        {
            File.Delete(IndexPath);
        }
    }
}
