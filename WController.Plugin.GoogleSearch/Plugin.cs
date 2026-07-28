using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using WController.Plugin.GoogleSearch.Properties;

namespace WController.Plugin.GoogleSearch;

public class Plugin
{
    public string Shortcut { get; } = "g";
    public Keys ShortcutKeys { get; } = Keys.Control | Keys.Alt | Keys.G;
    internal Image DefaultImage = Resources.RunCommand;
    internal Image AiImage = Resources.AI;
    internal bool IsLoaded => true;

    public Plugin()
    {
    }

    public void OnLoadItems(string? textSearch, List<(string Id, string Name, Image Image)> items)
    {
        if (string.IsNullOrWhiteSpace(textSearch))
            return;

        items.Add((textSearch!, $"Search: {textSearch}", DefaultImage));
        items.Add(($"ai:{textSearch}", $"AI Mode: {textSearch}", AiImage));
    }

    public bool OnSelectItem((string Id, string Name) item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
            return false;

        bool isAiMode = item.Id.StartsWith("ai:");
        string searchQuery = Uri.EscapeDataString(isAiMode ? item.Id.Substring(3) : item.Id);
        string googleSearchUrl = isAiMode
            ? $"https://www.google.com/search?udm=50&q={searchQuery}"
            : $"https://www.google.com/search?q={searchQuery}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = googleSearchUrl,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
