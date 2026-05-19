using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RagnaForge.Api;

namespace RagnaForge.Tests;

public static class PipelineRealPayloadBatteryTests
{
    private static readonly string FixturesPath = Path.Combine(AppContext.BaseDirectory, "../../../Fixtures/PipelinePayloads");

    public static async Task RunAllTests()
    {
        Console.WriteLine("Starting Pipeline Real Payload Battery Tests...");
        Environment.SetEnvironmentVariable("RAGNAFORGE_API_KEY", "test-key");

        using var workspace = TempWorkspace.Create();
        SetupValidWorkspace(workspace.Root);
        Environment.SetEnvironmentVariable("RagnaForge__WorkspaceRoot", workspace.Root);
        Environment.SetEnvironmentVariable("RagnaForge__Agent__AgentExePath", Path.Combine(workspace.Root, "missing-agente-setimmo.exe"));
        Environment.SetEnvironmentVariable("RagnaForge__Agent__AgentCacheDir", Path.Combine(workspace.Root, "agent-cache"));

        await using var factory = new WebApplicationFactory<RagnaForgeApiOptions>()
            .WithWebHostBuilder(builder =>
            {
                var apiRoot = Path.Combine(AppContext.BaseDirectory, "../../../../../src/RagnaForge.Api");
                if (Directory.Exists(apiRoot))
                {
                    builder.UseContentRoot(apiRoot);
                }

                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["RagnaForge:WorkspaceRoot"] = workspace.Root,
                        ["RagnaForge:Agent:AgentExePath"] = Path.Combine(workspace.Root, "missing-agente-setimmo.exe"),
                        ["RagnaForge:Agent:AgentCacheDir"] = Path.Combine(workspace.Root, "agent-cache")
                    });
                });
            });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-RagnaForge-Api-Key", "test-key");

        await Battery_ValidPayloads_PlanSucceeds(client);
        await Battery_InvalidPayloads_Returns422(client);
        await Battery_PathTraversal_IsSafelyRejected(client);
        await Battery_CommandInjection_IsSafelyHandled(client);
        await Battery_LargePayload_IsHandledSafely(client);
        await Battery_OversizedPayload_IsRejected(client);
        await Battery_DryRunAndDiffPreview_AreReadOnlyAndSafe(client);
        await Battery_DiffPreview_InvalidOperation_IsStatelessAndSafe(client);
        await Battery_IssuesAndReports_AreReadOnlyAndPathSafe(client);
        await Battery_Knowledge_SearchExplainAndSchema_AreReadOnly(client);
        await Battery_Knowledge_PathLikeQuery_IsRejected(client);
        await Battery_Knowledge_SchemaInvalidEntity_IsRejected(client);
        await Battery_SecurityEndpoints_DoNotExist(client);
        await Battery_PipelineStatus_ExposesSafetyFlags(client);
        await Battery_Concurrency_IsStable(client);
        await Battery_Knowledge_ConcurrentSearches_AreStable(client);
        await Battery_Repetition_Loop_IsStable(client);

        Console.WriteLine("Pipeline Real Payload Battery Tests PASS.");
    }

    private static async Task<JsonElement> LoadFixture(string filename)
    {
        var path = Path.Combine(FixturesPath, filename);
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static void SetupValidWorkspace(string root)
    {
        var rathena = Path.Combine(root, "rathena");
        var patch = Path.Combine(root, "patch");
        var grfs = Path.Combine(root, "grfs");
        var grfEditor = Path.Combine(root, "grfeditor");

        Directory.CreateDirectory(Path.Combine(rathena, "db", "import"));
        Directory.CreateDirectory(Path.Combine(rathena, "conf"));
        Directory.CreateDirectory(Path.Combine(patch, "data"));
        Directory.CreateDirectory(grfs);
        Directory.CreateDirectory(grfEditor);

        File.WriteAllText(Path.Combine(rathena, "db", "import", "map_index.txt"), "");
        File.WriteAllText(Path.Combine(rathena, "conf", "maps_athena.conf"), "");
        File.WriteAllText(Path.Combine(rathena, "db", "import", "item_db.yml"), "Header:\n  Type: ITEM_DB\n");

        Directory.CreateDirectory(Path.Combine(root, "data", "manifests"));
        var manifestJson = $$"""
        {
          "SchemaVersion": "1.0",
          "Paths": {
            "RathenaPath": "{{rathena.Replace("\\", "\\\\")}}",
            "PatchPath": "{{patch.Replace("\\", "\\\\")}}",
            "GrfRepositoryPath": "{{grfs.Replace("\\", "\\\\")}}",
            "GrfEditorPath": "{{grfEditor.Replace("\\", "\\\\")}}"
          },
          "EpisodeProfile": {
            "Name": "test",
            "Mode": "Renewal",
            "ClientDate": null,
            "Notes": "Pipeline battery workspace"
          }
        }
        """;
        File.WriteAllText(Path.Combine(root, "data", "manifests", "repositories.local.json"), manifestJson);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static JsonElement UnwrapData(JsonElement root) =>
        root.TryGetProperty("data", out var data) ? data : root;

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        return UnwrapData(root);
    }

    private static void AssertNoSensitiveLeak(string text, string context)
    {
        var forbidden = new[]
        {
            "stackTrace",
            "System.IO.",
            "DirectoryNotFoundException",
            "FileNotFoundException",
            "C:\\Users\\",
            "C:/Users/",
            "C:\\Windows\\",
            "C:/Windows/",
            "Desktop\\Ragna_Forge",
            "Desktop/Ragna_Forge",
            "Agente_Setimmo"
        };

        foreach (var marker in forbidden)
        {
            Assert(!text.Contains(marker, StringComparison.OrdinalIgnoreCase), $"{context} leaked forbidden marker '{marker}'. Text: {text}");
        }
    }

    private static async Task Battery_ValidPayloads_PlanSucceeds(HttpClient client)
    {
        var validFixtures = new[] {
            "item_consumable_valid.json",
            "equipment_weapon_valid.json",
            "equipment_armor_valid.json",
            "visual_asset_valid.json",
            "npc_simple_valid.json",
            "monster_simple_valid.json",
            "map_existing_valid.json",
            "payload_unicode_valid.json",
            "payload_casing_variants.json",
            "payload_external_data_warning.json",
            "payload_knowledge_hint_case.json"
        };

        foreach (var fixture in validFixtures)
        {
            var payload = await LoadFixture(fixture);
            var entityType = payload.TryGetProperty("entityType", out var e) ? e.GetString() : "item";
            var requestBody = new { entityType = entityType, mode = "inspect", payload = payload };
            var response = await client.PostAsJsonAsync("/api/pipeline/plan", requestBody);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Expected OK for {fixture}, got {response.StatusCode}. Body: {errorBody}");
            }

            var text = await response.Content.ReadAsStringAsync();
            AssertNoSensitiveLeak(text, $"Valid fixture {fixture}");
            Assert(!text.Contains("Falha segura", StringComparison.OrdinalIgnoreCase), $"Valid fixture must not trigger safe failure path: {fixture}");
            using var doc = JsonDocument.Parse(text);
            var plan = UnwrapData(doc.RootElement);
            Assert(plan.GetProperty("readOnly").GetBoolean(), $"Valid fixture plan must stay read-only: {fixture}");
            Assert(!plan.GetProperty("readiness").GetProperty("canApply").GetBoolean(), $"Valid fixture must not become apply-ready in API: {fixture}");
        }
    }

    private static async Task Battery_InvalidPayloads_Returns422(HttpClient client)
    {
        var fixtures = new[] {
            "payload_invalid_missing_required.json",
            "payload_invalid_entity_type.json"
        };

        foreach (var fixture in fixtures)
        {
            var payload = await LoadFixture(fixture);
            var entityType = payload.TryGetProperty("entityType", out var e) ? e.GetString() : "item";
            var requestBody = new { entityType = entityType, mode = "inspect", payload = payload };
            var response = await client.PostAsJsonAsync("/api/pipeline/plan", requestBody);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var root = await response.Content.ReadFromJsonAsync<JsonElement>();
                var plan = root.TryGetProperty("data", out var d) ? d : root;
                var planJsonStr = plan.ToString();

                var hasErrors = plan.TryGetProperty("errors", out var err) && err.GetArrayLength() > 0;
                var hasIssues = plan.TryGetProperty("issues", out var iss) && iss.GetArrayLength() > 0;
                var hasWarnings = plan.TryGetProperty("warnings", out var warns) && warns.GetArrayLength() > 0;

                Assert(hasErrors || hasIssues || hasWarnings, $"Invalid payload should return validation errors/warnings in the plan. Plan: {planJsonStr}");
            }
            else
            {
                Assert(response.StatusCode == HttpStatusCode.UnprocessableEntity || response.StatusCode == HttpStatusCode.BadRequest, $"Expected 422/400 or Plan errors for {fixture}, got {response.StatusCode}");
            }
        }
    }

    private static async Task Battery_PathTraversal_IsSafelyRejected(HttpClient client)
    {
        var payload = await LoadFixture("payload_invalid_path_traversal.json");
        var requestBody = new { entityType = "item", mode = "inspect", payload = payload };
        var response = await client.PostAsJsonAsync("/api/pipeline/plan", requestBody);

        Assert(response.StatusCode == HttpStatusCode.UnprocessableEntity || response.StatusCode == HttpStatusCode.OK, "Should be safely handled");
        var text = await response.Content.ReadAsStringAsync();
        AssertNoSensitiveLeak(text, "Traversal payload");
    }

    private static async Task Battery_CommandInjection_IsSafelyHandled(HttpClient client)
    {
        var payload = await LoadFixture("payload_invalid_command_injection.json");
        var requestBody = new { entityType = "item", mode = "inspect", payload = payload };
        var response = await client.PostAsJsonAsync("/api/pipeline/plan", requestBody);

        Assert(response.StatusCode == HttpStatusCode.UnprocessableEntity || response.StatusCode == HttpStatusCode.OK, "Should be safely handled");
        var text = await response.Content.ReadAsStringAsync();
        AssertNoSensitiveLeak(text, "Command injection payload");
        Assert(!text.Contains("nt authority", StringComparison.OrdinalIgnoreCase), "Command injection payload must not execute whoami");
        Assert(!text.Contains("root:x:", StringComparison.OrdinalIgnoreCase), "Command injection payload must not execute shell commands");
    }

    private static async Task Battery_LargePayload_IsHandledSafely(HttpClient client)
    {
        var payload = await LoadFixture("payload_large_but_allowed.json");
        var requestBody = new { entityType = "item", mode = "inspect", payload = payload };
        var response = await client.PostAsJsonAsync("/api/pipeline/plan", requestBody);
        Assert(response.StatusCode == HttpStatusCode.OK, "Large payload should be OK");
    }

    private static async Task Battery_OversizedPayload_IsRejected(HttpClient client)
    {
        var largeString = new string('A', 5 * 1024 * 1024);
        var payload = new { entityType = "item", name = largeString };
        var requestBody = new { entityType = "item", mode = "inspect", payload = payload };
        var response = await client.PostAsJsonAsync("/api/pipeline/plan", requestBody);
        Assert(response.StatusCode == HttpStatusCode.RequestEntityTooLarge || response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.UnprocessableEntity, "Oversized should be rejected");
        var text = await response.Content.ReadAsStringAsync();
        AssertNoSensitiveLeak(text, "Oversized payload");
    }

    private static async Task Battery_DryRunAndDiffPreview_AreReadOnlyAndSafe(HttpClient client)
    {
        var payload = await LoadFixture("item_consumable_valid.json");
        var requestBody = new { entityType = "item", mode = "inspect", payload = payload };

        var planResp = await client.PostAsJsonAsync("/api/pipeline/plan", requestBody);
        var planJson = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var root = planJson.TryGetProperty("data", out var d) ? d : planJson;
        var operationId = root.GetProperty("operationId").GetString();

        var dryRunResponse = await client.PostAsJsonAsync("/api/pipeline/dry-run", new { operationId = operationId, entityType = "item", payload = payload });
        if (dryRunResponse.StatusCode != HttpStatusCode.OK)
        {
            var err = await dryRunResponse.Content.ReadAsStringAsync();
            throw new Exception($"Dry run failed: {dryRunResponse.StatusCode} {err} (used opId: {operationId})");
        }

        var dryRunJson = await ReadDataAsync(dryRunResponse);
        Assert(dryRunJson.GetProperty("noPersistentWrites").GetBoolean(), "Dry-run must report NoPersistentWrites=true");
        Assert(!dryRunJson.GetProperty("safeForApply").GetBoolean(), "Dry-run must report SafeForApply=false");

        var diffPreviewResponse = await client.PostAsJsonAsync("/api/pipeline/diff-preview", new { operationId = operationId, entityType = "item", payload = payload });
        if (diffPreviewResponse.StatusCode != HttpStatusCode.OK)
        {
            var err = await diffPreviewResponse.Content.ReadAsStringAsync();
            throw new Exception($"Diff preview failed: {diffPreviewResponse.StatusCode} {err}");
        }

        var diffJson = await diffPreviewResponse.Content.ReadAsStringAsync();
        AssertNoSensitiveLeak(diffJson, "Diff-preview");
        using var diffDoc = JsonDocument.Parse(diffJson);
        var diffData = UnwrapData(diffDoc.RootElement);
        Assert(diffData.GetProperty("noPersistentWrites").GetBoolean(), "Diff-preview must report NoPersistentWrites=true");
    }

    private static async Task Battery_DiffPreview_InvalidOperation_IsStatelessAndSafe(HttpClient client)
    {
        var payload = await LoadFixture("item_consumable_valid.json");
        var response = await client.PostAsJsonAsync(
            "/api/pipeline/diff-preview",
            new { operationId = "missing-operation-id", entityType = "item", payload = payload });

        Assert(response.StatusCode == HttpStatusCode.OK, $"Stateless diff-preview should safely handle unknown operationId. Got: {response.StatusCode}");
        var text = await response.Content.ReadAsStringAsync();
        AssertNoSensitiveLeak(text, "Stateless diff-preview");
        using var doc = JsonDocument.Parse(text);
        var data = UnwrapData(doc.RootElement);
        Assert(data.GetProperty("operationId").GetString() == "missing-operation-id", "Diff-preview should echo operationId only as a safe logical id");
        Assert(data.GetProperty("noPersistentWrites").GetBoolean(), "Stateless diff-preview must remain read-only");
    }

    private static async Task Battery_IssuesAndReports_AreReadOnlyAndPathSafe(HttpClient client)
    {
        var issuesResponse = await client.GetAsync("/api/pipeline/issues");
        Assert(issuesResponse.StatusCode == HttpStatusCode.OK, $"Issues endpoint should return OK. Got: {issuesResponse.StatusCode}");
        var issuesText = await issuesResponse.Content.ReadAsStringAsync();
        AssertNoSensitiveLeak(issuesText, "Issues endpoint");
        using (var issuesDoc = JsonDocument.Parse(issuesText))
        {
            var issues = UnwrapData(issuesDoc.RootElement);
            Assert(issues.GetProperty("readOnly").GetBoolean(), "Issues endpoint must report readOnly=true");
            Assert(!issues.GetProperty("safeForApply").GetBoolean(), "Issues endpoint must report safeForApply=false");
            Assert(issues.GetProperty("summary").TryGetProperty("externalDataCount", out _), "Issues endpoint must expose external-data summary");
        }

        var reportsResponse = await client.GetAsync("/api/pipeline/reports");
        Assert(reportsResponse.StatusCode == HttpStatusCode.OK, $"Reports endpoint should return OK. Got: {reportsResponse.StatusCode}");
        var reportsText = await reportsResponse.Content.ReadAsStringAsync();
        AssertNoSensitiveLeak(reportsText, "Reports endpoint");

        var reportResponse = await client.GetAsync("/api/pipeline/reports/op-7cf2463edf7a");
        Assert(reportResponse.StatusCode == HttpStatusCode.OK, $"Report read endpoint should return OK. Got: {reportResponse.StatusCode}");
        var reportText = await reportResponse.Content.ReadAsStringAsync();
        Assert(reportText.Contains("read-only", StringComparison.OrdinalIgnoreCase), "Report read endpoint must make read-only state explicit");
        AssertNoSensitiveLeak(reportText, "Report read endpoint");

        var traversalReportResponse = await client.GetAsync("/api/pipeline/reports/%2e%2e%2fsecret");
        Assert(
            traversalReportResponse.StatusCode == HttpStatusCode.BadRequest
            || traversalReportResponse.StatusCode == HttpStatusCode.UnprocessableEntity
            || traversalReportResponse.StatusCode == HttpStatusCode.NotFound,
            $"Report traversal must be blocked or not routed. Got: {traversalReportResponse.StatusCode}");
        var traversalText = await traversalReportResponse.Content.ReadAsStringAsync();
        AssertNoSensitiveLeak(traversalText, "Report traversal");

        var absoluteReportResponse = await client.GetAsync("/api/pipeline/reports/C%3A%5CWindows%5Cwin.ini");
        Assert(
            absoluteReportResponse.StatusCode == HttpStatusCode.BadRequest
            || absoluteReportResponse.StatusCode == HttpStatusCode.UnprocessableEntity
            || absoluteReportResponse.StatusCode == HttpStatusCode.NotFound,
            $"Report absolute path must be blocked or not routed. Got: {absoluteReportResponse.StatusCode}");
        var absoluteText = await absoluteReportResponse.Content.ReadAsStringAsync();
        AssertNoSensitiveLeak(absoluteText, "Report absolute path");
    }


    private static async Task Battery_SecurityEndpoints_DoNotExist(HttpClient client)
    {
        var applyResponse = await client.PostAsync("/api/apply", null);
        Assert(applyResponse.StatusCode == HttpStatusCode.NotFound, "Apply should not exist");

        var rollbackResponse = await client.PostAsync("/api/rollback", null);
        Assert(rollbackResponse.StatusCode == HttpStatusCode.NotFound, "Rollback should not exist");
    }

    private static async Task Battery_PipelineStatus_ExposesSafetyFlags(HttpClient client)
    {
        var response = await client.GetAsync("/api/pipeline/status");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"Status failed: {response.StatusCode} {err}");
        }
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        var json = root.TryGetProperty("data", out var d) ? d : root;
        Assert(!json.GetProperty("applyAvailable").GetBoolean(), "applyAvailable should be false");
        Assert(!json.GetProperty("rollbackRealAvailable").GetBoolean(), "rollbackRealAvailable should be false");
        Assert(json.GetProperty("apiReadOnly").GetBoolean(), "apiReadOnly should be true");
    }

    private static async Task Battery_Concurrency_IsStable(HttpClient client)
    {
        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            var p = new { entityType = "item", aegisName = $"Test_Concurrency_{i}" };
            var requestBody = new { entityType = "item", mode = "inspect", payload = p };
            var response = await client.PostAsJsonAsync("/api/pipeline/plan", requestBody);
            return response.StatusCode;
        });

        var results = await Task.WhenAll(tasks);
        Assert(results.All(c => c == HttpStatusCode.OK), "Concurrency should be OK");
    }

    private static async Task Battery_Knowledge_ConcurrentSearches_AreStable(HttpClient client)
    {
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var response = await client.GetAsync($"/api/knowledge/search?q=item_{i}");
            var text = await response.Content.ReadAsStringAsync();
            AssertNoSensitiveLeak(text, "Knowledge concurrency response");
            return response.StatusCode;
        });

        var results = await Task.WhenAll(tasks);
        Assert(
            results.All(c => c == HttpStatusCode.OK || c == HttpStatusCode.TooManyRequests),
            "Knowledge concurrency should be OK or hit the structured rate-limit guard");
    }

    private static async Task Battery_Knowledge_SearchExplainAndSchema_AreReadOnly(HttpClient client)
    {
        var search = await client.GetAsync("/api/knowledge/search?q=item_db");
        Assert(search.StatusCode == HttpStatusCode.OK, $"Knowledge search should be OK. Got: {search.StatusCode}");
        AssertNoSensitiveLeak(await search.Content.ReadAsStringAsync(), "Knowledge search");

        var explain = await client.GetAsync("/api/knowledge/explain?topic=map%20dependencies");
        Assert(explain.StatusCode == HttpStatusCode.OK, $"Knowledge explain should be OK. Got: {explain.StatusCode}");
        AssertNoSensitiveLeak(await explain.Content.ReadAsStringAsync(), "Knowledge explain");

        var schema = await client.GetAsync("/api/knowledge/schema/item");
        Assert(schema.StatusCode == HttpStatusCode.OK, $"Knowledge schema should be OK. Got: {schema.StatusCode}");
        AssertNoSensitiveLeak(await schema.Content.ReadAsStringAsync(), "Knowledge schema");
    }

    private static async Task Battery_Repetition_Loop_IsStable(HttpClient client)
    {
        for (int i = 0; i < 25; i++)
        {
            var payload = new { entityType = "npc", mapName = $"map_{i}" };
            var requestBody = new { entityType = "npc", mode = "inspect", payload = payload };
            var planResp = await client.PostAsJsonAsync("/api/pipeline/plan", requestBody);
            Assert(planResp.StatusCode == HttpStatusCode.OK || planResp.StatusCode == HttpStatusCode.UnprocessableEntity || planResp.StatusCode == HttpStatusCode.TooManyRequests, $"Repetition plan OK/422/429. Got: {planResp.StatusCode}");
            if (planResp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                AssertNoSensitiveLeak(await planResp.Content.ReadAsStringAsync(), "Rate-limit response");
            }

            var statusResp = await client.GetAsync("/api/pipeline/status");
            if (statusResp.StatusCode != HttpStatusCode.OK && statusResp.StatusCode != HttpStatusCode.TooManyRequests)
            {
                var err = await statusResp.Content.ReadAsStringAsync();
                throw new Exception($"Repetition status failed: {statusResp.StatusCode} {err}");
            }
        }
    }

    private static async Task Battery_Knowledge_PathLikeQuery_IsRejected(HttpClient client)
    {
        var response = await client.GetAsync("/api/knowledge/search?q=../item_db");
        Assert(response.StatusCode == HttpStatusCode.UnprocessableEntity || response.StatusCode == HttpStatusCode.BadRequest, $"Path-like query should be rejected. Got: {response.StatusCode}");
        AssertNoSensitiveLeak(await response.Content.ReadAsStringAsync(), "Knowledge path-like query");
    }

    private static async Task Battery_Knowledge_SchemaInvalidEntity_IsRejected(HttpClient client)
    {
        var response = await client.GetAsync("/api/knowledge/schema/invalid_entity");
        Assert(response.StatusCode == HttpStatusCode.UnprocessableEntity || response.StatusCode == HttpStatusCode.BadRequest, $"Invalid entity should be rejected. Got: {response.StatusCode}");
        AssertNoSensitiveLeak(await response.Content.ReadAsStringAsync(), "Knowledge invalid schema");
    }
}
