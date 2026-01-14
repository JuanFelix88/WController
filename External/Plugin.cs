using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WController.External;

public class Plugin
{
    public string DllPath { get; }
    private dynamic? instance = null;
    public string? Shortcut => instance?.Shortcut;
    public Keys ShortcutKeys => instance?.ShortcutKeys;
    public bool IsLoaded { get; private set; }

    public static bool IsPlugin(string dllPath)
    {
        try
        {
            var assembly = Assembly.LoadFile(dllPath);
            var types = assembly.GetTypes();
            return types.Any(t => t.FullName.StartsWith("WController.Plugin."));
        }
        catch
        {
            return false;
        }
    }

    public event Action<Plugin>? CalledByShortcut;

    public Plugin(string dllPath)
    {
        DllPath = dllPath;
        LoadPlugin();
    }

    private void LoadPlugin()
    {
        if (IsLoaded) return;

        var assembly = Assembly.LoadFile(DllPath);
        var types = assembly.GetTypes();

        string? entrypointPluginClass = types.FirstOrDefault(t => Regex.IsMatch(t.FullName, @"WController\.Plugin\.(.*)\.Plugin"))?.FullName;

        if (entrypointPluginClass is null) throw new Exception("Plugin entry class not found.");

        instance = Activator.CreateInstance(assembly.GetType(entrypointPluginClass));
        IsLoaded = true;

        if (ShortcutKeys != Keys.None)
        {
            Util.GlobalHotkeyMapper.AddListenerFor(ShortcutKeys, () => CalledByShortcut?.Invoke(this));
        }
    }

    public IEnumerable<PluginItem> DispatchLoadItems(string? textSearch)
    {
        LoadPlugin();

        var externalObjectItems = new List<(string Id, string Name, Image Image)>();

        instance?.OnLoadItems(textSearch, externalObjectItems);
        return externalObjectItems.Select(o => new PluginItem
        {
            Id = o.Id,
            Name = o.Name,
            Image = o.Image,
            Plugin = this
        });
    }

    public bool DispatchSelectItem(PluginItem item)
    {
        LoadPlugin();

        bool succes = instance?.OnSelectItem((item.Id, item.Name)) ?? false;
        return succes;
    }
}

public static class FileInfoExtensions
{
    public static bool IsDllPlugin(this FileInfo file) => Plugin.IsPlugin(file.FullName);
}
