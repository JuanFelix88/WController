using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WController.Plugin.ProjectsSearch.Util;
internal static class StringsProcessor
{
    public static void LoadByCSharp(string path, StringColletion strings)
    {
        try
        {
            foreach (
                string file in
                Directory.GetFiles(path, "*.cs", SearchOption.TopDirectoryOnly))
            {
                string fileContent = File.ReadAllText(Path.Combine(path, file));

                var matches = Regex.Matches(fileContent, $"= \"(.*)\"");

                if (matches.Count == 0) continue;

                foreach (Match m in matches)
                {
                    if (!m.Success) continue;
                    if (m.Captures.Count == 0) continue;

                    strings.Add(m.Groups[0].Value.ToLowerSafe());
                }
            }
        }
        catch { }

        foreach (string folder in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            if (DirIgnore.IsIgnored(folder)) continue;
            try { LoadByCSharp(Path.Combine(path, folder), strings); } catch { }
        }
    }

    public static void LoadByJS(string path, StringColletion strings)
    {
        try
        {
            foreach (
                string file in
                Directory.GetFiles(path, "*.js", SearchOption.TopDirectoryOnly))
            {
                string fileContent = File.ReadAllText(Path.Combine(path, file));

                var matches = Regex.Matches(fileContent, $"= \"(.*)\"");

                if (matches.Count == 0) continue;

                foreach (Match m in matches)
                {
                    if (!m.Success) continue;
                    if (m.Captures.Count == 0) continue;

                    strings.Add(m.Groups[0].Value.ToLowerSafe());
                }
            }

            foreach (
                string file in
                Directory.GetFiles(path, "*.ts", SearchOption.TopDirectoryOnly))
            {
                string fileContent = File.ReadAllText(Path.Combine(path, file));

                var matches = Regex.Matches(fileContent, $"= \"(.*)\"");

                if (matches.Count == 0) continue;

                foreach (Match m in matches)
                {
                    if (!m.Success) continue;
                    if (m.Captures.Count == 0) continue;

                    strings.Add(m.Groups[0].Value.ToLowerSafe());
                }
            }

            foreach (
                string file in
                Directory.GetFiles(path, "*.tsx", SearchOption.TopDirectoryOnly))
            {
                string fileContent = File.ReadAllText(Path.Combine(path, file));

                var matches = Regex.Matches(fileContent, $"= \"(.*)\"");

                if (matches.Count == 0) continue;

                foreach (Match m in matches)
                {
                    if (!m.Success) continue;
                    if (m.Captures.Count == 0) continue;

                    strings.Add(m.Groups[0].Value.ToLowerSafe());
                }
            }
        }
        catch { }

        foreach (string folder in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            if (DirIgnore.IsIgnored(folder)) continue;
            try { LoadByJS(Path.Combine(path, folder), strings); } catch { }
        }
    }
}
