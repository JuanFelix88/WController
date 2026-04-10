using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WController.Agent.Tools;

public class SearchCodebaseTool : IAgentTool
{
    public string Name => "search_codebase";

    public ToolDefinition Definition => new ToolDefinition
    {
        Name = Name,
        Description = "Search for text or regex patterns across files in the working directory. Returns matching lines with file paths and line numbers.",
        Parameters = new[]
        {
            new ToolParameter { Name = "query", Type = "string", Description = "Text or regex pattern to search for", Required = true },
            new ToolParameter { Name = "filePattern", Type = "string", Description = "Glob-style file pattern filter (e.g. '*.cs', '*.txt'). Default: '*.*'", Required = false },
            new ToolParameter { Name = "isRegex", Type = "boolean", Description = "Whether query is a regex pattern. Default: false", Required = false },
            new ToolParameter { Name = "maxResults", Type = "integer", Description = "Maximum number of results. Default: 50", Required = false }
        }
    };

    private readonly Func<string> getWorkingDir;

    public SearchCodebaseTool(Func<string> getWorkingDir)
    {
        this.getWorkingDir = getWorkingDir;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, string> arguments, CancellationToken ct)
    {
        if (!arguments.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query))
            return Task.FromResult(new ToolResult { Success = false, Error = "Missing required parameter: query" });

        arguments.TryGetValue("filePattern", out var filePattern);
        if (string.IsNullOrWhiteSpace(filePattern)) filePattern = "*.*";

        arguments.TryGetValue("isRegex", out var isRegexStr);
        bool isRegex = string.Equals(isRegexStr, "true", StringComparison.OrdinalIgnoreCase);

        arguments.TryGetValue("maxResults", out var maxStr);
        if (!int.TryParse(maxStr, out int maxResults) || maxResults <= 0) maxResults = 50;

        string workDir = getWorkingDir();
        if (!Directory.Exists(workDir))
            return Task.FromResult(new ToolResult { Success = false, Error = $"Working directory not found: {workDir}" });

        try
        {
            Regex? regex = isRegex ? new Regex(query, RegexOptions.IgnoreCase | RegexOptions.Compiled) : null;
            var sb = new StringBuilder();
            int count = 0;

            var ignoreDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bin", "obj", "node_modules", ".git", ".vs", "packages", "debug", "release"
            };

            foreach (var file in EnumerateFiles(workDir, filePattern, ignoreDirs))
            {
                if (ct.IsCancellationRequested) break;
                if (count >= maxResults) break;

                try
                {
                    var lines = File.ReadAllLines(file);
                    string relativePath = GetRelativePath(workDir, file);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (count >= maxResults) break;

                        bool match = isRegex
                            ? regex!.IsMatch(lines[i])
                            : lines[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

                        if (match)
                        {
                            sb.AppendLine($"{relativePath}:{i + 1}: {lines[i].TrimStart()}");
                            count++;
                        }
                    }
                }
                catch { /* skip unreadable files */ }
            }

            if (count == 0)
                return Task.FromResult(new ToolResult { Success = true, Output = "No matches found." });

            return Task.FromResult(new ToolResult { Success = true, Output = sb.ToString() });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult { Success = false, Error = ex.Message });
        }
    }

    private static IEnumerable<string> EnumerateFiles(string root, string pattern, HashSet<string> ignoreDirs)
    {
        Queue<string> dirs = new Queue<string>();
        dirs.Enqueue(root);

        while (dirs.Count > 0)
        {
            string dir = dirs.Dequeue();

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir, pattern); }
            catch { continue; }

            foreach (var f in files)
                yield return f;

            IEnumerable<string> subDirs;
            try { subDirs = Directory.EnumerateDirectories(dir); }
            catch { continue; }

            foreach (var sub in subDirs)
            {
                string dirName = Path.GetFileName(sub);
                if (!ignoreDirs.Contains(dirName))
                    dirs.Enqueue(sub);
            }
        }
    }

    private static string GetRelativePath(string basePath, string fullPath)
    {
        if (!basePath.EndsWith("\\")) basePath += "\\";
        if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return fullPath.Substring(basePath.Length);
        return fullPath;
    }
}
