using System.Collections.Generic;

namespace WController.Agent;

public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}

public class AgentMessage
{
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }
}

public class ToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = string.Empty;
}
