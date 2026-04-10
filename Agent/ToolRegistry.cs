using System.Collections.Generic;
using System.Linq;

namespace WController.Agent;

public class ToolRegistry
{
    private readonly Dictionary<string, IAgentTool> tools = new Dictionary<string, IAgentTool>();

    public void Register(IAgentTool tool)
    {
        tools[tool.Name] = tool;
    }

    public IAgentTool? GetTool(string name)
    {
        tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IReadOnlyList<IAgentTool> GetAll() => tools.Values.ToList();

    public IReadOnlyList<ToolDefinition> GetDefinitions() => tools.Values.Select(t => t.Definition).ToList();
}
