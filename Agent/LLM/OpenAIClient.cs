using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WController.Agent.LLM;

public class OpenAIClient : ILLMClient
{
    private readonly HttpClient http;
    private readonly string apiKey;
    private readonly string endpoint;
    private readonly string model;

    public string ModelName => model;

    public OpenAIClient(string apiKey, string model = "gpt-4o", string endpoint = "https://api.openai.com/v1/chat/completions")
    {
        this.apiKey = apiKey;
        this.model = model;
        this.endpoint = endpoint;
        http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        http.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<LLMResponse> SendAsync(List<AgentMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        string bodyJson = BuildRequestJson(messages, tools, stream: false);
        var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var res = await http.PostAsync(endpoint, content, ct).ConfigureAwait(false);
        var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"LLM API error ({res.StatusCode}): {json}");

        var obj = JsonHelper.ParseObject(json);
        var choices = JsonHelper.GetArray(obj, "choices");
        if (choices == null || choices.Count == 0) throw new Exception("Invalid API response: missing choices");

        var choice = JsonHelper.AsDictionary(choices[0]);
        var message = JsonHelper.GetObject(choice, "message");
        if (message == null) throw new Exception("Invalid API response: missing message");

        return ParseMessage(message);
    }

    public async Task StreamAsync(
        List<AgentMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        Action<string> onTextDelta,
        Action<LLMResponse> onComplete,
        CancellationToken ct)
    {
        string bodyJson = BuildRequestJson(messages, tools, stream: true);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
        };

        using (var res = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            if (!res.IsSuccessStatusCode)
            {
                var errorBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new Exception($"LLM API error ({res.StatusCode}): {errorBody}");
            }

            using (var stream = await res.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var fullText = new StringBuilder();
                var toolCallsAccum = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

                while (!reader.EndOfStream && !ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;
                    if (!line.StartsWith("data: ")) continue;

                    var data = line.Substring(6).Trim();
                    if (data == "[DONE]") break;

                    Dictionary<string, object?> chunk;
                    try { chunk = JsonHelper.ParseObject(data); }
                    catch { continue; }

                    var choices = JsonHelper.GetArray(chunk, "choices");
                    if (choices == null || choices.Count == 0) continue;

                    var choiceObj = JsonHelper.AsDictionary(choices[0]);
                    var delta = JsonHelper.GetObject(choiceObj, "delta");
                    if (delta == null) continue;

                    var textContent = JsonHelper.GetString(delta, "content");
                    if (textContent != null)
                    {
                        fullText.Append(textContent);
                        onTextDelta(textContent);
                    }

                    var toolCalls = JsonHelper.GetArray(delta, "tool_calls");
                    if (toolCalls != null)
                    {
                        foreach (var tcRaw in toolCalls)
                        {
                            var tc = JsonHelper.AsDictionary(tcRaw);
                            int index = JsonHelper.GetInt(tc, "index");
                            if (!toolCallsAccum.ContainsKey(index))
                            {
                                var fn = JsonHelper.GetObject(tc, "function");
                                toolCallsAccum[index] = (
                                    JsonHelper.GetString(tc, "id") ?? "",
                                    fn != null ? (JsonHelper.GetString(fn, "name") ?? "") : "",
                                    new StringBuilder()
                                );
                            }

                            var fnDelta = JsonHelper.GetObject(tc, "function");
                            var argFrag = fnDelta != null ? JsonHelper.GetString(fnDelta, "arguments") : null;
                            if (argFrag != null)
                                toolCallsAccum[index].Args.Append(argFrag);
                        }
                    }
                }

                var response = new LLMResponse { Content = fullText.ToString() };

                if (toolCallsAccum.Count > 0)
                {
                    response.ToolCalls = toolCallsAccum
                        .OrderBy(kv => kv.Key)
                        .Select(kv => new ToolCall
                        {
                            Id = kv.Value.Id,
                            Name = kv.Value.Name,
                            ArgumentsJson = kv.Value.Args.ToString()
                        })
                        .ToList();
                }

                onComplete(response);
            }
        }
    }

    private string BuildRequestJson(List<AgentMessage> messages, IReadOnlyList<ToolDefinition> tools, bool stream)
    {
        var msgList = new List<object?>();
        foreach (var m in messages)
        {
            var dict = new Dictionary<string, object?> { ["role"] = RoleName(m.Role) };

            if (m.Role == MessageRole.Tool)
            {
                dict["content"] = m.Content;
                dict["tool_call_id"] = m.ToolCallId;
            }
            else if (m.Role == MessageRole.Assistant && m.ToolCalls != null && m.ToolCalls.Count > 0)
            {
                dict["content"] = string.IsNullOrEmpty(m.Content) ? null : m.Content;

                var tcList = new List<object?>();
                foreach (var tc in m.ToolCalls)
                {
                    tcList.Add(new Dictionary<string, object?>
                    {
                        ["id"] = tc.Id,
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, object?>
                        {
                            ["name"] = tc.Name,
                            ["arguments"] = tc.ArgumentsJson
                        }
                    });
                }
                dict["tool_calls"] = tcList;
            }
            else
            {
                dict["content"] = m.Content;
            }

            msgList.Add(dict);
        }

        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = msgList,
            ["stream"] = stream,
            ["temperature"] = 0.1
        };

        if (tools.Count > 0)
        {
            var toolList = new List<object?>();
            foreach (var t in tools)
            {
                var props = new Dictionary<string, object?>();
                var required = new List<object?>();

                foreach (var p in t.Parameters)
                {
                    props[p.Name] = new Dictionary<string, object?>
                    {
                        ["type"] = MapType(p.Type),
                        ["description"] = p.Description
                    };
                    if (p.Required)
                        required.Add(p.Name);
                }

                toolList.Add(new Dictionary<string, object?>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = new Dictionary<string, object?>
                        {
                            ["type"] = "object",
                            ["properties"] = props,
                            ["required"] = required
                        }
                    }
                });
            }
            body["tools"] = toolList;
        }

        return JsonHelper.SerializeObject(body);
    }

    private static string MapType(string type)
    {
        return type switch
        {
            "integer" => "integer",
            "boolean" => "boolean",
            "number" => "number",
            _ => "string"
        };
    }

    private static string RoleName(MessageRole role) => role switch
    {
        MessageRole.System => "system",
        MessageRole.User => "user",
        MessageRole.Assistant => "assistant",
        MessageRole.Tool => "tool",
        _ => "user"
    };

    private static LLMResponse ParseMessage(Dictionary<string, object?> message)
    {
        var response = new LLMResponse { Content = JsonHelper.GetString(message, "content") ?? "" };

        var toolCalls = JsonHelper.GetArray(message, "tool_calls");
        if (toolCalls != null && toolCalls.Count > 0)
        {
            response.ToolCalls = new List<ToolCall>();
            foreach (var tcRaw in toolCalls)
            {
                var tc = JsonHelper.AsDictionary(tcRaw);
                var fn = JsonHelper.GetObject(tc, "function");
                response.ToolCalls.Add(new ToolCall
                {
                    Id = JsonHelper.GetString(tc, "id") ?? "",
                    Name = fn != null ? (JsonHelper.GetString(fn, "name") ?? "") : "",
                    ArgumentsJson = fn != null ? (JsonHelper.GetString(fn, "arguments") ?? "{}") : "{}"
                });
            }
        }

        return response;
    }

    public void Dispose()
    {
        http.Dispose();
    }
}
