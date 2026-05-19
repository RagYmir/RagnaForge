using System.Text;
using System.Text.Json;
using RagnaForge.Agent.Core;
using RagnaForge.Agent.Core.Runtime;
using RagnaForge.Agent.Core.Output;
using RagnaForge.Agent.Mcp.Prompts;
using RagnaForge.Agent.Mcp.Resources;
using RagnaForge.Agent.Mcp.Tools;

namespace RagnaForge.Agent.Mcp;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> Main(string[] args)
    {
        var agentRoot = AgentRootResolver.Resolve(AppContext.BaseDirectory).AgentRoot;
        var registry = new McpToolRegistry(new McpToolContext(agentRoot));
        var resources = new McpResourceRegistry(registry, agentRoot);
        var prompts = new McpPromptRegistry();

        if (args.Contains("--list-tools", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                version = AgentVersion.Current,
                tools = registry.Tools.Select(t => t.Name).OrderBy(n => n)
            }, JsonOpts));
            return 0;
        }

        if (args.Contains("--list-resources", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                version = AgentVersion.Current,
                resources = resources.ListResources()
            }, JsonOpts));
            return 0;
        }

        if (args.Contains("--list-prompts", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                version = AgentVersion.Current,
                prompts = prompts.ListPrompts()
            }, JsonOpts));
            return 0;
        }

        await RunStdioAsync(registry, resources, prompts);
        return 0;
    }

    private static async Task RunStdioAsync(
        McpToolRegistry registry,
        McpResourceRegistry resources,
        McpPromptRegistry prompts)
    {
        while (true)
        {
            var request = await ReadMessageAsync(Console.In);
            if (request is null) break;

            using var doc = JsonDocument.Parse(request);
            var response = HandleRequest(registry, resources, prompts, doc.RootElement);
            if (response is not null)
                await WriteMessageAsync(JsonSerializer.Serialize(response, JsonOpts));
        }
    }

    private static object? HandleRequest(
        McpToolRegistry registry,
        McpResourceRegistry resources,
        McpPromptRegistry prompts,
        JsonElement request)
    {
        var id = request.TryGetProperty("id", out var idProp) ? idProp.Clone() : default;
        var hasId = request.TryGetProperty("id", out _);
        var method = request.TryGetProperty("method", out var methodProp) ? methodProp.GetString() : null;

        if (!hasId && method is "notifications/initialized") return null;

        return method switch
        {
            "initialize" => Response(id, new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new { name = "RagnaForge.Agent.Mcp", version = AgentVersion.Current },
                capabilities = new { tools = new { }, resources = new { }, prompts = new { } }
            }),
            "tools/list" => Response(id, new
            {
                tools = registry.Tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    inputSchema = t.InputSchema
                }).OrderBy(t => t.name)
            }),
            "tools/call" => HandleToolCall(registry, id, request),
            "resources/list" => Response(id, new
            {
                resources = resources.ListResources()
            }),
            "resources/read" => Response(id, HandleResourceRead(resources, request)),
            "prompts/list" => Response(id, new
            {
                prompts = prompts.ListPrompts()
            }),
            "prompts/get" => Response(id, HandlePromptGet(prompts, request)),
            _ => Error(id, -32601, $"Unsupported MCP method: {method}")
        };
    }

    private static object HandleToolCall(McpToolRegistry registry, JsonElement id, JsonElement request)
    {
        if (!request.TryGetProperty("params", out var paramsElement) ||
            !paramsElement.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return Error(id, -32602, "tools/call requires params.name.");
        }

        var toolName = nameElement.GetString()!;
        var arguments = paramsElement.TryGetProperty("arguments", out var argsElement)
            ? argsElement
            : JsonDocument.Parse("{}").RootElement;

        // Path Traversal and Absolute Path Protection for ALL MCP tools
        var argumentsJson = arguments.ToString() ?? "";
        if (argumentsJson.Contains("..") || argumentsJson.Contains(":\\") || argumentsJson.Contains(":/"))
        {
            var safetyBlock = new
            {
                success = false,
                operation = toolName,
                readOnly = true,
                correlationId = JsonOutput.GenerateOperationId(),
                timestampUtc = DateTime.UtcNow.ToString("o"),
                warnings = new List<string> { "Input contained absolute paths or directory traversal attempts." },
                errors = new List<string> { "Safety policy violation: Directory traversal or absolute path is blocked." },
                data = (object?)null
            };

            return Response(id, new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = JsonSerializer.Serialize(safetyBlock, JsonOpts)
                    }
                },
                isError = true
            });
        }

        var output = registry.Execute(toolName, arguments);

        // Strict MCP read-only envelope wrapping
        var mcpEnvelope = new
        {
            success = output.Ok,
            operation = output.Mode,
            readOnly = true,
            correlationId = output.OperationId,
            timestampUtc = DateTime.UtcNow.ToString("o"),
            warnings = output.Warnings,
            errors = output.Errors,
            data = output.Data
        };

        return Response(id, new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(mcpEnvelope, JsonOpts)
                }
            },
            isError = !output.Ok
        });
    }

    private static object HandleResourceRead(McpResourceRegistry resources, JsonElement request)
    {
        if (!request.TryGetProperty("params", out var paramsElement) ||
            !paramsElement.TryGetProperty("uri", out var uriElement) ||
            uriElement.ValueKind != JsonValueKind.String)
        {
            return new
            {
                contents = new[]
                {
                    new
                    {
                        uri = string.Empty,
                        mimeType = "application/json",
                        text = JsonSerializer.Serialize(new
                        {
                            ok = false,
                            readOnly = true,
                            correlationId = JsonOutput.GenerateOperationId(),
                            errors = new[] { "resources/read requires params.uri." }
                        }, JsonOpts)
                    }
                }
            };
        }

        return resources.ReadResource(uriElement.GetString());
    }

    private static object HandlePromptGet(McpPromptRegistry prompts, JsonElement request)
    {
        var promptName = request.TryGetProperty("params", out var paramsElement) &&
                         paramsElement.TryGetProperty("name", out var nameElement) &&
                         nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;

        return prompts.GetPrompt(promptName);
    }

    private static object Response(JsonElement id, object result) => new { jsonrpc = "2.0", id, result };

    private static object Error(JsonElement id, int code, string message) => new
    {
        jsonrpc = "2.0",
        id,
        error = new { code, message }
    };

    private static async Task<string?> ReadMessageAsync(TextReader input)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var line = CleanProtocolLine(input.ReadLine());
        if (line is null) return null;

        if (line.TrimStart().StartsWith('{') || !line.Contains(':'))
            return line.Length == 0 ? null : line;

        while (!string.IsNullOrEmpty(line))
        {
            var idx = line.IndexOf(':');
            if (idx > 0)
            {
                var key = CleanProtocolLine(line[..idx])!.Trim();
                if (key.EndsWith("Content-Length", StringComparison.OrdinalIgnoreCase))
                    key = "Content-Length";
                headers[key] = line[(idx + 1)..].Trim();
            }
            line = input.ReadLine();
            if (line is null) return null;
        }

        if (!headers.TryGetValue("Content-Length", out var lenText) || !int.TryParse(lenText, out var length))
        {
            return null;
        }

        var buffer = new char[length];
        var read = 0;
        while (read < length)
        {
            var n = await input.ReadAsync(buffer.AsMemory(read, length - read));
            if (n == 0) return null;
            read += n;
        }

        var message = new string(buffer).TrimStart('\uFEFF');
        return message;
    }

    private static string? CleanProtocolLine(string? line) =>
        line?.TrimStart('\uFEFF', '\u200B', '\u2060');

    private static async Task WriteMessageAsync(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        await Console.Out.WriteAsync($"Content-Length: {payload.Length}\r\n\r\n");
        await Console.Out.WriteAsync(json);
        await Console.Out.FlushAsync();
    }

}
