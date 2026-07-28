using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WController.Plugin.Prompts.Forms;
using WController.Plugin.Prompts.Network;
using WController.Plugin.Prompts.Properties;
using WController.Plugin.Prompts.Util;

namespace WController.Plugin.Prompts;
public class Plugin
{
    public string Shortcut { get; } = "p";
    public Keys ShortcutKeys { get; } = Keys.Control | Keys.Alt | Keys.P;
    internal Image DefaultImage = Resources.RunCommand;
    internal bool IsLoaded => reloadAt is not null;
    private int maxReturn = 8;
    private List<PrePrompt> prompts = new();
    private DateTime? reloadAt = null;
    private readonly string dividerEndPrompt = "END PROMPT";
    private System.Timers.Timer reloadTimer = new();

    public Plugin()
    {
        new Thread(LoadPlugin).Start();

        reloadTimer.Interval = TimeSpan.FromSeconds(30).TotalMilliseconds;
        reloadTimer.Enabled = true;
        reloadTimer.AutoReset = true;
        reloadTimer.Elapsed += this.OnReloadTimerElapsed;
    }

    #region Plugin Relationship
    private void LoadPlugin()
    {
        LoadPrompts();
    }

    private void LoadPrompts()
    {
        lock (this.prompts)
        {
            var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var wcontrollerFolder = Path.Combine(userFolder, ".wcontroller");
            string promptsFilePath = Path.Combine(wcontrollerFolder, "prompts.txt");

            if (!Directory.Exists(wcontrollerFolder) || !File.Exists(promptsFilePath))
            {
                reloadAt = DateTime.Now;
                this.prompts = [];
                return;
            }

            this.prompts = File.ReadAllText(promptsFilePath).Split([dividerEndPrompt], StringSplitOptions.None)
                .Select(p => p.TrimStart())
                .Where(p => string.IsNullOrWhiteSpace(p) is false)
                .Select(p => PrePrompt.FromTextFile(p))
                .Distinct()
                .ToList();

            reloadAt = DateTime.Now;
        }
    }

    public void OnLoadItems(string? textSearch, List<(string Id, string Name, Image Image)> items)
    {
        if (textSearch == "@")
        {
            items.Add(("@", "Open prompts config", Resources.Automatic));
            return;
        }

        CallIfNeededReloadPrompts();
        lock (this.prompts)
        {
            var matchedPrompts = this.prompts
                .Where(p => string.IsNullOrEmpty(textSearch) || p.Contains(textSearch))
                .Take(maxReturn)
                .Select((p, index) => (Id: p.Title, Name: p.Title, Image: DefaultImage))
                .ToList();
            items.AddRange(matchedPrompts);
        }
    }

    public bool OnSelectItem((string Id, string Name) item)
    {
        if (item.Id == "@")
        {
            string dirPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wcontroller");
            string filePath = Path.Combine(dirPath, "prompts.txt");
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            if (!File.Exists(filePath)) File.Create(filePath).Close();
            Process.Start("explorer.exe", filePath);
            return true;
        }

        PrePrompt prePrompt = prompts.FirstOrDefault(p => p.Title == item.Name);

        if (prePrompt is null) return false;

        string prompt = new Regex(@"\\[\r|\n]{1,}$").Replace(prePrompt.Content, string.Empty);

        if (CompletePrompt.NeedToComplete(prompt))
        {
            var completePrompt = new CompletePrompt();

            completePrompt.ShowWithPrompt(prompt);
            completePrompt.PromptCompleted += async (cp) =>
            {
                completePrompt.Close();
                var computingView = ComputingPrompt.ShowWithDescription(completePrompt.CompletedPrompt);
                try
                {
                    var result = await IAService.RequestPrompt(cp.CompletedPrompt);

                    var responseText = result.Response.Trim();

                    foreach (var splitterCode in (List<string>)[
                        "Copy code",
                        "C#Copy",
                        "JavaScriptCopy",
                        "javascriptCopy",
                        "TypeScriptCopy",
                        "PythonCopy",
                        "ShellCopy",
                        "JSONCopy"
                    ])
                    {
                        if (responseText.Contains(splitterCode))
                        {
                            var parts = responseText.Split(new string[] { splitterCode }, StringSplitOptions.RemoveEmptyEntries);

                            if (parts.Length > 2) continue;

                            var otherText = parts.LastOrDefault();

                            if (otherText is not null)
                            {
                                responseText = otherText.Trim();
                            }
                        }
                    }
                        
                    CopyTextToClipboard(responseText);
                    computingView.Focus();
                    MessageBox.Show("Response AI copied.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    computingView.Close();
                }
            };
            return true;
        }

        Task.Run(() => PasteText(prompt, false));

        return true;
    }
    #endregion

    private void CopyTextToClipboard(string text)
    {
        try
        {
            var t = new Thread(() =>
            {
                Clipboard.SetText(text);
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
        }
        catch (Exception)
        {
            throw;
        }
    }

    private void PasteText(string text, bool withEnter = false)
    {
        try
        {
            var t = new Thread(() =>
            {
                Clipboard.SetText(text);
                Task.Delay(100).Wait();
                KeyboardSimulator.SendCtrlV();
                if (withEnter) KeyboardSimulator.SendEnter();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
        }
        catch (Exception)
        {
            throw;
        }
    }

    private void OnReloadTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        => LoadPrompts();

    private void CallIfNeededReloadPrompts()
    {
        if (reloadAt is null)
        {
            LoadPrompts();
            return;
        }
        if ((DateTime.Now - reloadAt.Value).TotalSeconds <= 10) return;
        LoadPrompts();
    }
}
