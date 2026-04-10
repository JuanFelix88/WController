using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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
    private TextBox inputTextBox = null!;
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
            Width = 380
        };
        modelsTextBox.LostFocus += (s, e) => SaveConfigFromUI();
        topBar.Controls.Add(modelsTextBox);

        modelLabel = new Label
        {
            Text = "gpt-4o",
            Font = new Font("Consolas", 8.5f, FontStyle.Bold),
            ForeColor = AccentColor,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Location = new Point(454, 74)
        };
        modelLabel.Click += (s, e) => ShowModelPicker();
        topBar.Controls.Add(modelLabel);

        var modelHint = new Label
        {
            Text = "Ctrl+Alt+.",
            Font = new Font("Consolas", 7f),
            ForeColor = DimTextColor,
            AutoSize = true,
            Location = new Point(454, 90)
        };
        topBar.Controls.Add(modelHint);

        this.Controls.Add(topBar);

        // === SEPARATOR ===
        var topSep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = BorderColor };
        this.Controls.Add(topSep);
        topSep.BringToFront();

        // === INPUT AREA (bottom) ===
        inputArea = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 80,
            BackColor = BgColor,
            Padding = new Padding(12, 8, 12, 8)
        };

        shimmerLabel = new ShimmerLabel
        {
            Dock = DockStyle.Top,
            Height = 20,
            Visible = false
        };
        inputArea.Controls.Add(shimmerLabel);

        statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 16,
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = DimTextColor,
            Text = "Enter to send · Shift+Enter new line · Ctrl+Backspace stop · Esc close",
            TextAlign = ContentAlignment.MiddleLeft
        };
        inputArea.Controls.Add(statusLabel);

        inputTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = TextColor,
            BackColor = InputBgColor,
            BorderStyle = BorderStyle.FixedSingle,
            Multiline = true,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = false
        };
        inputTextBox.KeyDown += OnInputKeyDown;
        inputArea.Controls.Add(inputTextBox);

        var bottomSep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = BorderColor };
        this.Controls.Add(bottomSep);
        this.Controls.Add(inputArea);
        bottomSep.BringToFront();

        // === CHAT AREA (center) ===
        chatArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgColor,
            AutoScroll = true,
            Padding = new Padding(8)
        };

        chatFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = BgColor,
            Padding = new Padding(4)
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

        menu.Show(modelLabel, new Point(0, modelLabel.Height));
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

        // Shift+Enter: new line
        if (e.Shift && e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            int pos = inputTextBox.SelectionStart;
            inputTextBox.Text = inputTextBox.Text.Insert(pos, Environment.NewLine);
            inputTextBox.SelectionStart = pos + Environment.NewLine.Length;
            return;
        }

        // Enter: send
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
        inputTextBox.Enabled = false;

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
            SafeInvoke(() => AppendSystemMessage(error));
        };

        orchestrator.OnComplete += () =>
        {
            SafeInvoke(() =>
            {
                shimmerLabel.IsShimmering = false;
                inputTextBox.Enabled = true;
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
            AppendSystemMessage($"Error: {ex.Message}");
        }
        finally
        {
            shimmerLabel.IsShimmering = false;
            inputTextBox.Enabled = true;
            inputTextBox.Focus();
        }
    }

    private void StopAgent()
    {
        session.Stop();
        shimmerLabel.IsShimmering = false;
        inputTextBox.Enabled = true;
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
        var toolLabel = new Label
        {
            Text = $"  ⚡ {toolInfo}",
            Font = new Font("Consolas", 8.5f),
            ForeColor = DimTextColor,
            BackColor = ToolBgColor,
            AutoSize = true,
            Padding = new Padding(4, 2, 4, 2)
        };
        parentPanel.Controls.Add(toolLabel);
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
        return panel;
    }

    private void ScrollToBottom()
    {
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
}
