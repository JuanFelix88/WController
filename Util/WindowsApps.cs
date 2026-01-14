using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace WController.Util;

public static class WindowsApps
{
    public static List<(string Path, string Name)> GetInstalledApps()
    {
        var result = new List<(string Path, string Name)>();

        string[] registryKeys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var regKey in registryKeys)
            {
                using (RegistryKey key = root.OpenSubKey(regKey))
                {
                    if (key == null) continue;

                    string p = string.Empty;
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey? subKey = key.OpenSubKey(subKeyName))
                            {
                                if (subKey == null) continue;

                                //string? directory = subKey.GetValue("InstallLocation") as string;

                                string? name = subKey.GetValue("DisplayName") as string;
                                if (string.IsNullOrWhiteSpace(name)) continue;

                                string? path = subKey.GetValue("DisplayIcon") as string;

                                if (path?.EndsWith(".exe") is false && !string.IsNullOrWhiteSpace(path))
                                {

                                    p = path;
                                    if (path.EndsWith("\\")) path.Substring(0, path.Length - 1);
                                    if (path.StartsWith("\\")) path.Substring(1);
                                    path = path.Replace("\"", string.Empty);
                                    p = path;

                                    if (Path.IsPathRooted(path) is false)
                                        continue;

                                    DirectoryInfo di = new DirectoryInfo(Path.GetDirectoryName(path));
                                    if (!di.Exists) continue;

                                    var exeFiles = di.GetFiles("*.exe");
                                    if (exeFiles.Length > 0)
                                        path = exeFiles.FirstOrDefault()?.FullName;

                                    if (path is null)
                                    {
                                        var lnkFiles = di.GetFiles("*.lnk");
                                        if (lnkFiles.Length > 0)
                                            path = lnkFiles.FirstOrDefault()?.FullName;
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(name))
                                    result.Add((path, name));
                            }
                        }
                        catch (Exception)
                        {

                            throw;
                        }

                    }
                }
            }
        }

        return result;
    }
}
