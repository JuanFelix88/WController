using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WController.Agent.Tools;

public class WriteFileTool : IAgentTool
{
    public string Name => "write_file";

    public ToolDefinition Definition => new ToolDefinition
    {
        Name = Name,
        Description = "Write content to a file. Creates the file and directories if they do not exist. Use mode 'overwrite' to replace or 'append' to append.",
        Parameters = new[]
        {
            new ToolParameter { Name = "path", Type = "string", Description = "Absolute or relative path to the file", Required = true },
            new ToolParameter { Name = "content", Type = "string", Description = "The content to write", Required = true },
            new ToolParameter { Name = "mode", Type = "string", Description = "'overwrite' (default) or 'append'", Required = false }
        }
    };

    private readonly Func<string> getWorkingDir;

    public WriteFileTool(Func<string> getWorkingDir)
    {
        this.getWorkingDir = getWorkingDir;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, string> arguments, CancellationToken ct)
    {
        if (!arguments.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
            return Task.FromResult(new ToolResult { Success = false, Error = "Missing required parameter: path" });
        if (!arguments.TryGetValue("content", out var content))
            return Task.FromResult(new ToolResult { Success = false, Error = "Missing required parameter: content" });

        path = ResolvePath(path);

        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            arguments.TryGetValue("mode", out var mode);
            bool append = string.Equals(mode, "append", StringComparison.OrdinalIgnoreCase);

            if (append)
                File.AppendAllText(path, content);
            else
                File.WriteAllText(path, content);

            return Task.FromResult(new ToolResult { Success = true, Output = $"File written: {path}" });
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
