using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WController;

internal sealed class WindowBlocks : IDisposable
{
    private readonly object sync = new object();
    private readonly string path;
    private readonly FileSystemWatcher watcher;
    private readonly Timer reloadTimer;
    private Regex[] rules = Array.Empty<Regex>();
    private bool reloadPending;
    private bool disposed;

    internal WindowBlocks(string? path = null)
    {
        this.path = Path.GetFullPath(path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "blocks.txt"));
        reloadTimer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
        watcher = new FileSystemWatcher(Path.GetDirectoryName(this.path)!, Path.GetFileName(this.path))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged;
        watcher.Renamed += OnFileChanged;
        watcher.Error += (_, _) => ScheduleReload();
        watcher.EnableRaisingEvents = true;
        Reload();
    }

    internal bool IsBlocked(string title)
    {
        Regex[] snapshot;
        lock (sync)
        {
            // An activation need not wait for the watcher debounce to expire.
            if (reloadPending && !disposed)
                ReloadRules();
            snapshot = rules;
        }

        foreach (Regex rule in snapshot)
        {
            try
            {
                if (rule.IsMatch(title))
                    return true;
            }
            catch (RegexMatchTimeoutException ex)
            {
                Debug.WriteLine($"Timeout em blocks.txt: {ex.Pattern}");
                return true;
            }
        }
        return false;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        ScheduleReload();
    }

    private void ScheduleReload()
    {
        lock (sync)
        {
            if (disposed) return;
            reloadPending = true;
            reloadTimer.Change(100, Timeout.Infinite);
        }
    }

    private void Reload()
    {
        lock (sync)
        {
            if (!disposed)
                ReloadRules();
        }
    }

    private void ReloadRules()
    {
        try
        {
            var updatedRules = new List<Regex>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                string? line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        updatedRules.Add(new Regex(line, RegexOptions.None, TimeSpan.FromMilliseconds(100)));
                    }
                    catch (ArgumentException ex)
                    {
                        Debug.WriteLine($"Regex inválida em blocks.txt, linha {lineNumber}: {ex.Message}");
                    }
                }
            }
            rules = updatedRules.ToArray();
            reloadPending = false;
        }
        catch (FileNotFoundException)
        {
            rules = Array.Empty<Regex>();
            reloadPending = false;
        }
        catch (DirectoryNotFoundException)
        {
            rules = Array.Empty<Regex>();
            reloadPending = false;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // Keep the last successfully read rules while another program holds the file.
            Debug.WriteLine($"Erro ao ler blocks.txt: {ex.Message}");
            reloadPending = true;
            reloadTimer.Change(250, Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            watcher.Dispose();
            reloadTimer.Dispose();
        }
    }
}
