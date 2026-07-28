using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WController.Agent.Tools;

public class ListDirectoryTool : IAgentTool
{
    public string Name => "list_directory";

    public ToolDefinition Definition => new ToolDefinition
    {
        Name = Name,
        Description = "List the contents of a directory. Shows files and subdirectories with sizes. Folders end with /.",
        Parameters = new[]
        {
            new ToolParameter { Name = "path", Type = "string", Description = "Absolute or relative path to the directory. Default: working directory.", Required = false },
            new ToolParameter { Name = "recursive", Type = "boolean", Description = "If true, list recursively (max 3 levels). Default: false", Required = false }
        }
    };

    private readonly Func<string> getWorkingDir;

    public ListDirectoryTool(Func<string> getWorkingDir)
    {
        this.getWorkingDir = getWorkingDir;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, string> arguments, CancellationToken ct)
    {
        arguments.TryGetValue("path", out var path);
        if (string.IsNullOrWhiteSpace(path)) path = getWorkingDir();
        else path = ResolvePath(path);

        if (!Directory.Exists(path))
            return Task.FromResult(new ToolResult { Success = false, Error = $"Directory not found: {path}" });

        arguments.TryGetValue("recursive", out var recursiveStr);
        bool recursive = string.Equals(recursiveStr, "true", StringComparison.OrdinalIgnoreCase);

        try
        {
            var sb = new StringBuilder();
            ListDir(sb, path, 0, recursive ? 3 : 0, ct);
            return Task.FromResult(new ToolResult { Success = true, Output = sb.ToString() });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
        }
    }

    private void ListDir(StringBuilder sb, string dir, int depth, int maxDepth, CancellationToken ct)
    {
        string indent = new string(' ', depth * 2);

        try
        {
            foreach (var d in Directory.GetDirectories(dir))
            {
                if (ct.IsCancellationRequested) return;
                string name = Path.GetFileName(d);
                sb.AppendLine($"{indent}{name}/");
                if (depth < maxDepth)
                    ListDir(sb, d, depth + 1, maxDepth, ct);
            }

            foreach (var f in Directory.GetFiles(dir))
            {
                if (ct.IsCancellationRequested) return;
                string name = Path.GetFileName(f);
                sb.AppendLine($"{indent}{name}");
            }
        }
        catch { /* skip inaccessible dirs */ }
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        return Path.Combine(getWorkingDir(), path);
    }
}
