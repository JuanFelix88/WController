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
    private const int VERSION = 1;
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
            using (FileStream fs = new FileStream(_settingsPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                // Escrever header
                writer.Write(HEADER);
                writer.Write(VERSION);
                writer.Write(windows.Count);

                // Escrever cada janela
                foreach (var window in windows)
                {
                    writer.Write(window.Shortcut ?? string.Empty);
                    writer.Write(window.Title ?? string.Empty);
                    writer.Write(window.ProgramPath ?? string.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro ao salvar configurações: {ex.Message}");
        }
    }

    public static List<WindowConfigurable> Load()
    {
        var windows = new List<WindowConfigurable>();

        if (!File.Exists(_settingsPath))
            return windows;

        try
        {
            using (FileStream fs = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // Validar header
                string header = reader.ReadString();
                if (header != HEADER)
                    return windows;

                // Validar versão
                int version = reader.ReadInt32();
                if (version != VERSION)
                    return windows;

                // Ler quantidade de janelas
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        string shortCut = reader.ReadString();
                        string title = reader.ReadString();
                        string programPath = reader.ReadString();

                        if (string.IsNullOrEmpty(programPath))
                            continue;

                        windows.Add(new WindowConfigurable
                        {
                            Shortcut = shortCut,
                            Title = title,
                            ProgramPath = programPath
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Erro ao carregar janela {i}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro ao carregar configurações: {ex.Message}");
        }

        return windows;
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
