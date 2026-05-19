using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RagnaForge.Agent.Core.Knowledge;

public sealed class KnowledgeService
{
    private readonly string _agentRoot;
    public string? LastReadOnlyIndexWarning { get; private set; }
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public KnowledgeService(string agentRoot)
    {
        if (string.IsNullOrWhiteSpace(agentRoot))
            throw new ArgumentNullException(nameof(agentRoot));

        _agentRoot = Path.GetFullPath(agentRoot);
    }

    /// <summary>
    /// Loads all registered knowledge sources from knowledge/sources/*.json.
    /// </summary>
    public List<KnowledgeSource> LoadSources()
    {
        var list = new List<KnowledgeSource>();
        var sourcesDir = Path.Combine(_agentRoot, "knowledge", "sources");
        if (!Directory.Exists(sourcesDir))
            return list;

        foreach (var file in Directory.GetFiles(sourcesDir, "*.json"))
        {
            KnowledgePathGuard.EnforceBoundary(_agentRoot, file);
            try
            {
                var content = File.ReadAllText(file);
                var source = JsonSerializer.Deserialize<KnowledgeSource>(content, JsonOptions);
                if (source != null)
                {
                    list.Add(source);
                }
            }
            catch
            {
                // Gracefully ignore corrupt files to prevent crashes
            }
        }

        return list;
    }

    /// <summary>
    /// Loads all curated knowledge packs from knowledge/packs/*.json.
    /// </summary>
    public List<KnowledgePack> LoadPacks()
    {
        var list = new List<KnowledgePack>();
        var packsDir = Path.Combine(_agentRoot, "knowledge", "packs");
        if (!Directory.Exists(packsDir))
            return list;

        foreach (var file in Directory.GetFiles(packsDir, "*.json"))
        {
            KnowledgePathGuard.EnforceBoundary(_agentRoot, file);
            try
            {
                var content = File.ReadAllText(file);
                var pack = JsonSerializer.Deserialize<KnowledgePack>(content, JsonOptions);
                if (pack != null)
                {
                    list.Add(pack);
                }
            }
            catch
            {
                // Gracefully ignore corrupt packs to prevent crashes
            }
        }

        return list;
    }

    /// <summary>
    /// Loads the consolidated index from knowledge/index/knowledge.index.json without writing.
    /// If index is missing or corrupt, builds a transient in-memory index and emits a safe warning.
    /// </summary>
    public KnowledgeIndex LoadIndexReadOnly()
    {
        LastReadOnlyIndexWarning = null;
        var indexPath = Path.Combine(_agentRoot, "knowledge", "index", "knowledge.index.json");
        KnowledgePathGuard.EnforceBoundary(_agentRoot, indexPath);
        if (File.Exists(indexPath))
        {
            KnowledgePathGuard.EnforceBoundary(_agentRoot, indexPath);
            try
            {
                var content = File.ReadAllText(indexPath);
                var index = JsonSerializer.Deserialize<KnowledgeIndex>(content, JsonOptions);
                if (index != null)
                    return index;
            }
            catch
            {
                LastReadOnlyIndexWarning = "Knowledge index could not be read; using a transient in-memory index. Run knowledge build to refresh the local controlled index.";
                return BuildIndexInMemory();
            }
        }

        LastReadOnlyIndexWarning = "Knowledge index is missing; using a transient in-memory index. Run knowledge build to persist the local controlled index.";
        return BuildIndexInMemory();
    }

    /// <summary>
    /// Builds a consolidated search index from all active packs and saves it.
    /// </summary>
    public KnowledgeIndex BuildIndex()
    {
        return BuildIndexCore(persist: true);
    }

    /// <summary>
    /// Builds a consolidated search index from all active packs without writing it.
    /// </summary>
    public KnowledgeIndex BuildIndexInMemory()
    {
        return BuildIndexCore(persist: false);
    }

    private KnowledgeIndex BuildIndexCore(bool persist)
    {
        var index = new KnowledgeIndex();
        var packs = LoadPacks();

        var topicsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tagsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entityTypesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filePatternsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceRefsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pack in packs)
        {
            foreach (var entry in pack.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Id))
                    continue;

                index.Entries[entry.Id] = entry;

                if (!string.IsNullOrWhiteSpace(entry.Topic))
                    topicsSet.Add(entry.Topic);

                foreach (var tag in entry.Tags)
                    tagsSet.Add(tag);

                foreach (var et in entry.EntityTypes)
                    entityTypesSet.Add(et);

                foreach (var fp in entry.FilePatterns)
                    filePatternsSet.Add(fp);

                foreach (var sr in entry.SourceRefs)
                    sourceRefsSet.Add(sr);
            }
        }

        index.Topics = [.. topicsSet.OrderBy(x => x)];
        index.Tags = [.. tagsSet.OrderBy(x => x)];
        index.EntityTypes = [.. entityTypesSet.OrderBy(x => x)];
        index.FilePatterns = [.. filePatternsSet.OrderBy(x => x)];
        index.SourceRefs = [.. sourceRefsSet.OrderBy(x => x)];

        if (persist)
        {
            var indexDir = Path.Combine(_agentRoot, "knowledge", "index");
            var indexPath = Path.Combine(indexDir, "knowledge.index.json");
            KnowledgePathGuard.EnforceBoundary(_agentRoot, indexDir);
            KnowledgePathGuard.EnforceBoundary(_agentRoot, indexPath);

            Directory.CreateDirectory(indexDir);
            var indexJson = JsonSerializer.Serialize(index, JsonOptions);
            File.WriteAllText(indexPath, indexJson);
        }

        return index;
    }

    /// <summary>
    /// Searches knowledge entries matching standard queries and filters.
    /// </summary>
    public List<KnowledgeResult> Search(KnowledgeQuery query)
    {
        var index = LoadIndexReadOnly();
        var results = new List<KnowledgeResult>();

        var terms = (query.Query ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToList();

        foreach (var entry in index.Entries.Values)
        {
            // Category filter
            if (!string.IsNullOrWhiteSpace(query.Category) &&
                !entry.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase))
                continue;

            // EntityType filter
            if (!string.IsNullOrWhiteSpace(query.EntityType) &&
                !entry.EntityTypes.Any(et => et.Equals(query.EntityType, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Tags filter
            if (query.Tags != null && query.Tags.Count > 0 &&
                !query.Tags.Any(t => entry.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
                continue;

            // Score matching
            double score = 0;
            var matchedTags = new List<string>();

            if (terms.Count > 0)
            {
                foreach (var term in terms)
                {
                    if (entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                        score += 15;

                    if (entry.Topic.Contains(term, StringComparison.OrdinalIgnoreCase))
                        score += 10;

                    if (entry.Id.Contains(term, StringComparison.OrdinalIgnoreCase))
                        score += 5;

                    foreach (var tag in entry.Tags)
                    {
                        if (tag.Contains(term, StringComparison.OrdinalIgnoreCase))
                        {
                            score += 8;
                            if (!matchedTags.Contains(tag)) matchedTags.Add(tag);
                        }
                    }

                    if (entry.Summary.Contains(term, StringComparison.OrdinalIgnoreCase))
                        score += 5;

                    if (entry.Details.Contains(term, StringComparison.OrdinalIgnoreCase))
                        score += 3;
                }

                // If query is specified and we got a score of 0, it means it doesn't match the query
                if (score == 0)
                    continue;
            }
            else
            {
                // No search query terms: base score on category/entityType filter presence
                score = 1.0;
            }

            results.Add(new KnowledgeResult
            {
                EntryId = entry.Id,
                Title = entry.Title,
                Summary = entry.Summary,
                Details = query.IncludeDetails ? entry.Details : null,
                Confidence = entry.Confidence,
                SourceRefs = query.IncludeSources ? entry.SourceRefs : [],
                Warnings = entry.Warnings,
                MatchedTags = matchedTags,
                Score = score
            });
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Title)
            .Take(query.Limit)
            .ToList();
    }

    /// <summary>
    /// Explains a topic or entityType using standard entries.
    /// </summary>
    public List<KnowledgeResult> Explain(string topicOrEntityType)
    {
        if (string.IsNullOrWhiteSpace(topicOrEntityType))
            return [];

        // Exact match by topic or entityType
        var q1 = new KnowledgeQuery { Query = "", Limit = 5, IncludeDetails = true, IncludeSources = true };
        var index = LoadIndexReadOnly();

        var matches = index.Entries.Values
            .Where(e => e.Topic.Equals(topicOrEntityType, StringComparison.OrdinalIgnoreCase) ||
                        e.EntityTypes.Any(et => et.Equals(topicOrEntityType, StringComparison.OrdinalIgnoreCase)) ||
                        e.Category.Equals(topicOrEntityType, StringComparison.OrdinalIgnoreCase))
            .Select(e => new KnowledgeResult
            {
                EntryId = e.Id,
                Title = e.Title,
                Summary = e.Summary,
                Details = e.Details,
                Confidence = e.Confidence,
                SourceRefs = e.SourceRefs,
                Warnings = e.Warnings,
                MatchedTags = [],
                Score = 100.0
            })
            .ToList();

        if (matches.Count > 0)
            return matches;

        // Fallback to fuzzy search query
        return Search(new KnowledgeQuery
        {
            Query = topicOrEntityType,
            Limit = 3,
            IncludeDetails = true,
            IncludeSources = true
        });
    }

    /// <summary>
    /// Retrieves a single knowledge entry by ID.
    /// </summary>
    public KnowledgeEntry? GetEntry(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var index = LoadIndexReadOnly();
        return index.Entries.TryGetValue(id, out var entry) ? entry : null;
    }

    /// <summary>
    /// Performs static validation check on knowledge packs.
    /// Checks for duplicate IDs, missing source refs, invalid JSON files, and confidence.
    /// </summary>
    public List<string> ValidatePacks()
    {
        var issues = new List<string>();
        var sources = LoadSources().Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var packs = LoadPacks();
        var uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pack in packs)
        {
            if (string.IsNullOrWhiteSpace(pack.Id))
                issues.Add($"Pack name '{pack.Name}' has an empty or null pack ID.");

            foreach (var entry in pack.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Id))
                {
                    issues.Add($"Entry '{entry.Title}' in pack '{pack.Name}' has an empty ID.");
                    continue;
                }

                if (!uniqueIds.Add(entry.Id))
                {
                    issues.Add($"Duplicate entry ID detected: '{entry.Id}' is defined multiple times.");
                }

                if (string.IsNullOrWhiteSpace(entry.Title))
                    issues.Add($"Entry '{entry.Id}' has an empty title.");

                if (string.IsNullOrWhiteSpace(entry.Summary))
                    issues.Add($"Entry '{entry.Id}' has an empty summary.");

                if (entry.SourceIds.Count == 0)
                {
                    issues.Add($"Entry '{entry.Id}' does not reference any knowledge sources.");
                }
                else
                {
                    foreach (var sId in entry.SourceIds)
                    {
                        if (!sources.Contains(sId))
                        {
                            issues.Add($"Entry '{entry.Id}' references an undefined source ID: '{sId}'.");
                        }
                    }
                }

                var allowedConfidence = new[] { "authoritative", "informative", "unverified" };
                if (!allowedConfidence.Contains(entry.Confidence.ToLowerInvariant()))
                {
                    issues.Add($"Entry '{entry.Id}' has invalid confidence: '{entry.Confidence}'. Allowed: authoritative, informative, unverified.");
                }
            }
        }

        return issues;
    }
}
