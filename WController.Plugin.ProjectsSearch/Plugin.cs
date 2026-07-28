using ProjectsSearch.Plugin.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using WController.Plugin.ProjectsSearch.Properties;
using WController.Plugin.ProjectsSearch.Util;

namespace WController.Plugin.ProjectsSearch;
public class Plugin
{
    public string Shortcut { get; } = "ps";
    public Keys ShortcutKeys { get; } = Keys.None;
    internal Image DefaultImage = Resources.Bookmark;
    internal List<Project> Projects = [];
    internal bool IsLoaded { get; private set; } = false;
    private int maxReturn = 8;

    public Plugin()
    {
        new Thread(LoadPlugin).Start();
    }

    private void LoadPlugin()
    {
        StringColletion.ClearCache();
        string[] bases = [
            $"C:\\www"
        ];

        foreach (string baseDir in bases)
        {
            if (Directory.Exists(baseDir) is false) continue;

            LoadProjectsByPath(baseDir, Projects);
        }

        foreach (var p in Projects)
        {
            p.Strings.SaveInCache();
        }

        GC.Collect();
        IsLoaded = true;
    }

    private void LoadProjectsByPath(string path, List<Project> projects)
    {
        try
        {
            foreach (
                string file in
                Directory.GetFiles(path, "*.csproj", SearchOption.TopDirectoryOnly))
            {
                var project = Project.FromCsproj(Path.Combine(path, file));

                if (projects.Any(p => p.Id == project.Id)) continue;

                projects.Add(project);
            }

            foreach (
                string file in
                Directory.GetFiles(path, "package.json", SearchOption.TopDirectoryOnly))
            {
                var project = Project.FromPackageJson(Path.Combine(path, file));

                if (projects.Any(p => p.Id == project.Id)) continue;

                projects.Add(project);
            }
        }
        catch { }

        foreach (string folder in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            if (DirIgnore.IsIgnored(folder)) continue;
            try { LoadProjectsByPath(Path.Combine(path, folder), projects); } catch { }
        }
    }

    public void OnLoadItems(string? textSearch, List<(string Id, string Name, Image Image)> items)
    {
        if (textSearch is null) return;

        textSearch = textSearch.ToLowerSafe();
        var projects = Projects.ToArray();

        foreach (var p in projects)
        {
            if (items.Count == maxReturn) return;
            if (p.ContainsWithName(textSearch) && items.Any(i => i.Id == p.Id) is false)
            {
                Image img = p.Type switch
                {
                    Project.ProjectType.CSharp => Resources.CSharpLogo,
                    Project.ProjectType.JS => Resources.JSLogo,
                    _ => DefaultImage
                };

                items.Add((p.Id, p.Name, img));
            }
        }

        foreach (var p in projects)
        {
            if (items.Count == maxReturn) return;
            if (p.ContainsWithStrings(textSearch) && items.Any(i => i.Id == p.Id) is false)
            {
                Image img = p.Type switch
                {
                    Project.ProjectType.CSharp => Resources.CSharpLogo,
                    Project.ProjectType.JS => Resources.JSLogo,
                    _ => DefaultImage
                };

                items.Add((p.Id, p.Name, img));
            }
        }
    }

    public bool OnSelectItem((string Id, string Name) item)
    {
        Project? project = Projects.FirstOrDefault(p => p.Id == item.Id);

        if (project is null) return false;

        if (project == Project.ProjectType.CSharp)
        {
            System.Diagnostics.Process.Start("explorer.exe", project.ProjectFilePath);
        }

        if (project == Project.ProjectType.JS)
        {
            System.Diagnostics.Process.Start(
                GetAssocExec.FromPath(project.ProjectFilePath),
                project.ProjectDirectory);
        }

        return true;
    }
}
