using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WController.Agent.LLM;
using WController.Agent.Rendering;
using WController.Agent.Tools;

namespace WController.Agent.UI;

public partial class AgentChatForm : Form
{
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED - reduce flicker
            return cp;
        }
    }

    private static readonly Color BgColor = Color.FromArgb(30, 30, 30);
    private static readonly Color SecondaryColor = Color.FromArgb(45, 45, 45);
    private static readonly Color BorderColor = Color.FromArgb(57, 57, 57);
    private static readonly Color AccentColor = Color.FromArgb(86, 156, 214);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 220);
    private static readonly Color DimTextColor = Color.FromArgb(140, 140, 140);
    private static readonly Color InputBgColor = Color.FromArgb(38, 38, 38);
    private static readonly Color ToolBgColor = Color.FromArgb(35, 35, 35);

    // Instance state - no globals
    private AgentSession session;
    private AgentOrchestrator? orchestrator;
    private ToolRegistry toolRegistry;
    private ILLMClient? llmClient;
    private IMarkdownRenderer markdownRenderer;
    private AgentConfig config;

    // UI controls
    private Panel topBar = null!;
    private TextBox folderTextBox = null!;
    private Button browseFolderBtn = null!;
    private Label modelLabel = null!;
    private TextBox apiKeyTextBox = null!;
    private TextBox apiUrlTextBox = null!;
    private TextBox modelsTextBox = null!;
    private Panel chatArea = null!;
    private FlowLayoutPanel chatFlow = null!;
    private Panel inputArea = null!;
    private RichTextBox inputTextBox = null!;
    private ShimmerLabel shimmerLabel = null!;
    private Label statusLabel = null!;

    public AgentChatForm()
    {
        session = new AgentSession();
        toolRegistry = new ToolRegistry();
        markdownRenderer = new WinFormsMarkdownRenderer();
        config = AgentConfig.Load();
        InitializeUI();
        RegisterTools();
        LoadConfigToUI();
    }

    private void InitializeUI()
    {
        // Form settings
        this.Text = "WController Agent";
        this.FormBorderStyle = FormBorderStyle.None;
        this.ShowInTaskbar = false;
        this.TopMost = true;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Size = new Size(720, 600);
        this.BackColor = BgColor;
        this.DoubleBuffered = true;
        this.Padding = new Padding(1);
        this.KeyPreview = true;

        // === TOP BAR ===
        topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = BgColor,
            Padding = new Padding(12, 8, 12, 4)
        };

        // Row 1: Title + ESC
        var titleLabel = new Label
        {
            Text = "Agent",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = TextColor,
            AutoSize = true,
            Location = new Point(12, 6)
        };
        topBar.Controls.Add(titleLabel);

        var closeBtn = new Label
        {
            Text = "ESC",
            Font = new Font("Consolas", 8f),
            ForeColor = DimTextColor,
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        closeBtn.Location = new Point(topBar.Width - 44, 8);
        closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeBtn.Click += (s, e) => this.Hide();
        topBar.Controls.Add(closeBtn);

        // Row 2: Folder
        var folderLabel = new Label
        {
            Text = "Folder:",
            Font = new Font("Segoe UI", 9f),
            ForeColor = DimTextColor,
            AutoSize = true,
            Location = new Point(12, 30)
        };
        topBar.Controls.Add(folderLabel);

        folderTextBox = new TextBox
        {
            Font = new Font("Consolas", 9f),
            ForeColor = TextColor,
            BackColor = InputBgColor,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(62, 28),
            Width = 360,
            Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        folderTextBox.TextChanged += (s, e) => session.WorkingDirectory = folderTextBox.Text;
        topBar.Controls.Add(folderTextBox);

        browseFolderBtn = new Button
        {
            Text = "...",
            Font = new Font("Segoe UI", 8f),
            ForeColor = TextColor,
            BackColor = SecondaryColor,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(30, 22),
            Location = new Point(426, 28),
            Cursor = Cursors.Hand
        };
        browseFolderBtn.FlatAppearance.BorderColor = BorderColor;
        browseFolderBtn.Click += OnBrowseFolder;
        topBar.Controls.Add(browseFolderBtn);

        // Row 3: API URL + API Key
        var urlLabel = new Label
        {
            Text = "URL:",
            Font = new Font("Segoe UI", 9f),
            ForeColor = DimTextColor,
            AutoSize = true,
            Location = new Point(12, 52)
        };
        topBar.Controls.Add(urlLabel);

        apiUrlTextBox = new TextBox
        {
            Font = new Font("Consolas", 8.5f),
            ForeColor = TextColor,
            BackColor = InputBgColor,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(44, 50),
            Width = 290
        };
        apiUrlTextBox.LostFocus += (s, e) => SaveConfigFromUI();
        topBar.Controls.Add(apiUrlTextBox);

        var keyLabel = new Label
        {
            Text = "Key:",
            Font = new Font("Segoe UI", 9f),
            ForeColor = DimTextColor,
            AutoSize = true,
            Location = new Point(340, 52)
        };
        topBar.Controls.Add(keyLabel);

        apiKeyTextBox = new TextBox
        {
            Font = new Font("Consolas", 8.5f),
            ForeColor = TextColor,
            BackColor = InputBgColor,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(372, 50),
            Width = 200,
            UseSystemPasswordChar = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        apiKeyTextBox.LostFocus += (s, e) => SaveConfigFromUI();
        topBar.Controls.Add(apiKeyTextBox);

        // Row 4: Models + Active model
        var modelsLabel = new Label
        {
            Text = "Models:",
            Font = new Font("Segoe UI", 9f),
            ForeColor = DimTextColor,
            AutoSize = true,
            Location = new Point(12, 74)
        };
        topBar.Controls.Add(modelsLabel);

        modelsTextBox = new TextBox
        {
            Font = new Font("Consolas", 8.5f),
            ForeColor = TextColor,
            BackColor = InputBgColor,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(66, 72),
            Width = 500,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        modelsTextBox.LostFocus += (s, e) => SaveConfigFromUI();
        topBar.Controls.Add(modelsTextBox);

        this.Controls.Add(topBar);

        // === SEPARATOR ===
        var topSep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = BorderColor };
        this.Controls.Add(topSep);
        topSep.BringToFront();

        // === INPUT AREA (bottom) ===
        inputArea = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 100,
            BackColor = BgColor,
            Padding = new Padding(12, 8, 12, 6)
        };

        shimmerLabel = new ShimmerLabel
        {
            Dock = DockStyle.Top,
            Height = 20,
            Visible = false
        };
        inputArea.Controls.Add(shimmerLabel);

        // Bottom row: model selector + hints
        var bottomRow = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 20,
            BackColor = BgColor
        };

        modelLabel = new Label
        {
            Text = "gpt-4o",
            Font = new Font("Consolas", 8.5f, FontStyle.Bold),
            ForeColor = AccentColor,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Location = new Point(0, 2)
        };
        modelLabel.Click += (s, e) => ShowModelPicker();
        bottomRow.Controls.Add(modelLabel);

        statusLabel = new Label
        {
            Dock = DockStyle.Right,
            Width = 420,
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = DimTextColor,
            Text = "Enter send · Shift+Enter newline · Ctrl+Bksp stop · Ctrl+Alt+. model · Esc close",
            TextAlign = ContentAlignment.MiddleRight
        };
        bottomRow.Controls.Add(statusLabel);

        inputArea.Controls.Add(bottomRow);

        inputTextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = TextColor,
            BackColor = InputBgColor,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            AcceptsTab = false
        };
        inputTextBox.KeyDown += OnInputKeyDown;
        inputArea.Controls.Add(inputTextBox);

        var bottomSep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = BorderColor };
        this.Controls.Add(bottomSep);
        this.Controls.Add(inputArea);
        bottomSep.BringToFront();

        // === CHAT AREA (center) ===
        chatArea = new SubtleScrollPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            AutoScroll = true,
            Padding = new Padding(16, 12, 16, 12)
        };

        chatFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = BgColor,
            Padding = new Padding(8, 120, 8, 40)
        };
        chatArea.Controls.Add(chatFlow);
        this.Controls.Add(chatArea);

        // Events
        this.Load += OnFormLoad;
        this.Resize += OnFormResize;
        this.Paint += OnFormPaint;

        session.WorkingDirectory = folderTextBox.Text;
    }

    private void RegisterTools()
    {
        Func<string> getDir = () => session.WorkingDirectory;
        toolRegistry.Register(new ReadFileTool(getDir));
        toolRegistry.Register(new WriteFileTool(getDir));
        toolRegistry.Register(new SearchCodebaseTool(getDir));
        toolRegistry.Register(new RunCommandTool(getDir));
        toolRegistry.Register(new ListDirectoryTool(getDir));
    }

    private ILLMClient? EnsureClient()
    {
        string key = apiKeyTextBox.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            AppendSystemMessage("Please set your API key first.");
            return null;
        }

        string url = apiUrlTextBox.Text.Trim();
        if (string.IsNullOrEmpty(url))
        {
            AppendSystemMessage("Please set the API URL first.");
            return null;
        }

        string model = config.SelectedModel;

        // Recreate client if settings changed
        if (llmClient != null && llmClient.ModelName == model)
            return llmClient;

        llmClient?.Dispose();
        llmClient = new OpenAIClient(key, model, url);
        return llmClient;
    }

    // =========== Config management ===========

    private void LoadConfigToUI()
    {
        apiUrlTextBox.Text = config.ApiUrl;
        apiKeyTextBox.Text = config.ApiKey;
        modelsTextBox.Text = string.Join(", ", config.Models);
        modelLabel.Text = config.SelectedModel;
    }

    private void SaveConfigFromUI()
    {
        config.ApiUrl = apiUrlTextBox.Text.Trim();
        config.ApiKey = apiKeyTextBox.Text.Trim();
        config.SelectedModel = modelLabel.Text;

        var models = new System.Collections.Generic.List<string>();
        foreach (string part in modelsTextBox.Text.Split(','))
        {
            string m = part.Trim();
            if (!string.IsNullOrEmpty(m)) models.Add(m);
        }
        if (models.Count > 0) config.Models = models;

        // Force client recreation on next send
        llmClient?.Dispose();
        llmClient = null;

        config.Save();
    }

    private void ShowModelPicker()
    {
        // Parse models from textbox (most up-to-date source)
        var models = new System.Collections.Generic.List<string>();
        foreach (string part in modelsTextBox.Text.Split(','))
        {
            string m = part.Trim();
            if (!string.IsNullOrEmpty(m)) models.Add(m);
        }

        if (models.Count == 0)
        {
            AppendSystemMessage("No models configured. Add models in the Models field (comma-separated).");
            return;
        }

        var menu = new ContextMenuStrip
        {
            BackColor = SecondaryColor,
            ForeColor = TextColor,
            Font = new Font("Consolas", 9.5f),
            ShowImageMargin = false
        };
        menu.Renderer = new DarkMenuRenderer();

        foreach (string model in models)
        {
            var item = menu.Items.Add(model);
            if (model == config.SelectedModel)
            {
                item.Font = new Font("Consolas", 9.5f, FontStyle.Bold);
                item.ForeColor = AccentColor;
            }
            item.Click += (s, e) =>
            {
                config.SelectedModel = model;
                modelLabel.Text = model;
                llmClient?.Dispose();
                llmClient = null;
                config.Save();
                statusLabel.Text = $"Model: {model}";
            };
        }

        // Show above the model label (which is now in the bottom bar)
        var screenPoint = modelLabel.PointToScreen(new Point(0, 0));
        var formPoint = this.PointToClient(screenPoint);
        menu.Show(this, new Point(formPoint.X, formPoint.Y - (models.Count * 26) - 4));
    }

    // Dark renderer for ContextMenuStrip
    private class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors()) { }
    }

    private class DarkMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
        public override Color MenuItemBorder => Color.FromArgb(70, 70, 70);
        public override Color MenuBorder => Color.FromArgb(57, 57, 57);
        public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 45);
        public override Color ImageMarginGradientBegin => Color.FromArgb(45, 45, 45);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(45, 45, 45);
        public override Color ImageMarginGradientEnd => Color.FromArgb(45, 45, 45);
    }

    // =========== Keyboard handling ===========

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Backspace: stop agent
        if (e.Control && e.KeyCode == Keys.Back)
        {
            e.SuppressKeyPress = true;
            StopAgent();
            return;
        }

        // Enter: send (without Shift)
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            SendMessage();
            return;
        }

        // Esc: hide
        if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            this.Hide();
            return;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            this.Hide();
            return true;
        }
        // Ctrl+Alt+. → Model picker
        if (keyData == (Keys.Control | Keys.Alt | Keys.OemPeriod))
        {
            ShowModelPicker();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // =========== Agent interaction ===========

    private async void SendMessage()
    {
        string text = inputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (session.IsRunning) return;

        var client = EnsureClient();
        if (client == null) return;

        inputTextBox.Text = string.Empty;
        AppendUserMessage(text);

        orchestrator = new AgentOrchestrator(client, session, toolRegistry);
        orchestrator.InitializeSession();

        // Create a message panel for the assistant response
        var responsePanel = CreateMessagePanel("Agent", AccentColor);
        var contentPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = BgColor,
            Width = chatFlow.Width - 40,
            Padding = new Padding(0)
        };
        responsePanel.Controls.Add(contentPanel);
        chatFlow.Controls.Add(responsePanel);

        shimmerLabel.IsShimmering = true;
        inputTextBox.ReadOnly = true;

        var cts = new CancellationTokenSource();
        session.CancellationSource = cts;

        orchestrator.OnTextDelta += delta =>
        {
            SafeInvoke(() => markdownRenderer.RenderAppend(delta, contentPanel));
        };

        orchestrator.OnToolStart += toolName =>
        {
            SafeInvoke(() =>
            {
                shimmerLabel.ShimmerText = toolName;
                AppendToolMessage(toolName, responsePanel);
            });
        };

        orchestrator.OnToolComplete += (toolName, result) =>
        {
            SafeInvoke(() =>
            {
                shimmerLabel.ShimmerText = "Thinking...";
            });
        };

        orchestrator.OnError += error =>
        {
            LogError(error);
            SafeInvoke(() => AppendSystemMessage(error));
        };

        orchestrator.OnComplete += () =>
        {
            SafeInvoke(() =>
            {
                shimmerLabel.IsShimmering = false;
                inputTextBox.ReadOnly = false;
                inputTextBox.Focus();
                ScrollToBottom();
            });
        };

        orchestrator.OnThinking += () =>
        {
            SafeInvoke(() => shimmerLabel.ShimmerText = "Thinking...");
        };

        try
        {
            await Task.Run(() => orchestrator.RunAsync(text, cts.Token)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogError($"Error in SendMessage: {ex.Message}", ex);
            AppendSystemMessage($"Error: {ex.Message}");
        }
        finally
        {
            shimmerLabel.IsShimmering = false;
            inputTextBox.ReadOnly = false;
            inputTextBox.Focus();
        }
    }

    internal static void LogError(string message, Exception? ex = null)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WController");
            Directory.CreateDirectory(dir);
            var logPath = Path.Combine(dir, "crash.log");
            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}] {message}{Environment.NewLine}";
            if (ex != null)
                entry += $"{ex}{Environment.NewLine}";
            entry += Environment.NewLine;
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Swallow - logging must never throw
        }
    }

    private void StopAgent()
    {
        session.Stop();
        shimmerLabel.IsShimmering = false;
        inputTextBox.ReadOnly = false;
        statusLabel.Text = "Agent stopped.";
    }

    // =========== UI helpers ===========

    private void AppendUserMessage(string text)
    {
        var panel = CreateMessagePanel("You", Color.FromArgb(77, 184, 100));
        var label = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 10f),
            ForeColor = TextColor,
            AutoSize = true,
            MaximumSize = new Size(chatFlow.Width - 40, 0),
            Padding = new Padding(0, 2, 0, 4)
        };
        panel.Controls.Add(label);

        var separator = new Panel
        {
            Width = chatFlow.Width - 40,
            Height = 2,
            BackColor = Color.FromArgb(60, 60, 60),
            Margin = new Padding(0, 4, 0, 0)
        };
        panel.Controls.Add(separator);

        chatFlow.Controls.Add(panel);
        ScrollToBottom();
    }

    private void AppendSystemMessage(string text)
    {
        var label = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9f, FontStyle.Italic),
            ForeColor = Color.FromArgb(200, 160, 80),
            AutoSize = true,
            MaximumSize = new Size(chatFlow.Width - 20, 0),
            Padding = new Padding(8, 4, 8, 4)
        };
        chatFlow.Controls.Add(label);
        ScrollToBottom();
    }

    private void AppendToolMessage(string toolInfo, Panel parentPanel)
    {
        var wrapper = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = ToolBgColor,
            Margin = new Padding(8, 2, 0, 2)
        };

        var accentBar = new Panel
        {
            Width = 2,
            Height = 20,
            BackColor = AccentColor,
            Margin = new Padding(0, 2, 4, 2)
        };
        wrapper.Controls.Add(accentBar);

        var toolLabel = new Label
        {
            Text = $"⚡ {toolInfo}",
            Font = new Font("Consolas", 8.5f),
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = ToolBgColor,
            AutoSize = true,
            Padding = new Padding(0, 2, 4, 2)
        };
        wrapper.Controls.Add(toolLabel);

        parentPanel.Controls.Add(wrapper);
        ScrollToBottom();
    }

    private Panel CreateMessagePanel(string role, Color roleColor)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = BgColor,
            Width = chatFlow.Width - 20,
            Padding = new Padding(8, 4, 8, 4)
        };

        var roleLabel = new Label
        {
            Text = role,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = roleColor,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 2)
        };
        panel.Controls.Add(roleLabel);

        // Add separator at bottom when panel is added to parent
        panel.ControlAdded += (s, e) =>
        {
            // Remove previous separator if exists
            foreach (Control c in panel.Controls)
            {
                if (c.Tag as string == "_separator")
                {
                    panel.Controls.Remove(c);
                    c.Dispose();
                    break;
                }
            }
            var sep = new Panel
            {
                Width = panel.Width - 16,
                Height = 1,
                BackColor = Color.FromArgb(50, 50, 50),
                Margin = new Padding(0, 4, 0, 0),
                Tag = "_separator"
            };
            panel.Controls.Add(sep);
        };

        return panel;
    }

    private void EnsureBottomSpacer()
    {
        const int spacerHeight = 120;
        Panel? spacer = null;
        foreach (Control c in chatFlow.Controls)
        {
            if (c is Panel p && (string?)p.Tag == "__bottomSpacer")
            {
                spacer = p;
                break;
            }
        }
        if (spacer == null)
        {
            spacer = new Panel
            {
                Height = spacerHeight,
                Width = chatFlow.Width - 20,
                BackColor = Color.Transparent,
                Tag = "__bottomSpacer"
            };
            chatFlow.Controls.Add(spacer);
        }
        else
        {
            spacer.Height = spacerHeight;
            chatFlow.Controls.SetChildIndex(spacer, chatFlow.Controls.Count - 1);
        }
    }

    private void ScrollToBottom()
    {
        EnsureBottomSpacer();
        chatArea.ScrollControlIntoView(chatFlow);
        if (chatFlow.Controls.Count > 0)
        {
            var last = chatFlow.Controls[chatFlow.Controls.Count - 1];
            chatArea.ScrollControlIntoView(last);
        }
    }

    private void SafeInvoke(Action action)
    {
        if (InvokeRequired)
            BeginInvoke(action);
        else
            action();
    }

    // =========== Folder browsing ===========

    private void OnBrowseFolder(object? sender, EventArgs e)
    {
        using (var fbd = new FolderBrowserDialog())
        {
            fbd.SelectedPath = folderTextBox.Text;
            fbd.Description = "Select working folder for the agent";
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                folderTextBox.Text = fbd.SelectedPath;
            }
        }
    }

    // =========== Form appearance ===========

    private void OnFormLoad(object? sender, EventArgs e)
    {
        WindowEffects.TrySetAttribute(this.Handle, 33, 2);
        ApplyRoundedRegion(16);
        inputTextBox.Focus();
    }

    private void OnFormResize(object? sender, EventArgs e)
    {
        ApplyRoundedRegion(16);
        // Update chat flow width
        chatFlow.Width = chatArea.ClientSize.Width - 16;
        foreach (Control c in chatFlow.Controls)
        {
            if (c is FlowLayoutPanel flp)
                flp.Width = chatFlow.Width - 20;
        }
    }

    private void OnFormPaint(object? sender, PaintEventArgs e)
    {
        int borderRadius = 16;
        int borderThickness = 2;
        Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

        using (GraphicsPath path = GetRoundedPath(rect, borderRadius))
        using (Pen pen = new Pen(BorderColor, borderThickness))
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }
    }

    private void ApplyRoundedRegion(int radius)
    {
        Rectangle bounds = this.ClientRectangle;
        using (GraphicsPath path = GetRoundedPath(bounds, radius))
        {
            this.Region = new Region(path);
        }
    }

    private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        int d = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.StartFigure();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // =========== Show/Hide (preserve state) ===========

    public void ShowAgent()
    {
        this.Show();
        this.Activate();
        this.BringToFront();
        inputTextBox.Focus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            SaveConfigFromUI();
            this.Hide();
            return;
        }
        SaveConfigFromUI();
        llmClient?.Dispose();
        base.OnFormClosing(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        // Don't hide on deactivate for agent - user may interact with other windows
        base.OnDeactivate(e);
    }

    // =========== Public API ===========

    public void SetWorkingDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            folderTextBox.Text = path;
            session.WorkingDirectory = path;
        }
    }

    // =========== Subtle scrollbar panel ===========

    private class SubtleScrollPanel : Panel
    {
        private const int ScrollBarWidth = 6;
        private const int WM_NCCALCSIZE = 0x0083;
        private const int SB_VERT = 1;
        private static readonly Color ThumbColor = Color.FromArgb(100, 255, 255, 255);
        private static readonly Color TrackColor = Color.Transparent;

        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        [DllImport("user32.dll")]
        private static extern bool GetScrollInfo(IntPtr hWnd, int nBar, ref SCROLLINFO lpScrollInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct SCROLLINFO
        {
            public uint cbSize;
            public uint fMask;
            public int nMin;
            public int nMax;
            public uint nPage;
            public int nPos;
            public int nTrackPos;
        }

        private System.Windows.Forms.Timer? scrollFadeTimer;
        private float scrollOpacity;
        private bool scrollVisible;

        public SubtleScrollPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            scrollFadeTimer = new System.Windows.Forms.Timer { Interval = 30 };
            scrollFadeTimer.Tick += OnScrollFadeTick;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCCALCSIZE)
            {
                ShowScrollBar(Handle, SB_VERT, false);
            }
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            ShowSubtleScrollbar();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ShowSubtleScrollbar();
        }

        private void ShowSubtleScrollbar()
        {
            scrollOpacity = 1f;
            scrollVisible = true;
            scrollFadeTimer?.Stop();
            scrollFadeTimer?.Start();
            Invalidate();
        }

        private void OnScrollFadeTick(object? sender, EventArgs e)
        {
            scrollOpacity -= 0.06f;
            if (scrollOpacity <= 0)
            {
                scrollOpacity = 0;
                scrollVisible = false;
                scrollFadeTimer?.Stop();
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!scrollVisible || !AutoScroll) return;

            var si = new SCROLLINFO
            {
                cbSize = (uint)Marshal.SizeOf<SCROLLINFO>(),
                fMask = 0x17 // SIF_ALL
            };

            if (!GetScrollInfo(Handle, SB_VERT, ref si)) return;
            if (si.nMax <= (int)si.nPage) return;

            int trackHeight = ClientSize.Height - 4;
            if (trackHeight <= 0) return;

            float viewRatio = (float)si.nPage / (si.nMax + 1);
            int thumbHeight = Math.Max(20, (int)(trackHeight * viewRatio));
            float scrollRatio = (float)si.nPos / Math.Max(1, si.nMax - (int)si.nPage + 1);
            int thumbY = 2 + (int)(scrollRatio * (trackHeight - thumbHeight));

            int x = ClientSize.Width - ScrollBarWidth - 3;
            int alpha = (int)(scrollOpacity * ThumbColor.A);
            using (var brush = new SolidBrush(Color.FromArgb(alpha, ThumbColor)))
            using (var path = CreateRoundedRect(x, thumbY, ScrollBarWidth, thumbHeight, 3))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            }
        }

        private static GraphicsPath CreateRoundedRect(int x, int y, int w, int h, int r)
        {
            int d = r * 2;
            var path = new GraphicsPath();
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                scrollFadeTimer?.Stop();
                scrollFadeTimer?.Dispose();
                scrollFadeTimer = null;
            }
            base.Dispose(disposing);
        }
    }
}
