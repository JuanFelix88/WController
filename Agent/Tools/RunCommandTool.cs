using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WController.Agent.Tools;

public class RunCommandTool : IAgentTool
{
    public string Name => "run_command";

    public ToolDefinition Definition => new ToolDefinition
    {
        Name = Name,
        Description = "Run a CLI command in the working directory. Uses cmd.exe by default, or git bash if shell='bash' and it is available.",
        Parameters = new[]
        {
            new ToolParameter { Name = "command", Type = "string", Description = "The command to run", Required = true },
            new ToolParameter { Name = "shell", Type = "string", Description = "'cmd' (default) or 'bash' (uses git bash if available)", Required = false },
            new ToolParameter { Name = "timeout", Type = "integer", Description = "Timeout in seconds. Default: 60", Required = false }
        }
    };

    private readonly Func<string> getWorkingDir;

    public RunCommandTool(Func<string> getWorkingDir)
    {
        this.getWorkingDir = getWorkingDir;
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, string> arguments, CancellationToken ct)
    {
        if (!arguments.TryGetValue("command", out var command) || string.IsNullOrWhiteSpace(command))
            return new ToolResult { Success = false, Error = "Missing required parameter: command" };

        arguments.TryGetValue("shell", out var shell);
        if (string.IsNullOrWhiteSpace(shell)) shell = "cmd";

        arguments.TryGetValue("timeout", out var timeoutStr);
        if (!int.TryParse(timeoutStr, out int timeoutSec) || timeoutSec <= 0) timeoutSec = 60;

        string workDir = getWorkingDir();
        if (!Directory.Exists(workDir))
            return new ToolResult { Success = false, Error = $"Working directory not found: {workDir}" };

        string fileName;
        string args;

        if (shell.Equals("bash", StringComparison.OrdinalIgnoreCase))
        {
            string? bashPath = FindGitBash();
            if (bashPath == null)
                return new ToolResult { Success = false, Error = "Git Bash not found. Install Git for Windows or use shell='cmd'." };

            fileName = bashPath;
            args = $"-c \"{command.Replace("\"", "\\\"")}\"";
        }
        else
        {
            fileName = "cmd.exe";
            args = $"/C {command}";
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var proc = new Process { StartInfo = psi })
            {
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                proc.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                bool exited;
                using (ct.Register(() => { try { proc.Kill(); } catch { } }))
                {
                    exited = await Task.Run(() => proc.WaitForExit(timeoutSec * 1000), ct).ConfigureAwait(false);
                }

                if (!exited)
                {
                    try { proc.Kill(); } catch { }
                    return new ToolResult { Success = false, Error = $"Command timed out after {timeoutSec}s", Output = stdout.ToString() };
                }

                string output = stdout.ToString();
                string error = stderr.ToString();

                if (output.Length > 60000)
                    output = output.Substring(0, 60000) + "\n... [truncated]";

                return new ToolResult
                {
                    Success = proc.ExitCode == 0,
                    Output = output,
                    Error = string.IsNullOrWhiteSpace(error) ? null : error
                };
            }
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, Error = ex.Message };
        }
    }

    private static string? FindGitBash()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\bin\bash.exe")
        };

        foreach (var p in candidates)
            if (File.Exists(p)) return p;

        // try PATH
        try
        {
            var psi = new ProcessStartInfo("where", "bash.exe")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var proc = Process.Start(psi))
            {
                string? result = proc?.StandardOutput.ReadLine();
                proc?.WaitForExit(3000);
                if (!string.IsNullOrWhiteSpace(result) && File.Exists(result))
                    return result;
            }
        }
        catch { }

        return null;
    }
}
