using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;


namespace WController.Plugin.Prompts;
public class PrePrompt
{
    public static PrePrompt FromTextFile(string text)
    {
        var lines = text.Split([Environment.NewLine], StringSplitOptions.None);

        return new PrePrompt
        {
            Title = lines.FirstOrDefault() ?? string.Empty,
            Content = string.Join(Environment.NewLine, lines.Skip(1)).TrimStart()
        };
    }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public bool Contains(string text)
    {
        text = text.Trim().ToLowerInvariant();
        if (Title.ToLowerInvariant().Contains(text)) return true;
        if (Content.ToLowerInvariant().Contains(text)) return true;

        return false;
    }
        
}
