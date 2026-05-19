using System.Text.Json;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class ReportListTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_report_list";
    public string Description => "List all generated operation reports. Read-only.";
    public object InputSchema => SchemaFactory.Empty();

    public JsonOutput Execute(JsonElement arguments)
    {
        var output = JsonOutput.Success(Name);
        try
        {
            var reportsDir = Path.Combine(context.AgentRoot, "logs", "reports");
            var reportsList = new List<object>();

            if (Directory.Exists(reportsDir))
            {
                foreach (var file in Directory.GetFiles(reportsDir, "*.*").OrderByDescending(File.GetLastWriteTimeUtc))
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension is ".json" or ".md")
                    {
                        reportsList.Add(new
                        {
                            reportName = Path.GetFileName(file),
                            operationId = Path.GetFileNameWithoutExtension(file).Replace(".report", ""),
                            format = extension.TrimStart('.'),
                            sizeBytes = new FileInfo(file).Length,
                            createdAtUtc = File.GetLastWriteTimeUtc(file)
                        });
                    }
                }
            }

            output.Summary = $"Found {reportsList.Count} report(s).";
            output.Data = new { reports = reportsList };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error(Name, ex.Message);
        }

        return McpResponseLimiter.Limit(output);
    }
}
