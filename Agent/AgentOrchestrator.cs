using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WController.Agent.LLM;

namespace WController.Agent;

public class AgentOrchestrator
{
    private const int MaxIterations = 40;

    private static readonly string SystemPrompt = @"You are a highly capable coding agent running inside WController on Windows.
You have access to tools for reading files, writing files, searching across the codebase, listing directories, and running CLI commands.

## Operating principles
- Always read files before modifying them to understand existing code.
- Make precise, minimal edits. Do not over-engineer.
- When running commands, prefer cmd.exe unless the user requests bash.
- For complex multi-step tasks, think step by step and use tools iteratively.
- Report results concisely after completing tasks.
- If you encounter errors, diagnose and fix them rather than giving up.
- When writing code, follow the existing style and conventions in the project.
- You can run multiple tool calls in parallel when they are independent.

## Tool usage guidelines
- read_file: Use to inspect existing code before changes. Prefer reading larger ranges.
- write_file: Use to create or modify files. Always provide complete file content.
- search_codebase: Use to find relevant code. Use regex for flexible matching.
- list_directory: Use to explore project structure.
- run_command: Use to execute build commands, tests, git operations, etc.

## Response format
- Think about the task first, then act with tools.
- After completing the task, provide a clear summary of what was done.
- Use markdown in your responses for readability.
";

    private readonly ToolRegistry toolRegistry;
    private readonly ILLMClient llmClient;
    private readonly AgentSession session;

    public event Action<string>? OnTextDelta;
    public event Action<string>? OnToolStart;
    public event Action<string, ToolResult>? OnToolComplete;
    public event Action<string>? OnError;
    public event Action? OnComplete;
    public event Action? OnThinking;

    public AgentOrchestrator(ILLMClient llmClient, AgentSession session, ToolRegistry toolRegistry)
    {
        this.llmClient = llmClient;
        this.session = session;
        this.toolRegistry = toolRegistry;
    }

    public void InitializeSession()
    {
        if (session.Messages.Count == 0)
        {
            session.AddSystemMessage(SystemPrompt);
        }
    }

    public async Task RunAsync(string userMessage, CancellationToken ct)
    {
        session.IsRunning = true;
        session.AddUserMessage(userMessage);

        try
        {
            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                ct.ThrowIfCancellationRequested();

                OnThinking?.Invoke();

                var tools = toolRegistry.GetDefinitions();
                var response = await StreamResponse(tools, ct).ConfigureAwait(false);

                if (response.IsToolCall)
                {
                    session.AddAssistantMessage(response.Content, response.ToolCalls);

                    var toolTasks = new List<Task>();
                    foreach (var toolCall in response.ToolCalls!)
                    {
                        ct.ThrowIfCancellationRequested();

                        var tool = toolRegistry.GetTool(toolCall.Name);
                        if (tool == null)
                        {
                            session.AddToolResult(toolCall.Id, toolCall.Name, $"Error: Unknown tool '{toolCall.Name}'");
                            continue;
                        }

                        OnToolStart?.Invoke($"Running {toolCall.Name}...");

                        Dictionary<string, string> args;
                        try
                        {
                            args = ParseArguments(toolCall.ArgumentsJson);
                        }
                        catch (Exception ex)
                        {
                            session.AddToolResult(toolCall.Id, toolCall.Name, $"Error parsing arguments: {ex.Message}");
                            OnToolComplete?.Invoke(toolCall.Name, new ToolResult { Success = false, Error = ex.Message });
                            continue;
                        }

                        var result = await tool.ExecuteAsync(args, ct).ConfigureAwait(false);
                        string resultText = result.Success
                            ? result.Output
                            : $"Error: {result.Error}\n{result.Output}";

                        session.AddToolResult(toolCall.Id, toolCall.Name, resultText);
                        OnToolComplete?.Invoke(toolCall.Name, result);
                    }

                    continue; // next iteration to process tool results
                }
                else
                {
                    session.AddAssistantMessage(response.Content);
                    break; // done
                }
            }
        }
        catch (OperationCanceledException)
        {
            OnError?.Invoke("Agent stopped by user.");
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Agent error: {ex.Message}");
        }
        finally
        {
            session.IsRunning = false;
            OnComplete?.Invoke();
        }
    }

    public async Task<AgentOrchestrator> CreateSubAgent(string objective, CancellationToken ct)
    {
        var subSession = new AgentSession
        {
            WorkingDirectory = session.WorkingDirectory
        };

        var subOrchestrator = new AgentOrchestrator(llmClient, subSession, toolRegistry);
        subOrchestrator.InitializeSession();
        await subOrchestrator.RunAsync(objective, ct).ConfigureAwait(false);
        return subOrchestrator;
    }

    private async Task<LLMResponse> StreamResponse(IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        LLMResponse? finalResponse = null;

        await llmClient.StreamAsync(
            session.Messages,
            tools,
            delta => OnTextDelta?.Invoke(delta),
            response => finalResponse = response,
            ct
        ).ConfigureAwait(false);

        return finalResponse ?? new LLMResponse { Content = "" };
    }

    private static Dictionary<string, string> ParseArguments(string json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        var obj = JsonHelper.ParseObject(json);
        foreach (var kv in obj)
        {
            result[kv.Key] = kv.Value?.ToString() ?? "";
        }
        return result;
    }
}
