using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WController.Plugin.ProjectsSearch.Util;

namespace WController.Plugin.ProjectsSearch;
internal class Project
{
    public required string Id { get; set; }
    public required string ProjectDirectory { get; set; }
    public required string ProjectFilePath { get; set; }
    public required string Name { get; set; }
    public StringColletion Strings { get; } = new();
    public required ProjectType Type { get; set; }

    public bool ContainsWithName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (Name.ToLowerSafe().Contains(text)) return true;
        return false;
    }

    public bool ContainsWithStrings(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (Strings.Any(s => s.Contains(text))) return true;
        return false;
    }

    public bool Contains(string text)
    {
        return ContainsWithName(text) || ContainsWithStrings(text);
    }

    public static Project FromCsproj(string filePath)
    {
        string parentFolder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(filePath)));

        if (parentFolder is not null)
        {
            parentFolder = $"({parentFolder})";
        }

        Project project = new()
        {
            Id = filePath,
            ProjectDirectory = Path.GetDirectoryName(filePath) ?? string.Empty,
            ProjectFilePath = filePath,
            Name = $"{Path.GetFileNameWithoutExtension(filePath)} {parentFolder}".Trim(),
            Type = ProjectType.CSharp
        };

        Util.StringsProcessor.LoadByCSharp(project.ProjectDirectory, project.Strings);
        return project;
    }

    public static Project FromPackageJson(string filePath)
    {
        string parentFolder = Path.GetFileName(Path.GetDirectoryName(filePath));

        if (parentFolder is not null)
        {
            parentFolder = $"({parentFolder})";
        }

        var fileContent = File.ReadAllText(filePath);

        string pName = Regex.Match(fileContent, "\"name\"\\s*:\\s*\"([^\"]+)\"")?.Groups[1]?.Value ?? string.Empty;

        Project project = new()
        {
            Id = filePath,
            ProjectDirectory = Path.GetDirectoryName(filePath) ?? string.Empty,
            ProjectFilePath = filePath,
            Name = $"{pName} {parentFolder}".Trim(),
            Type = ProjectType.JS
        };

        Util.StringsProcessor.LoadByJS(project.ProjectDirectory, project.Strings);
        return project;
    }

    public static implicit operator ProjectType(Project project) => project.Type;

    #region Subtypes
    public enum ProjectType
    {
        Unknown,
        CSharp,
        JS
    }
    #endregion
}
