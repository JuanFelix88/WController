using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WController.Agent;

public class ToolResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class ToolDefinition
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required ToolParameter[] Parameters { get; set; }
}

public class ToolParameter
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Description { get; set; }
    public bool Required { get; set; } = true;
}

public interface IAgentTool
{
    string Name { get; }
    ToolDefinition Definition { get; }
    Task<ToolResult> ExecuteAsync(Dictionary<string, string> arguments, CancellationToken ct);
}
