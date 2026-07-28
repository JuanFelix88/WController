using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WController.Util;

namespace WController.External;

internal class PluginManager
{
    public List<Plugin> Plugins = new();
    public List<Exception> Exceptions = new();
    public event Action<Plugin>? PluginCalledByShortcut;

    public PluginManager()
    {
        var dlls = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).EnumerateFiles("WController.Plugin.*.dll");

        foreach (var dllFileInfo in dlls)
        {
            try
            {
                if (dllFileInfo.IsDllPlugin() is false)
                    continue;

                Plugins.Add(new Plugin(dllFileInfo.FullName));
                Plugins.Last().CalledByShortcut += (plugin) => PluginCalledByShortcut?.Invoke(plugin);
            }
            catch (Exception e) { throw; }
        }
    }

    public (Plugin Plugin, string TextSearch)? ExtractPluginSearch(string textShortcut)
    {
        var parts = textShortcut.Split([':']);

        if (parts.Length < 2)
            return null;

        string textSearch = parts[1];
        string pluginShortcut = parts[0];

        Plugin? plugin = Plugins.FirstOrDefault(p => p.Shortcut == pluginShortcut);

        if (plugin is null) return null;

        return (plugin, textSearch.Trim());
    }

    public void RemoveAllListenersHotKeys()
    {
        GlobalHotkeyMapper.RemoveAllListeners();
    }
}
