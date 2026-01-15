using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WController.Forms;

public partial class SetWindowsAliasesForm : Form
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

    public static void ShowSettings(IEnumerable<MainForm.WindowItem> items)
    {
        using (var selfForm = new SetWindowsAliasesForm())
        {
            selfForm.windowsList = items.ToList();
            selfForm.ShowDialog();
        }
    }

    private readonly Color AccentColor;
    private readonly Color SecondaryColor;
    private readonly Color TertiaryColor = Color.FromArgb(30, 76, 114);
    private readonly Color shortcutColor = Color.FromArgb(180, 255, 180);
    private List<MainForm.WindowItem> windowsList = new List<MainForm.WindowItem>();
    private Font shortcutFont = new Font("Consolas", 10, FontStyle.Underline);

    public SetWindowsAliasesForm()
    {
        AccentColor = GetWindowsAccentColor();
        SecondaryColor = GetWindowsSecondaryColor();
        InitializeComponent();
        var accentColor = GetFixedSolidColor(AccentColor);

        this.BackColor = accentColor;
        this.listBox.BackColor = accentColor;
        listBox.DrawMode = DrawMode.OwnerDrawFixed;
        this.listBox.DrawItem += this.OnListBoxDrawItem;

        this.Resize += this.OnResize;
        this.Load += this.OnLoad;
        this.Paint += this.OnPaint;
        this.Deactivate += this.OnDeactivate;
        this.VisibleChanged += this.OnVisibleChanged;

        listBox.DoubleClick += (s, e) =>
        {
            OpenEdit((WindowConfigurable)listBox.Items[listBox.SelectedIndex]);
        };

        listBox.KeyDown += this.OnListBoxKeyDown;
        ComputeHeightSize();
    }

    private void OpenNew()
    {
        var editedList = listBox.Items.Cast<WindowConfigurable>().ToList();
        var newConfig = Forms.SetAliasForm.OpenNew(this.windowsList);

        if (newConfig is not null)
        {
            editedList.Add(newConfig);
            listBox.Items.Clear();
            WinSettingsStore.Save(editedList);
            listBox.Items.AddRange(editedList.ToArray());
            ComputeHeightSize();
        }
    }

    public void OpenEdit(WindowConfigurable winConfig)
    {
        var editedList = listBox.Items.Cast<WindowConfigurable>().ToList();
        var newCofigured = Forms.SetAliasForm.OpenEdit(winConfig);

        if (newCofigured is not null)
        {
            WinSettingsStore.Save(editedList);
            listBox.Items.Clear();
            listBox.Items.AddRange(editedList.ToArray());
            ComputeHeightSize();
        }
    }

    public void DeleteConfig(WindowConfigurable winConfig)
    {
        var newList = listBox.Items.Cast<WindowConfigurable>().Where(wc => wc != winConfig).ToList();

        WinSettingsStore.Save(newList);
        listBox.Items.Clear();
        listBox.Items.AddRange(newList.ToArray());
        ComputeHeightSize();
    }

    private void OnVisibleChanged(object sender, EventArgs e)
    {
        if (!this.Visible)
        {
            listBox.Items.Clear();
        }
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
            OpenEdit((WindowConfigurable)listBox.Items[listBox.SelectedIndex]);
            this.Hide();
            return;
        }

        await Task.Delay(10);
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
        if (e.KeyCode == Keys.Enter)
        {
            OpenEdit((WindowConfigurable)listBox.Items[listBox.SelectedIndex]);
            this.Hide();
            return;
        }
        if (e.KeyCode == Keys.Escape)
        {
            this.Hide();
            return;
        }
        if (e.KeyCode == Keys.F1)
        {
            OpenNew();
            return;
        }
        if (e.KeyCode == Keys.Delete)
        {
            var item = (WindowConfigurable)listBox.Items[listBox.SelectedIndex];
            this.DeleteConfig(item);
            return;
        }
    }

    private void ComputeHeightSize()
    {
        int newSize = listBox.Items.Count * 24;
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

    private void OnListBoxDrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        ListBox lb = (ListBox)sender;
        WindowConfigurable item = (WindowConfigurable)lb.Items[e.Index];
        string text = item.Title;
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        Color backColor = selected ? TertiaryColor : lb.BackColor;
        Color foreColor = selected ? Color.White : lb.ForeColor;

        e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);

        int textX = 4;

        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            Rectangle shortRect = new Rectangle(textX, e.Bounds.Y, 15, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, item.Shortcut, shortcutFont, shortRect, shortcutColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            textX += 12;
        }

        Rectangle textRect = new Rectangle(textX, e.Bounds.Y, e.Bounds.Width - textX, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, lb.Font, textRect, foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        if (selected)
        {
            using (Pen borderPen = new Pen(IncrementColor(TertiaryColor, 2f), 1.5f))
            {
                borderPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                Rectangle borderRect = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
                e.Graphics.DrawRectangle(borderPen, borderRect);
            }
        }
    }

    private void OnPaint(object sender, PaintEventArgs e)
    {
        int borderRadius = 20;
        int borderThickness = 2;
        Color borderColor = SecondaryColor;

        Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 4);

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
        ApplyRoundedRegion(20);
        LoadItemsFromStore();
    }

    protected void LoadItemsFromStore()
    {
        listBox.Items.Clear();
        WinSettingsStore.Load().ForEach(item => listBox.Items.Add(item));
        ComputeHeightSize();
    }

    private void OnResize(object sender, EventArgs e)
    {
        ApplyRoundedRegion(20);
    }

    private void SelectPrevItem(bool preventWrap = false)
    {
        if (listBox.SelectedIndex != 0)
        {
            listBox.SelectedIndex--;
        }
        else if (listBox.Items.Count > 0 && !preventWrap)
        {
            listBox.SelectedIndex = listBox.Items.Count - 1;
        }
    }

    private void SelectNextItem(bool preventWrap = false)
    {
        if (listBox.SelectedIndex < listBox.Items.Count - 1)
        {
            listBox.SelectedIndex++;
        }
        else if (listBox.Items.Count > 0 && !preventWrap)
        {
            listBox.SelectedIndex = 0;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterHotKey(this.Handle, HOTKEY_ID);
        base.OnFormClosing(e);
    }
}
