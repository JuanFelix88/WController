using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WController.External;
using WController.Util;

namespace WController;

public partial class SearchItemsForm : Form
{
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams handleparam = base.CreateParams;
            handleparam.ExStyle |= 0x02000000;
            return handleparam;
        }
    }

    const int MOD_ALT = 0x1;
    const int MOD_CONTROL = 0x2;
    const int MOD_WIN = 0x8;
    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 9000;
    const int MaxDefaultSearchResults = 10;
    const int WindowScoreBoost = 100;

    delegate bool EnumWindowsPrc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static Color GetWindowsAccentColor()
    {
        return Color.FromArgb(32, 32, 32);
    }

    public static Color GetWindowsSecondaryColor()
    {
        return Color.FromArgb(57, 57, 57);
    }

    private void ApplyRoundedRegion(int radius)
    {
        Rectangle bounds = this.ClientRectangle;
        using (GraphicsPath path = GetRoundedPath(bounds, radius))
        {
            this.Region = new Region(path);
        }
    }


    private static Color GetFixedSolidColor(Color c)
    {
        var alphaDiff = 255 - c.A;
        float percent = (((float)c.A) * 1) / 255;

        int r = (int)Math.Floor(percent * c.R);
        int g = (int)Math.Floor(percent * c.G);
        int b = (int)Math.Floor(percent * c.B);

        if (r < 0) r = 0;
        if (g < 0) g = 0;
        if (b < 0) b = 0;

        return Color.FromArgb(r, g, b);
    }

    private static Color IncrementColor(Color c, float percent = 1.1f)
    {
        int r = (int)Math.Floor(percent * c.R);
        int g = (int)Math.Floor(percent * c.G);
        int b = (int)Math.Floor(percent * c.B);

        if (r > 255) r = 255;
        if (g > 255) g = 255;
        if (b > 255) b = 255;

        return Color.FromArgb(r, g, b);
    }

    private readonly Color AccentColor;
    private readonly Color SecondaryColor;
    private readonly Color TertiaryColor = Color.FromArgb(30, 76, 114);
    private readonly FileIndexes fileIndexes = new FileIndexes();
    private readonly PluginManager pluginManager = new();
    private readonly MainForm windowManager;
    private readonly Font windowShortcutFont = new Font("Consolas", 10, FontStyle.Underline);
    private MainForm.WindowItem[]? windowSearchItems;
    private int searchRequestVersion;

    public SearchItemsForm(MainForm windowManager)
    {
        this.windowManager = windowManager;
        AccentColor = GetWindowsAccentColor();
        SecondaryColor = GetWindowsSecondaryColor();
        InitializeComponent();
        this.Hide();
        var accentColor = GetFixedSolidColor(AccentColor);

        textBox.KeyDown += this.OnTextBoxKeyDown;

        this.BackColor = accentColor;
        this.listBox1.BackColor = accentColor;
        listBox1.DrawMode = DrawMode.OwnerDrawFixed;
        this.listBox1.DrawItem += this.listBox1_DrawItem;


        RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, Keys.Space);

        this.Resize += this.OnResize;
        this.Load += this.OnLoad;
        this.Paint += this.OnPaint;
        this.Deactivate += this.OnDeactivate;
        this.VisibleChanged += this.OnVisibleChanged;

        listBox1.DoubleClick += (s, e) => OpenSelectedItem();

        listBox1.KeyDown += this.OnListBoxKeyDown;
        ComputeHeightSize();

        pluginManager.PluginCalledByShortcut += this.OnPluginCalledByShortcut;
    }

    private void OnPluginCalledByShortcut(External.Plugin plugin)
    {
        searchRequestVersion++;
        windowSearchItems = null;
        if (!this.Visible) this.Show();
        this.Focus();
        this.textBox.Text = plugin.Shortcut + ": ";
        textBox.Focus();
        textBox.Select(textBox.Text.Length, 0);
        SearchWithText(textBox.Text);
        lbPlaceHolder.Visible = false;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Tab && textBox.Focused && !textBox.Text.Contains(" "))
        {
            textBox.Text += ": ";
            textBox.Focus();
            textBox.Select(textBox.Text.Length, 0);
            SearchWithText(textBox.Text);
            lbPlaceHolder.Visible = textBox.Text.Length == 0;
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnVisibleChanged(object sender, EventArgs e)
    {
        if (!this.Visible)
        {
            searchRequestVersion++;
            windowSearchItems = null;
            listBox1.Items.Clear();
        }
    }

    private void SearchWithText(string text)
    {
        var pluginSearch = pluginManager.ExtractPluginSearch(text);

        // plugins:
        if (pluginSearch is not null)
        {
            var (plugin, textSearch) = pluginSearch.Value;
            var pluginShortcuts = plugin.DispatchLoadItems(textSearch);

            textBox.ForeColor = Color.FromArgb(125, 255, 186);

            listBox1.SuspendLayout();
            listBox1.Items.Clear();

            foreach (var shortcut in pluginShortcuts)
                listBox1.Items.Add(shortcut);

            listBox1.Height = (listBox1.Items.Count * 24) + 5;

            if (listBox1.Items.Count > 0)
            {
                listBox1.SelectedIndex = 0;
            }

            listBox1.ResumeLayout();
            ComputeHeightSize();
            ApplyRoundedRegion(20);
            Refresh();
            return;
        }

        // Default search preserves the existing program index and adds open windows only for typed queries.
        textBox.ForeColor = Color.White;
        var programs = fileIndexes.SearchFiles(text, take: 100).ToArray();
        var windows = string.IsNullOrWhiteSpace(text)
            ? Array.Empty<MainForm.WindowItem>()
            : windowSearchItems ??= windowManager.GetSearchWindowItems().ToArray();
        var results = programs
            .Select((program, index) => new SearchItemResult(program, GetSearchScore(program.Name, text), index))
            .Where(result => result.Score >= 0)
            .Concat(windows
                .Select((window, index) => new SearchItemResult(
                    window,
                    GetWindowSearchScore(window, text),
                    programs.Length + index))
                .Where(result => result.Score >= 0))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.OriginalIndex)
            .Take(MaxDefaultSearchResults)
            .ToArray();

        listBox1.SuspendLayout();
        listBox1.Items.Clear();
        foreach (var result in results)
            listBox1.Items.Add(result.Item);

        listBox1.Height = (listBox1.Items.Count * 24) + 5;
        if (results.Length > 0)
            listBox1.SelectedIndex = 0;

        listBox1.ResumeLayout();
        ComputeHeightSize();
        ApplyRoundedRegion(20);
        Refresh();
    }

    private async void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up)
        {
            e.SuppressKeyPress = true;
            SelectPrevItem(true);
            return;
        }

        if (e.KeyCode == Keys.Down)
        {
            e.SuppressKeyPress = true;
            SelectNextItem(true);
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            this.Hide();
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            OpenSelectedItem();
            return;
        }

        if (e.KeyCode == Keys.Back && e.Control)
        {
            this.textBox.Text = string.Empty;
            e.SuppressKeyPress = true;
        }

        int requestVersion = ++searchRequestVersion;
        await Task.Delay(10);
        if (!Visible || requestVersion != searchRequestVersion)
            return;

        SearchWithText(textBox.Text);
        lbPlaceHolder.Visible = textBox.Text.Length == 0;
    }

    private void OnDeactivate(object sender, EventArgs e)
    {
        this.Hide();
    }

    private void OnListBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && e.Shift)
        {
            e.Handled = true;
            MessageBox.Show("Program closed.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Process.GetCurrentProcess().Kill();
            return;
        }
        if (e.KeyCode == Keys.Escape)
        {
            this.Hide();
            return;
        }
        if (e.KeyCode == Keys.Enter)
        {
            OpenSelectedItem();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void OpenSelectedItem()
    {
        if (listBox1.SelectedItem is MainForm.WindowItem window)
        {
            if (windowManager.ActivateWindowFromSearch(window))
                Hide();
            return;
        }

        if (listBox1.SelectedItem is ItemSelectable item)
        {
            item.Open();
            Hide();
        }
    }

    private static int GetWindowSearchScore(MainForm.WindowItem window, string query)
    {
        int score = Math.Max(
            GetSearchScore(window.ToStringWithoutShortcut(), query),
            GetSearchScore(window.Shortcut, query));
        return score < 0 ? score : score + WindowScoreBoost;
    }

    private static int GetSearchScore(string candidate, string query)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(query)) return -1;

        CompareInfo comparer = CultureInfo.CurrentCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        if (comparer.Compare(candidate, query, options) == 0) return 1_000_000;
        if (comparer.IsPrefix(candidate, query, options)) return 900_000 - Math.Min(candidate.Length - query.Length, 1_000);

        int wordPrefix = candidate.Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => comparer.IsPrefix(part, query, options))
            .ToList()
            .FindIndex(isPrefix => isPrefix);
        if (wordPrefix >= 0) return 800_000 - wordPrefix;

        int contains = comparer.IndexOf(candidate, query, options);
        return contains >= 0 ? 700_000 - Math.Min(contains, 1_000) : -1;
    }

    private sealed class SearchItemResult
    {
        public object Item { get; }
        public int Score { get; }
        public int OriginalIndex { get; }

        public SearchItemResult(object item, int score, int originalIndex)
        {
            Item = item;
            Score = score;
            OriginalIndex = originalIndex;
        }
    }

    private void ComputeHeightSize()
    {
        int newSize = listBox1.Items.Count * 24;
        newSize += 27;
        newSize += 15;

        if (newSize < 120) newSize = 120;

        Action func = () =>
        {
            this.Height = newSize;
        };

        if (this.InvokeRequired) this.Invoke(func);
        else func.Invoke();
    }

    private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        ListBox lb = (ListBox)sender;
        if (lb.Items[e.Index] is MainForm.WindowItem window)
        {
            DrawWindowItem(e, lb, window);
            return;
        }

        if (lb.Items[e.Index] is not ItemSelectable item) return;

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color backColor = selected ? TertiaryColor : lb.BackColor;
        Color foreColor = selected ? Color.White : lb.ForeColor;
        using var background = new SolidBrush(backColor);
        e.Graphics.FillRectangle(background, e.Bounds);

        int iconSize = e.Bounds.Height - 4;
        Rectangle iconRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, iconSize, iconSize);
        if (item.Image is not null)
            e.Graphics.DrawImage(item.Image, iconRect);

        Rectangle textRect = new Rectangle(iconRect.Right + 4, e.Bounds.Y, e.Bounds.Right - iconRect.Right - 4, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, item.ToString(), lb.Font, textRect, foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        if (selected)
            DrawSelectionBorder(e.Graphics, e.Bounds, TertiaryColor);
    }

    private void DrawWindowItem(DrawItemEventArgs e, ListBox listBox, MainForm.WindowItem item)
    {
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        bool enabled = item.Enabled;
        float opacity = !enabled ? MainForm.DisabledWindowOpacity : (item.IsIconic ? 0.8f : 1f);
        Color selectedColor = IncrementColor(TertiaryColor, opacity);
        Color backColor = selected ? selectedColor : listBox.BackColor;
        Color foreColor = selected ? Color.White : listBox.ForeColor;
        Color shortcutColor = Color.FromArgb(180, 255, 180);
        if (opacity < 1f)
        {
            foreColor = IncrementColor(foreColor, opacity);
            shortcutColor = IncrementColor(shortcutColor, opacity);
        }

        using var background = new SolidBrush(backColor);
        e.Graphics.FillRectangle(background, e.Bounds);

        int iconSize = e.Bounds.Height - 4;
        Rectangle iconRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, iconSize, iconSize);
        MainForm.DrawWindowIcon(e.Graphics, item, iconRect, enabled);

        int textX = iconRect.Right + 4;
        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            var shortcutBounds = new Rectangle(textX, e.Bounds.Y, 15, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, item.Shortcut, windowShortcutFont, shortcutBounds, shortcutColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            textX += 12;
        }

        int relevanceWidth = item.HighRelevance ? 12 : 0;
        Rectangle textBounds = new Rectangle(textX, e.Bounds.Y, Math.Max(0, e.Bounds.Right - textX - 4 - relevanceWidth), e.Bounds.Height);
        string title = item.ToStringWithoutShortcut();
        TextRenderer.DrawText(e.Graphics, title, listBox.Font, textBounds, foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (selected)
            DrawSelectionBorder(e.Graphics, e.Bounds, selectedColor);

        if (item.HighRelevance)
            e.Graphics.FillEllipse(Brushes.White, new Rectangle(e.Bounds.Right - 10, e.Bounds.Top + (e.Bounds.Height - 6) / 2, 6, 6));
    }

    private void DrawSelectionBorder(Graphics graphics, Rectangle bounds, Color selectedColor)
    {
        using var borderPen = new Pen(IncrementColor(selectedColor, 2f), 1.5f) { DashStyle = DashStyle.Dot };
        graphics.DrawRectangle(borderPen, new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1));
    }

    private void OnPaint(object sender, PaintEventArgs e)
    {
        int borderRadius = 20;
        int borderThickness = 2;
        Color borderColor = SecondaryColor;

        Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

        using (GraphicsPath path = GetRoundedPath(rect, borderRadius))
        using (Pen pen = new Pen(borderColor, borderThickness))
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }
    }

    private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 1;
        path.StartFigure();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void OnLoad(object sender, EventArgs e)
    {
        WindowEffects.TrySetAttribute(this.Handle, 33, 2);
        ApplyRoundedRegion(20);
    }

    private void OnResize(object sender, EventArgs e)
    {
        ApplyRoundedRegion(20);
    }

    private void SelectPrevItem(bool preventWrap = false)
    {
        if (listBox1.SelectedIndex != 0)
        {
            listBox1.SelectedIndex--;
        }
        else if (listBox1.Items.Count > 0 && !preventWrap)
        {
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
        }
    }

    private void SelectNextItem(bool preventWrap = false)
    {
        if (listBox1.SelectedIndex < listBox1.Items.Count - 1)
        {
            listBox1.SelectedIndex++;
        }
        else if (listBox1.Items.Count > 0 && !preventWrap)
        {
            listBox1.SelectedIndex = 0;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            if (this.Visible)
            {
                SelectNextItem();
                this.Activate();
            }
            else
            {
                this.WindowState = FormWindowState.Normal;
                this.PerformLayout();
                if (listBox1.Items.Count > 0) this.listBox1.SelectedIndex = 0;
                this.Show();
                this.Activate();
                this.BringToFront();
                this.Refresh();
                searchRequestVersion++;
                windowSearchItems = null;
                listBox1.Items.Clear();
                this.textBox.Text = string.Empty;
                lbPlaceHolder.Visible = textBox.Text.Length == 0;
                this.textBox.Focus();
            }

        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        pluginManager.RemoveAllListenersHotKeys();
        windowShortcutFont.Dispose();
        UnregisterHotKey(this.Handle, HOTKEY_ID);
        base.OnFormClosing(e);
    }
}
