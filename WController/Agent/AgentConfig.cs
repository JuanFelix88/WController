using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using WController.Agent.LLM;

namespace WController.Agent;

public class AgentConfig
{
    private static readonly string ConfigPath;

    static AgentConfig()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appDataPath, "WController");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        ConfigPath = Path.Combine(folder, "agent-config.json");
    }

    public string ApiUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string ApiKey { get; set; } = string.Empty;
    public List<string> Models { get; set; } = new List<string> { "gpt-4o", "gpt-4o-mini", "o3-mini" };
    public string SelectedModel { get; set; } = "gpt-4o";

    public void Save()
    {
        try
        {
            var obj = new Dictionary<string, object>
            {
                ["apiUrl"] = ApiUrl,
                ["apiKey"] = ApiKey,
                ["models"] = Models,
                ["selectedModel"] = SelectedModel
            };
            string json = JsonHelper.SerializeObject(obj);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save agent config: {ex.Message}");
        }
    }

    public static AgentConfig Load()
    {
        var config = new AgentConfig();
        if (!File.Exists(ConfigPath))
            return config;

        try
        {
            string json = File.ReadAllText(ConfigPath);
            var obj = JsonHelper.ParseObject(json);

            string? url = JsonHelper.GetString(obj, "apiUrl");
            if (url != null && url.Length > 0) config.ApiUrl = url;

            string? key = JsonHelper.GetString(obj, "apiKey");
            if (key != null && key.Length > 0) config.ApiKey = key;

            string? selected = JsonHelper.GetString(obj, "selectedModel");
            if (selected != null && selected.Length > 0) config.SelectedModel = selected;

            var models = JsonHelper.GetArray(obj, "models");
            if (models != null && models.Count > 0)
            {
                config.Models = new List<string>();
                foreach (var m in models)
                {
                    if (m is string s && !string.IsNullOrWhiteSpace(s))
                        config.Models.Add(s);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load agent config: {ex.Message}");
        }

        return config;
    }
}
