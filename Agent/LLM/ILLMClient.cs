using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WController.Agent.LLM;

public class LLMResponse
{
    public string Content { get; set; } = string.Empty;
    public List<ToolCall>? ToolCalls { get; set; }
    public bool IsToolCall => ToolCalls != null && ToolCalls.Count > 0;
}

public interface ILLMClient : IDisposable
{
    Task<LLMResponse> SendAsync(
        List<AgentMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken ct);

    Task StreamAsync(
        List<AgentMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        Action<string> onTextDelta,
        Action<LLMResponse> onComplete,
        CancellationToken ct);

    string ModelName { get; }
}
