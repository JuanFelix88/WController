using System.Collections.Generic;
using System.Threading;

namespace WController.Agent;

public class AgentSession
{
    public string Id { get; } = System.Guid.NewGuid().ToString("N");
    public string WorkingDirectory { get; set; } = string.Empty;
    public List<AgentMessage> Messages { get; } = new List<AgentMessage>();
    public bool IsRunning { get; set; }
    public CancellationTokenSource? CancellationSource { get; set; }

    public void AddSystemMessage(string content)
    {
        Messages.Add(new AgentMessage { Role = MessageRole.System, Content = content });
    }

    public void AddUserMessage(string content)
    {
        Messages.Add(new AgentMessage { Role = MessageRole.User, Content = content });
    }

    public void AddAssistantMessage(string content, List<ToolCall>? toolCalls = null)
    {
        Messages.Add(new AgentMessage { Role = MessageRole.Assistant, Content = content, ToolCalls = toolCalls });
    }

    public void AddToolResult(string toolCallId, string toolName, string content)
    {
        Messages.Add(new AgentMessage
        {
            Role = MessageRole.Tool,
            Content = content,
            ToolCallId = toolCallId,
            ToolName = toolName
        });
    }

    public void Stop()
    {
        CancellationSource?.Cancel();
        IsRunning = false;
    }
}
