using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WController.Util;

namespace WController;

public static class WinSettingsStore
{
    private static readonly string _settingsPath;
    private const string HEADER = "WSET";
    private const int VERSION = 3;
    private static List<WindowConfigurable> cachedWindows = new List<WindowConfigurable>();
    private static DateTime lastCachedRead = DateTime.MinValue;

    static WinSettingsStore()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string wcontrollerFolder = Path.Combine(appDataPath, "WController");

        if (!Directory.Exists(wcontrollerFolder))
            Directory.CreateDirectory(wcontrollerFolder);

        _settingsPath = Path.Combine(wcontrollerFolder, "WinSettings.dat");
    }

    public static void Save(List<WindowConfigurable> windows)
    {
        try
        {
            using var fs = new FileStream(_settingsPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fs);

            writer.Write(HEADER);
            writer.Write(VERSION);
            writer.Write(windows.Count);

            foreach (var window in windows)
                WriteWindow(writer, window);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro ao salvar configurações: {ex.Message}");
        }
    }

    private static void WriteWindow(BinaryWriter writer, WindowConfigurable window)
    {
        writer.Write(window.Shortcut ?? string.Empty);
        writer.Write(window.Title ?? string.Empty);
        writer.Write(window.ProgramPath ?? string.Empty);
        writer.Write((byte)window.MatchMode);
        writer.Write(window.RegexPattern ?? string.Empty);
        writer.Write(window.IconPath ?? string.Empty);
    }

    public static List<WindowConfigurable> Load()
    {
        var windows = new List<WindowConfigurable>();

        if (!File.Exists(_settingsPath))
            return windows;

        try
        {
            using var fs = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            string header = reader.ReadString();
            if (header != HEADER)
                return windows;

            int version = reader.ReadInt32();
            if (version < 1 || version > VERSION)
                return windows;

            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var window = ReadWindow(reader, version);
                    if (window != null)
                        windows.Add(window);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao carregar janela {i}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro ao carregar configurações: {ex.Message}");
        }

        return windows;
    }

    private static WindowConfigurable? ReadWindow(BinaryReader reader, int version)
    {
        string shortCut = reader.ReadString();
        string title = reader.ReadString();
        string programPath = reader.ReadString();

        MatchMode matchMode = MatchMode.Path;
        string regexPattern = string.Empty;
        string iconPath = string.Empty;

        if (version >= 2)
        {
            matchMode = (MatchMode)reader.ReadByte();
            regexPattern = reader.ReadString();
        }

        if (version >= 3)
            iconPath = reader.ReadString();

        if (string.IsNullOrEmpty(programPath) && matchMode == MatchMode.Path)
            return null;

        return new WindowConfigurable
        {
            Shortcut = shortCut,
            Title = title,
            ProgramPath = programPath,
            MatchMode = matchMode,
            RegexPattern = regexPattern,
            IconPath = iconPath
        };
    }

    public static List<WindowConfigurable> LoadFromCache()
    {
        if (DateTime.Now - lastCachedRead < TimeSpan.FromSeconds(2))
            return cachedWindows;

        cachedWindows = Load();
        lastCachedRead = DateTime.Now;
        return cachedWindows;
    }
}
