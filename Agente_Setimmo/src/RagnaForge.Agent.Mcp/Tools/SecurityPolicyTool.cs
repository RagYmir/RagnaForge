using System.Text.Json;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Safety;

namespace RagnaForge.Agent.Mcp.Tools;

public sealed class SecurityPolicyTool(McpToolContext context) : IMcpTool
{
    public string Name => "ragnaforge_security_policy";
    public string Description => "Return Agente Setimmo security policy constraints and safety guarantees. Read-only.";
    public object InputSchema => SchemaFactory.Empty();

    public JsonOutput Execute(JsonElement arguments)
    {
        var output = JsonOutput.Success(Name);
        try
        {
            var safetyPath = Path.Combine(context.ConfigDir, "safety.json");
            object? safetyConfig = null;

            if (File.Exists(safetyPath))
            {
                safetyConfig = JsonSerializer.Deserialize<object>(File.ReadAllText(safetyPath));
            }

            output.Summary = "Security Policy loaded successfully. Real apply and rollback operations are strictly BLOCKED.";
            output.Data = new
            {
                readOnlyMode = true,
                writeOperationsPermitted = false,
                applyBlocked = true,
                rollbackRealBlocked = true,
                blockOriginalGrfWrite = true,
                blockLubEditing = true,
                mcpEnforcedRules = new[]
                {
                    "No shell command execution allowed",
                    "Path traversal is strictly blocked (PathGuard containment)",
                    "No apply/rollback execute commands registered as tools",
                    "Response payload size protection enabled"
                },
                safetyConfig
            };
        }
        catch (Exception ex)
        {
            output = JsonOutput.Error(Name, ex.Message);
        }

        return McpResponseLimiter.Limit(output);
    }
}
