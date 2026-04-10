using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WController.Agent.Tools;

public class ReadFileTool : IAgentTool
{
    public string Name => "read_file";

    public ToolDefinition Definition => new ToolDefinition
    {
        Name = Name,
        Description = "Read the contents of a file. You can specify startLine and endLine for partial reads. Line numbers are 1-based.",
        Parameters = new[]
        {
            new ToolParameter { Name = "path", Type = "string", Description = "Absolute or relative path to the file to read", Required = true },
            new ToolParameter { Name = "startLine", Type = "integer", Description = "Start line number (1-based, optional)", Required = false },
            new ToolParameter { Name = "endLine", Type = "integer", Description = "End line number (1-based, inclusive, optional)", Required = false }
        }
    };

    private readonly Func<string> getWorkingDir;

    public ReadFileTool(Func<string> getWorkingDir)
    {
        this.getWorkingDir = getWorkingDir;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, string> arguments, CancellationToken ct)
    {
        if (!arguments.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
            return Task.FromResult(new ToolResult { Success = false, Error = "Missing required parameter: path" });

        path = ResolvePath(path);

        if (!File.Exists(path))
            return Task.FromResult(new ToolResult { Success = false, Error = $"File not found: {path}" });

        try
        {
            var lines = File.ReadAllLines(path);
            int start = 0;
            int end = lines.Length;

            if (arguments.TryGetValue("startLine", out var startStr) && int.TryParse(startStr, out int s))
                start = Math.Max(0, s - 1);
            if (arguments.TryGetValue("endLine", out var endStr) && int.TryParse(endStr, out int e))
                end = Math.Min(lines.Length, e);

            var selected = new string[end - start];
            Array.Copy(lines, start, selected, 0, selected.Length);
            string content = string.Join("\n", selected);

            if (content.Length > 60000)
                content = content.Substring(0, 60000) + "\n... [truncated]";

            return Task.FromResult(new ToolResult { Success = true, Output = content });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
        }
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        return Path.Combine(getWorkingDir(), path);
    }
}
