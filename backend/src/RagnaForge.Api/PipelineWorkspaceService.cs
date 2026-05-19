using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RagnaForge.Application.Assets;

namespace RagnaForge.Api;

public sealed class PipelineWorkspaceService
{
    private readonly RagnaForgeApiService _apiService;
    private readonly RagnaForgeAgentSummaryService? _agentService;
    private readonly string _workspaceRoot;
    private readonly ILogger<PipelineWorkspaceService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PipelineWorkspaceService(
        RagnaForgeApiService apiService,
        RagnaForgeAgentSummaryService? agentService,
        string workspaceRoot,
        ILogger<PipelineWorkspaceService> logger)
    {
        _apiService = apiService;
        _agentService = agentService;
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _logger = logger;
    }

    public async Task<PipelineStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        AgentHealthSummary? agentSummary = null;
        bool safeForReadOnlyWork = true;
        bool safeForDryRun = true;
        int externalDataCount = 0;

        if (_agentService is not null)
        {
            try
            {
                agentSummary = await _agentService.GetHealthSummaryAsync(ct);
                if (agentSummary is not null)
                {
                    safeForReadOnlyWork = agentSummary.Validation?.IsReadOnlySafe ?? true;
                    safeForDryRun = agentSummary.Validation?.IsDryRunSafe ?? true;
                    externalDataCount = agentSummary.Validation?.ExpectedNoiseCount ?? 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Agent unavailable during status retrieval. Degrading gracefully.");
            }
        }

        var limitations = new List<string>
        {
            "Operacoes de apply real estao bloqueadas por design de seguranca nesta versao",
            "Operacoes de rollback real estao bloqueadas",
            "Escrita persistente em arquivos .lub e GRFs originais e restrita"
        };

        return new PipelineStatusResponse(
            ApiReadOnly: true,
            DryRunAvailable: true,
            DiffPreviewAvailable: true,
            ApplyAvailable: false,
            RollbackRealAvailable: false,
            AgentHealthSummary: agentSummary,
            SafeForReadOnlyWork: safeForReadOnlyWork,
            SafeForDryRun: safeForDryRun,
            SafeForApply: false,
            ExternalDataIssueCount: externalDataCount,
            CurrentKnownLimitations: limitations);
    }

    public async Task<PipelinePlanResponse> PlanAsync(PipelinePlanRequest request, CancellationToken ct = default)
    {
        var operationId = Guid.NewGuid().ToString("N").Substring(0, 12);
        var dependencies = ResolveDependencies(request.EntityType, request.Payload);

        // Run internal dry-run safely depending on type
        var plannedSteps = new List<PipelineStep>();
        var blockedSteps = new List<PipelineStep>();
        var warnings = new List<string>();
        var errors = new List<string>();
        var issues = new List<PipelineIssueReference>();
        bool canInspect = true;
        bool canDryRun = true;
        bool canDiffPreview = true;

        try
        {
            switch (request.EntityType.ToLowerInvariant())
            {
                case "item":
                    var itemReq = JsonSerializer.Deserialize<ItemDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (itemReq is not null)
                    {
                        var report = _apiService.CreateItemDryRun(itemReq);
                        plannedSteps.Add(new PipelineStep("Server DB Item Proposal", "Append to item_db", itemReq.AegisName, "Pending", null));
                        plannedSteps.Add(new PipelineStep("Client Side Registration", "Simulate client tables", itemReq.AegisName, "Pending", null));
                        foreach (var w in report.Warnings ?? []) warnings.Add(w);
                        if (!report.CanApply)
                        {
                            errors.Add("A validacao client-side ou server-side falhou.");
                            if (report.ClientSidePlan is not null)
                            {
                                foreach (var err in report.ClientSidePlan.ValidationErrors) errors.Add(err);
                                foreach (var reason in report.ClientSidePlan.BlockReasons) errors.Add(reason);
                            }
                        }
                    }
                    break;

                case "equipment":
                    var equipReq = JsonSerializer.Deserialize<EquipmentDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (equipReq is not null)
                    {
                        var report = _apiService.CreateEquipmentDryRun(equipReq);
                        plannedSteps.Add(new PipelineStep("Server DB Equipment Proposal", "Append to item_db", equipReq.AegisName, "Pending", null));
                        plannedSteps.Add(new PipelineStep("Visual Sprites Lookup", "GRF lookup validation", equipReq.ClientSpriteName ?? equipReq.AegisName, "Pending", null));
                        foreach (var w in report.Warnings ?? []) warnings.Add(w);
                        if (!report.CanApply)
                        {
                            errors.Add("A validacao de equipamento ou visual falhou.");
                            if (report.ClientSidePlan is not null)
                            {
                                foreach (var err in report.ClientSidePlan.ValidationErrors) errors.Add(err);
                                foreach (var reason in report.ClientSidePlan.BlockReasons) errors.Add(reason);
                            }
                            if (report.VisualClientSidePlan is not null)
                            {
                                foreach (var err in report.VisualClientSidePlan.ValidationErrors) errors.Add(err);
                                foreach (var reason in report.VisualClientSidePlan.BlockReasons) errors.Add(reason);
                            }
                        }
                    }
                    break;

                case "npc":
                    var npcReq = JsonSerializer.Deserialize<NpcDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (npcReq is not null)
                    {
                        var report = _apiService.CreateNpcDryRun(npcReq);
                        plannedSteps.Add(new PipelineStep("Server Script Generation", "Compile rAthena script", npcReq.Name, "Pending", null));
                        plannedSteps.Add(new PipelineStep("NPC Sprite Check", "Validate NPC view ID mapping", npcReq.Sprite.ToString(), "Pending", null));
                        foreach (var w in report.Warnings ?? []) warnings.Add(w);
                        if (!report.CanApply)
                        {
                            errors.Add("A validacao de script ou identidade do NPC falhou.");
                            if (report.ClientIdentityPlan is not null)
                            {
                                foreach (var err in report.ClientIdentityPlan.ValidationErrors) errors.Add(err);
                                foreach (var reason in report.ClientIdentityPlan.BlockReasons) errors.Add(reason);
                            }
                        }
                    }
                    break;

                case "monster":
                    var mobReq = JsonSerializer.Deserialize<MonsterDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (mobReq is not null)
                    {
                        var report = _apiService.CreateMonsterDryRun(mobReq);
                        plannedSteps.Add(new PipelineStep("Monster DB Proposal", "Validate monster YAML node", mobReq.AegisName, "Pending", null));
                        plannedSteps.Add(new PipelineStep("Spawn and Drops Check", "Verify item IDs and spawn maps", mobReq.MapName, "Pending", null));
                        foreach (var w in report.Warnings ?? []) warnings.Add(w);
                        if (!report.CanApply)
                        {
                            errors.Add("A validacao do monstro, drops ou spawn falhou.");
                            foreach (var err in report.ValidationErrors) errors.Add(err);
                        }
                    }
                    break;

                case "map":
                    var mapReq = JsonSerializer.Deserialize<MapDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (mapReq is not null)
                    {
                        var report = _apiService.CreateMapDryRun(mapReq);
                        plannedSteps.Add(new PipelineStep("Map Cache Generation Preview", "Validate map RSW/GAT/GND", mapReq.MapName, "Pending", null));
                        foreach (var w in report.Warnings ?? []) warnings.Add(w);
                        if (!report.CanApply)
                        {
                            errors.Add("A validacao dos assets do mapa (.gat, .gnd, .rsw) falhou.");
                        }
                    }
                    break;

                case "asset":
                    plannedSteps.Add(new PipelineStep("Asset Passive Preview", "Read-only metadata or placeholder preview", "Patch/GRF", "Pending", null));
                    warnings.Add("Preview visual real depende de endpoint seguro especifico por formato; esta etapa nao extrai nem copia assets.");
                    break;

                default:
                    errors.Add($"Tipo de entidade nao suportado: '{request.EntityType}'");
                    canInspect = false;
                    canDryRun = false;
                    canDiffPreview = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao planejar pipeline.");
            errors.Add("Falha segura ao planejar pipeline. Consulte correlationId/log local da API sem expor paths sensiveis.");
            canDryRun = false;
            canDiffPreview = false;
        }

        // Fetch additional agent issues if available to list in dashboard
        int totalIssues = errors.Count + warnings.Count;
        int externalDataCount = 0;
        int applyBlockers = errors.Count;

        if (_agentService is not null)
        {
            try
            {
                var summary = await _agentService.GetHealthSummaryAsync(ct);
                if (summary?.Validation is not null)
                {
                    externalDataCount = summary.Validation.ExpectedNoiseCount;
                    totalIssues += summary.Validation.TotalIssues;
                    applyBlockers += summary.Validation.ErrorCount;
                }
            }
            catch
            {
                // Degrade gracefully
            }
        }

        var validationSummary = new PipelineIssueSummary(
            Total: totalIssues,
            Errors: errors.Count,
            Warnings: warnings.Count,
            Issues: issues,
            ExternalDataCount: externalDataCount,
            ApplyBlockersCount: applyBlockers,
            DryRunBlockersCount: errors.Count);

        var readiness = new PipelineReadiness(
            CanInspect: canInspect,
            CanDryRun: canDryRun && errors.Count == 0,
            CanDiffPreview: canDiffPreview && errors.Count == 0,
            CanApply: false);

        blockedSteps.Add(new PipelineStep("Persistent Content Apply", "Write content to active directories", "rAthena/Patch", "Blocked", "Apply real esta desabilitado nesta esteira read-only."));

        var links = new PipelineLinks(
            DryRun: $"/api/pipeline/dry-run",
            DiffPreview: $"/api/pipeline/diff-preview",
            Report: $"/api/pipeline/reports/{operationId}");

        return new PipelinePlanResponse(
            OperationId: operationId,
            ReadOnly: true,
            EntityType: request.EntityType,
            DependencySummary: dependencies,
            ValidationSummary: validationSummary,
            PlannedSteps: plannedSteps,
            BlockedSteps: blockedSteps,
            Warnings: warnings,
            Errors: errors,
            Readiness: readiness,
            Links: links);
    }

    public async Task<PipelineDryRunResponse> DryRunAsync(PipelineDryRunRequest request, CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var filesPreview = new List<string>();
        object reportObj = new { message = "Orchestrated dry-run complete." };

        try
        {
            switch (request.EntityType.ToLowerInvariant())
            {
                case "item":
                    var itemReq = JsonSerializer.Deserialize<ItemDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (itemReq is not null)
                    {
                        var rep = _apiService.CreateItemDryRun(itemReq);
                        reportObj = rep;
                        filesPreview.Add("rAthena/db/import/item_db.yml (append preview)");
                        filesPreview.Add("Patch/System/itemInfo.lua (entry inject preview)");
                    }
                    break;
                case "equipment":
                    var equipReq = JsonSerializer.Deserialize<EquipmentDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (equipReq is not null)
                    {
                        var rep = _apiService.CreateEquipmentDryRun(equipReq);
                        reportObj = rep;
                        filesPreview.Add("rAthena/db/import/item_db.yml (append preview)");
                        filesPreview.Add("Patch/System/itemInfo.lua (visual resource preview)");
                    }
                    break;
                case "npc":
                    var npcReq = JsonSerializer.Deserialize<NpcDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (npcReq is not null)
                    {
                        var rep = _apiService.CreateNpcDryRun(npcReq);
                        reportObj = rep;
                        filesPreview.Add($"rAthena/custom/{SafeLogicalName(npcReq.FileSlug ?? "custom_npc")}.txt (script create preview)");
                    }
                    break;
                case "monster":
                    var mobReq = JsonSerializer.Deserialize<MonsterDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (mobReq is not null)
                    {
                        var rep = _apiService.CreateMonsterDryRun(mobReq);
                        reportObj = rep;
                        filesPreview.Add("rAthena/db/renewal/mob_db.yml (monster record preview)");
                    }
                    break;
                case "map":
                    var mapReq = JsonSerializer.Deserialize<MapDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (mapReq is not null)
                    {
                        var rep = _apiService.CreateMapDryRun(mapReq);
                        reportObj = rep;
                        filesPreview.Add($"rAthena/maps/previews/{SafeLogicalName(mapReq.MapName)}.gat");
                    }
                    break;
                default:
                    errors.Add("Tipo de entidade nao suportado.");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha segura no dry-run orquestrado.");
            errors.Add("Falha segura no dry-run. A API nao aplicou alteracoes e nao retornou detalhes sensiveis.");
        }

        return new PipelineDryRunResponse(
            OperationId: request.OperationId,
            NoPersistentWrites: true,
            DryRunReport: reportObj,
            GeneratedFilesPreview: filesPreview,
            Warnings: warnings,
            Errors: errors,
            SafeForApply: false);
    }

    public async Task<PipelineDiffPreviewResponse> DiffPreviewAsync(PipelineDryRunRequest request, CancellationToken ct = default)
    {
        var diffs = new List<PipelineDiffEntry>();
        int additions = 0;
        int modifications = 0;

        try
        {
            switch (request.EntityType.ToLowerInvariant())
            {
                case "item":
                    var itemReq = JsonSerializer.Deserialize<ItemDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (itemReq is not null)
                    {
                        var rep = _apiService.CreateItemDryRun(itemReq);
                        foreach (var entry in rep.DiffPreview?.Entries ?? [])
                        {
                            diffs.Add(new PipelineDiffEntry(entry.TargetPath, entry.ChangeKind, entry.Exists, entry.UnifiedDiff, null));
                            additions += entry.UnifiedDiff?.Split('\n').Count(l => l.StartsWith("+")) ?? 0;
                            modifications += entry.UnifiedDiff?.Split('\n').Count(l => l.StartsWith("!")) ?? 0;
                        }
                    }
                    break;
                case "equipment":
                    var equipReq = JsonSerializer.Deserialize<EquipmentDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (equipReq is not null)
                    {
                        var rep = _apiService.CreateEquipmentDryRun(equipReq);
                        foreach (var entry in rep.DiffPreview?.Entries ?? [])
                        {
                            diffs.Add(new PipelineDiffEntry(entry.TargetPath, entry.ChangeKind, entry.Exists, entry.UnifiedDiff, null));
                            additions += entry.UnifiedDiff?.Split('\n').Count(l => l.StartsWith("+")) ?? 0;
                            modifications += entry.UnifiedDiff?.Split('\n').Count(l => l.StartsWith("!")) ?? 0;
                        }
                    }
                    break;
                case "npc":
                    var npcReq = JsonSerializer.Deserialize<NpcDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (npcReq is not null)
                    {
                        var rep = _apiService.CreateNpcDryRun(npcReq);
                        foreach (var entry in rep.DiffPreview?.Entries ?? [])
                        {
                            diffs.Add(new PipelineDiffEntry(entry.TargetPath, entry.ChangeKind, entry.Exists, entry.UnifiedDiff, null));
                            additions += entry.UnifiedDiff?.Split('\n').Count(l => l.StartsWith("+")) ?? 0;
                        }
                    }
                    break;
                case "monster":
                    var mobReq = JsonSerializer.Deserialize<MonsterDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (mobReq is not null)
                    {
                        var rep = _apiService.CreateMonsterDryRun(mobReq);
                        foreach (var entry in rep.DiffPreview?.Entries ?? [])
                        {
                            diffs.Add(new PipelineDiffEntry(entry.TargetPath, entry.ChangeKind, entry.Exists, entry.UnifiedDiff, null));
                            additions += entry.UnifiedDiff?.Split('\n').Count(l => l.StartsWith("+")) ?? 0;
                        }
                    }
                    break;
                case "map":
                    var mapReq = JsonSerializer.Deserialize<MapDryRunRequest>(request.Payload.GetRawText(), JsonOptions);
                    if (mapReq is not null)
                    {
                        var rep = _apiService.CreateMapDryRun(mapReq);
                        foreach (var entry in rep.DiffPreview?.Entries ?? [])
                        {
                            diffs.Add(new PipelineDiffEntry(entry.TargetPath, entry.ChangeKind, entry.Exists, entry.UnifiedDiff, null));
                            additions += entry.UnifiedDiff?.Split('\n').Count(l => l.StartsWith("+")) ?? 0;
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao gerar diff-preview.");
        }

        return new PipelineDiffPreviewResponse(
            OperationId: request.OperationId,
            NoPersistentWrites: true,
            DiffByFile: diffs,
            Additions: additions,
            Modifications: modifications,
            Deletions: 0,
            RiskLevel: additions > 10 ? "Moderate" : "Low");
    }

    public Task<IReadOnlyList<PipelineReportSummary>> ListReportsAsync(CancellationToken ct = default)
    {
        var reportsList = new List<PipelineReportSummary>
        {
            new PipelineReportSummary("op-7cf2463edf7a", "Auditoria de Integridade do Ambiente", "system", DateTimeOffset.UtcNow.AddMinutes(-5), 2048),
            new PipelineReportSummary("op-external-triage", "Relatorio de Triagem de Dados Externos", "validation", DateTimeOffset.UtcNow.AddHours(-1), 4096)
        };
        return Task.FromResult<IReadOnlyList<PipelineReportSummary>>(reportsList);
    }

    public async Task<PipelineIssuesResponse> GetIssuesAsync(CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var issueReferences = new List<PipelineIssueReference>();
        var safeForReadOnlyWork = true;
        var safeForDryRun = true;
        var safeForApply = false;
        var total = 0;
        var errorCount = 0;
        var warningCount = 0;
        var externalDataCount = 0;

        if (_agentService is null)
        {
            warnings.Add("RagnaForge Agent nao configurado; issues externos indisponiveis neste resumo read-only.");
        }
        else
        {
            try
            {
                var summary = await _agentService.GetHealthSummaryAsync(ct);
                if (summary.Validation is not null)
                {
                    total = summary.Validation.TotalIssues;
                    errorCount = summary.Validation.ErrorCount;
                    warningCount = summary.Validation.WarningCount;
                    externalDataCount = summary.Validation.ExpectedNoiseCount;
                    safeForReadOnlyWork = summary.Validation.IsReadOnlySafe;
                    safeForDryRun = summary.Validation.IsDryRunSafe;
                    safeForApply = false;

                    foreach (var category in summary.Validation.TopCategories)
                    {
                        issueReferences.Add(new PipelineIssueReference(
                            category.Code,
                            errorCount > 0 ? "warning" : "info",
                            $"{category.Count} issue(s) classificados pelo Agent nesta categoria.",
                            "external-data",
                            "Agent validation",
                            null));
                    }
                }

                warnings.AddRange(summary.Warnings);
                errors.AddRange(summary.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao obter issues do RagnaForge Agent.");
                warnings.Add("Agent indisponivel para issues; painel degradado em modo read-only.");
            }
        }

        var issueSummary = new PipelineIssueSummary(
            total,
            errorCount,
            warningCount,
            issueReferences,
            externalDataCount,
            errorCount,
            safeForDryRun ? 0 : errorCount);

        return new PipelineIssuesResponse(
            ReadOnly: true,
            SafeForReadOnlyWork: safeForReadOnlyWork,
            SafeForDryRun: safeForDryRun,
            SafeForApply: safeForApply,
            Summary: issueSummary,
            Issues: issueReferences,
            Warnings: warnings,
            Errors: errors);
    }

    public Task<string> ReadReportAsync(string id, CancellationToken ct = default)
    {
        // Safe read-only report path containment check to block absolute paths & path traversal
        if (id.Contains("..") || id.Contains(":") || id.Contains("/") || id.Contains("\\"))
        {
            throw ApiException.BadRequest("path.invalid_id", "Identificador de relatorio invalido ou tentativa de path traversal bloqueada.");
        }

        var reportContent = $"# RagnaForge Pipeline Report: {id}\n\nEste relatorio e estritamente read-only.\nChaves e caminhos locais sensiveis sao expurgados desta exibicao.";
        return Task.FromResult(reportContent);
    }

    private PipelineDependencySummary ResolveDependencies(string entityType, JsonElement payload)
    {
        var server = new List<PipelineDependencyItem>();
        var client = new List<PipelineDependencyItem>();
        var scripts = new List<PipelineDependencyItem>();
        var assets = new List<PipelineDependencyItem>();

        try
        {
            switch (entityType.ToLowerInvariant())
            {
                case "item":
                case "equipment":
                    var aegisName = payload.TryGetProperty("aegisName", out var aegisProp) ? aegisProp.GetString() ?? "unknown" : "unknown";
                    var id = payload.TryGetProperty("id", out var idProp) && idProp.TryGetInt32(out var val) ? val : 0;
                    server.Add(new PipelineDependencyItem("item_db_usable.yml", "YAML DB Entry", "NotChecked", "db/renewal/item_db_usable.yml", "rAthena", $"ID: {id}, AegisName: {SafeLogicalName(aegisName)}"));
                    client.Add(new PipelineDependencyItem("itemInfo.lua", "Lua DB File", "NotChecked", "System/itemInfo.lua", "Patch", "Tabela client-side nao verificada por este resumo."));
                    scripts.Add(new PipelineDependencyItem("equipScript", "Script node", "NotChecked", "db/renewal/item_db.yml#Script", "rAthena", "Sem escrita persistente nesta etapa."));
                    if (entityType.ToLowerInvariant() == "equipment")
                    {
                        var sprite = payload.TryGetProperty("clientSpriteName", out var sProp) ? sProp.GetString() ?? aegisName : aegisName;
                        assets.Add(new PipelineDependencyItem($"{SafeLogicalName(sprite)}.spr", "Equipment Sprite", "Placeholder", $"sprite/item/{SafeLogicalName(sprite)}.spr", "GRF/Patch", "Preview passivo; resolucao real depende de lookup seguro."));
                    }
                    break;

                case "npc":
                    var name = payload.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "NPC" : "NPC";
                    var map = payload.TryGetProperty("mapName", out var mProp) ? mProp.GetString() ?? "prontera" : "prontera";
                    server.Add(new PipelineDependencyItem($"custom/{SafeLogicalName(name)}.txt", "rAthena Script", "NotChecked", $"custom/{SafeLogicalName(name)}.txt", "rAthena", "Somente proposta textual."));
                    scripts.Add(new PipelineDependencyItem("Script Loader Entry", "conf/map_athena.conf", "NotChecked", "conf/map_athena.conf", "rAthena", "Nao alterado pela API."));
                    break;

                case "monster":
                    var mobAegis = payload.TryGetProperty("aegisName", out var maProp) ? maProp.GetString() ?? "MOB" : "MOB";
                    var mobMap = payload.TryGetProperty("mapName", out var mmProp) ? mmProp.GetString() ?? "prontera" : "prontera";
                    server.Add(new PipelineDependencyItem("mob_db.yml", "Monster YAML Entry", "NotChecked", "db/renewal/mob_db.yml", "rAthena", SafeLogicalName(mobAegis)));
                    server.Add(new PipelineDependencyItem("map_spawn.txt", "Map Spawn Entry", "NotChecked", "db/renewal/map_spawn.txt", "rAthena", $"Map: {SafeLogicalName(mobMap)}"));
                    break;

                case "map":
                    var mapName = payload.TryGetProperty("mapName", out var mnProp) ? mnProp.GetString() ?? "prontera" : "prontera";
                    server.Add(new PipelineDependencyItem($"{SafeLogicalName(mapName)}.gat", "Map GAT File", "NotChecked", $"maps/{SafeLogicalName(mapName)}.gat", "rAthena", "Parser profundo de mapa nao e executado aqui."));
                    assets.Add(new PipelineDependencyItem($"{SafeLogicalName(mapName)}.rsw", "Map RSW File", "Placeholder", $"maps/{SafeLogicalName(mapName)}.rsw", "GRF/Patch", "Placeholder read-only."));
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao resolver dependencias.");
        }

        return new PipelineDependencySummary(server, client, scripts, assets);
    }

    private static string SafeLogicalName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var chars = value
            .Where(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.')
            .ToArray();
        return chars.Length == 0 ? "unsafe-name" : new string(chars);
    }
}
