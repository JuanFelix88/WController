using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using WController.Properties;
using WController.Util;

namespace WController;

public class FileIndexes
{
    private List<FileIndexed> files = new List<FileIndexed>();
    private System.Timers.Timer timerRefresh = new System.Timers.Timer();

    public FileIndexes(int secondsToRefresh = 60 * 5)
    {
        timerRefresh.AutoReset = true;
        timerRefresh.Enabled = true;
        timerRefresh.Interval = secondsToRefresh * 1000;
        timerRefresh.Elapsed += this.OnTimerElapsed;

        Task.Run(ComputeIndexes);
    }

    public IEnumerable<FileIndexed> SearchFiles(string text, int take = 6)
    {
        if (string.IsNullOrEmpty(text))
            return new FileIndexed[0];

        text = Util.Text.RemoveDiacritics(text).ToLowerInvariant();

        var filteredFiles = files.Where(f => f.Name.ToLowerInvariant().Contains(text)).Take(100);

        filteredFiles = filteredFiles.OrderBy(filteredFile => filteredFile.Name.Length);

        filteredFiles = filteredFiles.OrderBy(filteredFile =>
        {
            int index = filteredFile.SearchText.IndexOf(text, StringComparison.OrdinalIgnoreCase);
            return index == -1 ? int.MaxValue : index;
        });

        return filteredFiles.Take(take);
    }

    private void OnTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        ComputeIndexes();
    }

    private void GetListFromPath(string path, List<string> files)
    {
        try
        {
            foreach (string file in Directory.GetFiles(path, "*.lnk", SearchOption.TopDirectoryOnly))
            {
                files.Add(file);
            }
        }
        catch { }

        foreach (string folder in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            try { GetListFromPath(Path.Combine(path, folder), files); } catch { }
        }
    }

    private void ComputeIndexes()
    {
        List<string> shortcuts = new List<string>();

        GetListFromPath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), shortcuts);
        GetListFromPath(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), shortcuts);
        GetListFromPath("C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs", shortcuts);

        foreach (var file in files)
        {
            file.Image.Dispose();
        }

        shortcuts = shortcuts.OrderBy(shortcut => shortcut).ToList();

        files.Clear();
        foreach (var shortcut in shortcuts)
        {
            try
            {
                string shortname = Path.GetFileName(shortcut).Replace(".lnk", "");
                if (files.Any(f => f.FullPath == shortcut || f.Name == shortname)) continue;

                Image image = Util.IconHelper.GetIcon(shortcut, false)?.ToBitmap()!;
                files.Add(new()
                {
                    FullPath = shortcut,
                    Name = shortname,
                    Image = Util.Resizer.ResizeImage(image, Util.Resizer.DefaultIconViewSize, Util.Resizer.DefaultIconViewSize),
                    SearchText = Util.Text.RemoveDiacritics(Path.GetFileName(shortcut).ToLowerInvariant()) + " " +
                                  Util.Text.RemoveDiacritics(Path.GetFileNameWithoutExtension(shortcut).ToLowerInvariant()) + " " +
                                  Util.Text.RemoveDiacritics(Path.GetDirectoryName(shortcut).ToLowerInvariant())
                });
                image.Dispose();
            }
            catch { }
        }

        var apps = WindowsApps.GetInstalledApps();
        
        foreach (var app in apps)
        {
            try
            {
                if (files.Any(f => f.FullPath == app.Path || f.Name == app.Name)) continue;

                Image image = Util.IconHelper.GetIcon(app.Path, false)?.ToBitmap() ?? Resources.ProgramIcon;
                files.Add(new()
                {
                    FullPath = app.Path,
                    Name = app.Name,
                    Image = Util.Resizer.ResizeImage(image, Util.Resizer.DefaultIconViewSize, Util.Resizer.DefaultIconViewSize),
                    SearchText = Util.Text.RemoveDiacritics(app.Name.ToLowerInvariant()) + " " +
                                    Util.Text.RemoveDiacritics(Path.GetFileNameWithoutExtension(app.Path).ToLowerInvariant()) + " " +
                                    Util.Text.RemoveDiacritics(Path.GetDirectoryName(app.Path).ToLowerInvariant())
                });
                image.Dispose();
            }
            catch (Exception)
            {

                throw;
            }
        }

        foreach (var app in GetWindowsApps())
        {
            try
            {
                if (files.Any(f => f.FullPath == app.FilePath || f.Name == app.Name)) continue;

                Image image = Util.IconHelper.GetIcon(app.FilePath, false)?.ToBitmap() ?? Resources.ProgramIcon;

                files.Add(new()
                {
                    FullPath = app.FilePath,
                    Name = app.Name,
                    Image = Util.Resizer.ResizeImage(image, Util.Resizer.DefaultIconViewSize, Util.Resizer.DefaultIconViewSize),
                    SearchText = Util.Text.RemoveDiacritics(app.Name.ToLowerInvariant()) + " " +
                                    Util.Text.RemoveDiacritics(Path.GetFileNameWithoutExtension(app.FilePath).ToLowerInvariant()) + " " +
                                    Util.Text.RemoveDiacritics(Path.GetDirectoryName(app.FilePath).ToLowerInvariant())
                });
                image.Dispose();
            }
            catch (Exception e)
            {
                throw;
            }
        }
    }

    private IEnumerable<(string Name, string Path, string FilePath)> GetWindowsApps()
    {
        string command = @"
            $host.UI.RawUI.BufferSize = New-Object Management.Automation.Host.Size(200, 300);
            Get-AppxPackage | Select Name, InstallLocation
        ";

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-Command \"{command}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(psi);
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Regex regex = new Regex(@"(?<name>\S+)\s+(?<path>[A-Z]:\\[^\r\n]+)", RegexOptions.Compiled);

        var matches = regex.Matches(output);
        foreach (Match match in matches)
        {
            string name = match.Groups["name"].Value.Trim();
            string path = match.Groups["path"].Value.Trim();

            string manifestPath = Path.Combine(path, "AppxManifest.xml");
            if (!File.Exists(manifestPath)) continue;

            var doc = XDocument.Load(manifestPath);
            bool isUserApp = doc.Descendants().Any(x => x.Name.LocalName == "Application" &&
                               x.Attributes().Any(a => a.Name.LocalName == "Executable"));

            if (isUserApp is false) continue;
            //if (path.Contains("WindowsApps")) continue;

            //var di = new DirectoryInfo(path);
            //foreach(var f in di.GetFiles("*.exe"))
            //{
            //    yield return (name, path, f.FullName);
            //}

            XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
            var apps = doc.Descendants().Where(x => x.Name.LocalName == "Application");


            foreach (var app in apps)
            {
                string? exe = app.Attribute("Executable")?.Value;
                string? displayName = app.Elements(uap + "VisualElements")
                                        .Attributes("DisplayName")
                                        .FirstOrDefault()?.Value;

                if (string.IsNullOrEmpty(exe)) continue;
                if (string.IsNullOrEmpty(displayName)) displayName = name;
                yield return (name, path, Path.Combine(path,exe));
            }
        }
    }
}

public class FileIndexed : ItemSelectable
{
    public required string FullPath { get; set; }
    public required string SearchText { get; set; }
    public override void Open()
    {
        if (FullPath.EndsWith(".exe"))
        {
            System.Diagnostics.Process.Start(FullPath);
            return;
        }

        System.Diagnostics.Process.Start("explorer.exe", FullPath);
    }
}
