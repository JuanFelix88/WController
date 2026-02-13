using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WController.Properties;
using WController.Util;

namespace WController;

public partial class MainForm : Form
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
    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 9000;

    delegate bool EnumWindowsPrc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsPrc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_RESTORE = 9;
    const int SW_MINIMIZE = 6;

    const uint WM_SYSCOMMAND = 0x0112;
    const int SC_MINIMIZE = 0xF020;
    const int SC_RESTORE = 0xF120;

    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    const uint WM_CLOSE = 0x0010;

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(
        int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
        int nWidthEllipse, int nHeightEllipse);

    public static Color GetWindowsAccentColor()
    {
        return Color.FromArgb(32, 32, 32);
        //var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        //if (key != null)
        //{
        //    var value = key.GetValue("ColorizationColor");
        //    if (value != null)
        //    {
        //        int dword = (int)value;
        //        byte a = (byte)((dword >> 24) & 0xFF);
        //        byte r = (byte)((dword >> 16) & 0xFF);
        //        byte g = (byte)((dword >> 8) & 0xFF);
        //        byte b = (byte)(dword & 0xFF);
        //        return Color.FromArgb(a, r, g, b);
        //    }
        //}

        //return SystemColors.Highlight; // fallback
    }
    public static Color GetWindowsSecondaryColor()
    {
        return Color.FromArgb(57, 57, 57);
        //var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        //if (key != null)
        //{
        //    var value = key.GetValue("ColorizationColor");
        //    if (value != null)
        //    {
        //        int dword = (int)value;
        //        byte a = (byte)((dword >> 24) & 0xFF);
        //        byte r = (byte)((dword >> 16) & 0xFF);
        //        byte g = (byte)((dword >> 8) & 0xFF);
        //        byte b = (byte)(dword & 0xFF);
        //        return Color.FromArgb(a, r, g, b);
        //    }
        //}

        //return SystemColors.Highlight; // fallback
    }

    //protected override void OnHandleCreated(EventArgs e)
    //{
    //    base.OnHandleCreated(e);

    //    int radius = 20;
    //    IntPtr hRgn = CreateRoundRectRgn(0, 0, Width, Height, radius, radius);
    //    Region = Region.FromHrgn(hRgn);
    //}

    private void ApplyRoundedRegion(int radius)
    {
        Rectangle bounds = this.ClientRectangle;
        using (GraphicsPath path = GetRoundedPath(bounds, radius))
        {
            this.Region = new Region(path);
        }
    }

    //private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    //{
    //    int d = radius * 2;
    //    GraphicsPath path = new GraphicsPath();
    //    path.StartFigure();
    //    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
    //    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
    //    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
    //    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
    //    path.CloseFigure();
    //    return path;
    //}

    public void CloseWindow(IntPtr hWnd)
    {
        PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private void FocusWindow(WindowItem window)
    {
        if (IsIconic(window.Handle))
            ShowWindow(window.Handle, SW_RESTORE);

        SetForegroundWindow(window.Handle);
    }

    private void FocusInOneWindow(WindowItem window)
    {
        this.Hide();
        foreach (WindowItem itemWindow in listBox1.Items.Cast<WindowItem>().Reverse())
        {
            if (window == itemWindow) continue;
            if (itemWindow == null) continue;
            if (itemWindow.IsIconic) continue;
            HideWindow(itemWindow);
        }
        FocusWindow(window);

        if (listBox1.Items.Count > 0)
        {
            IncrementCount((WindowItem)listBox1.Items[0], window);
        }
    }

    private void ShowWindow(WindowItem window)
    {
        if (IsIconic(window.Handle))
        {
            ShowWindow(window.Handle, SW_RESTORE);
        }
        SetForegroundWindow(window.Handle);
        SetForegroundWindow(this.Handle);
        this.Show();
    }

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private void HideWindow(WindowItem window)
    {
        SendMessage(window.Handle, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
        SetForegroundWindow(this.Handle);
    }

    const int WM_GETICON = 0x007F;
    const int ICON_SMALL2 = 2;
    const int GCL_HICON = -14;

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("user32.dll", EntryPoint = "GetClassLong")]
    static extern uint GetClassLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
    static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

    static IntPtr GetClassLongAuto(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetClassLongPtr64(hWnd, nIndex);
        else
            return new IntPtr((long)GetClassLong32(hWnd, nIndex));
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowRect(IntPtr hWnd, out Rectangle lpRect);

    private void ChangeOpacityImage(Image image, float modifier)
    {
        if (image is Bitmap bmp)
        {
            {
                for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        Color esmaecido = Color.FromArgb((int)(c.A * modifier), c.R, c.G, c.B); // 50% opacidade
                        bmp.SetPixel(x, y, esmaecido);
                    }
            }
        }
    }

    public static Icon? GetWindowIcon(IntPtr hWnd)
    {
        if (!IsWindow(hWnd)) return null;

        IntPtr hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL2, 0);
        if (hIcon == IntPtr.Zero)
            hIcon = GetClassLongAuto(hWnd, GCL_HICON);

        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            Icon icon = Icon.FromHandle(hIcon);
            using (Bitmap bmp = icon.ToBitmap())
            {
                var points = new[]
                {
                    new Point(0, 0),
                    new Point(bmp.Width - 1, 0),
                    new Point(0, bmp.Height - 1),
                    new Point(bmp.Width - 1, bmp.Height - 1),
                    new Point(bmp.Width / 2, bmp.Height / 2)
                };

                foreach (var p in points)
                {
                    if (bmp.GetPixel(p.X, p.Y).A > 0)
                        return icon;
                }
            }
        }
        catch { }

        return null;
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
    private readonly Color ShortcutColor = Color.FromArgb(180, 255, 180);
    private Hashtable windowsToIgnore = new Hashtable(10);
    private Dictionary<IntPtr, (Image Icon, Image IconIconic)> iconsWindows = new Dictionary<IntPtr, (Image Icon, Image IconIconic)>(400);
    private Hashtable windowsRenames = new Hashtable();
    private Dictionary<IntPtr, string> windowsShortcuts = new Dictionary<IntPtr, string>();
    private Dictionary<(IntPtr, IntPtr), int> windowsReferences = new Dictionary<(IntPtr, IntPtr), int>();
    private Dictionary<IntPtr, string> windowsPaths = new Dictionary<IntPtr, string>();
    private Icon DefaultWindowIcon;
    private Image DefaultWindowIconImage;
    private Image DefaultWindowIconImageIconic;

    private SearchItemsForm searchItemsForm;
    private System.Timers.Timer temporaryTimerChecker = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
    private System.Timers.Timer temporaryTimer = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
    private float multiplierOpacityDecrement = 0.8f;
    private Font shortcutFont = new Font("Consolas", 10, FontStyle.Underline);
    private Rectangle previewRectangle;
    private IntPtr previewHandle = IntPtr.Zero;
    private bool isOneWindowMode = false;

    public MainForm()
    {
        AccentColor = GetWindowsAccentColor();
        SecondaryColor = GetWindowsSecondaryColor();
        InitializeComponent();
        this.Hide();
        var accentColor = GetFixedSolidColor(AccentColor);
        DefaultWindowIcon = IconFromBytes(Resources.DefaultWindow);
        DefaultWindowIconImage = DefaultWindowIcon.ToBitmap();
        DefaultWindowIconImageIconic = DefaultWindowIcon.ToBitmap();
        ChangeOpacityImage(DefaultWindowIconImageIconic, multiplierOpacityDecrement - 0.2f);

        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(listBox1, true, null);

        // preview rectangle:
        previewRectangle = new Rectangle(
            pnPreview.Location.X + 3,
            pnPreview.Location.Y + 2,
            pnPreview.Size.Width,
            pnPreview.Size.Height > 300 ? 300 : pnPreview.Size.Height);

        BackColor = accentColor;
        listBox1.BackColor = accentColor;
        listBox1.DrawMode = DrawMode.OwnerDrawVariable;
        listBox1.MeasureItem += this.OnListBox1MeasureItem;
        listBox1.DrawItem += this.OnListBox1DrawItem;
        listBox1.SelectedIndexChanged += this.OnListBoxSelectedIndexChanged;

        ComputeIgnoreWindows();
        LoadWindowList();
        RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, Keys.Oemtilde);
        //this.KeyPreview = true; // importante para o formulário capturar teclas antes dos controles
        //this.KeyDown += this.MainForm_KeyDown;


        this.Resize += this.MainForm_Resize;
        this.Load += this.MainForm_Load;
        this.Paint += this.MainForm_Paint;
        this.Deactivate += this.OnDeactivate;

        listBox1.DoubleClick += (s, e) =>
        {
            if (listBox1.SelectedItem is WindowItem item)
            {
                FocusWindow(item);
                this.Hide();
            }
        };
        listBox1.KeyDown += this.OnListBox1KeyDown;

        temporaryTimerChecker.Enabled = true;
        temporaryTimerChecker.AutoReset = true;
        temporaryTimerChecker.Elapsed += this.OnTemporaryTimerCheckerElapsed;
        temporaryTimerChecker.Start();

        searchItemsForm = new SearchItemsForm();
    }

    private void OnListBoxSelectedIndexChanged(object sender, EventArgs e)
    {
        if (previewHandle != IntPtr.Zero)
            WindowPreview.ClosePreview(previewHandle);

        if (listBox1.SelectedIndex == (-1) || listBox1.SelectedItem is null)
        {
            lbPreview.Text = "No selected Window";
            pnPreview.BackColor = AccentColor;
            return;
        }

        WindowItem selectedWindow = (WindowItem)listBox1.SelectedItem;
        Rectangle previewRect = GetProportionalRectangle(selectedWindow?.Handle ?? IntPtr.Zero, previewRectangle);

        previewHandle = WindowPreview.ShowPreview(
            this.Handle,
            selectedWindow?.Handle ?? IntPtr.Zero,
            previewRect,
            245);

        lbPreview.Text = string.Empty;
        lbSelectedWindow.Text = selectedWindow?.Title;
    }

    private void OnTemporaryTimerCheckerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        bool isChanged = false;

        foreach (var hWnd in iconsWindows.Keys.ToList())
        {
            if (iconsWindows[hWnd].Icon == DefaultWindowIconImage)
            {
                Icon? icon = GetWindowIcon(hWnd);
                if (icon is not null)
                {
                    Image imageIcon = icon.ToBitmap();
                    Image imageIconIconic = icon.ToBitmap();
                    ChangeOpacityImage(imageIconIconic, multiplierOpacityDecrement - 0.2f);

                    iconsWindows[hWnd] = (imageIcon, imageIconIconic);
                    isChanged = true;
                }
            }
        }

        if (isChanged == true)
        {
            Invoke((MethodInvoker)(() =>
            {
                foreach (WindowItem item in listBox1.Items)
                {
                    item.Icon = iconsWindows[item.Handle].Icon;
                    item.IconIconic = iconsWindows[item.Handle].IconIconic;
                }

                listBox1.Refresh();
            }));
        }
    }

    private void OnDeactivate(object sender, EventArgs e)
    {
        this.Hide();
    }

    //protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    //{
    //    if (keyData == Keys.Menu && hasAltCommandMode == true)
    //    {
    //        WindowItem selected = (WindowItem)listBox1.SelectedItem;

    //        if (selected != null)
    //        {
    //            FocusWindow(selected);
    //        }

    //        this.Hide();
    //        hasAltCommandMode = false;
    //    }

    //    return base.ProcessCmdKey(ref msg, keyData);
    //}

    private Icon IconFromBytes(byte[] iconBytes)
    {
        using (var ms = new MemoryStream(iconBytes))
        {
            return new Icon(ms);
        }
    }

    private Rectangle GetProportionalRectangle(IntPtr hWnd, Rectangle container)
    {
        if (!GetWindowRect(hWnd, out Rectangle windowRect))
            return container;

        int windowWidth = windowRect.Width;
        int windowHeight = windowRect.Height;
        double windowAspect = (double)windowWidth / windowHeight;
        double containerAspect = (double)container.Width / container.Height;

        int rectWidth, rectHeight;
        if (windowAspect > containerAspect)
        {
            rectWidth = container.Width;
            rectHeight = (int)(container.Width / windowAspect);
        }
        else
        {
            rectHeight = container.Height;
            rectWidth = (int)(container.Height * windowAspect);
        }

        int x = container.X + (container.Width - rectWidth) / 2;
        int y = container.Y + (container.Height - rectHeight) / 2 - ((container.Height - rectHeight) / 2 / 2);

        return new Rectangle(x, y, rectWidth, rectHeight);
    }

    private Color GetDominantColor(Image image)
    {
        if (image is not Bitmap bmp)
            return AccentColor;

        Dictionary<Color, int> colorFrequency = new Dictionary<Color, int>();
        int step = Math.Max(1, bmp.Width / 32);

        for (int y = 0; y < bmp.Height; y += step)
        {
            for (int x = 0; x < bmp.Width; x += step)
            {
                Color pixel = bmp.GetPixel(x, y);
                if (pixel.A < 128) continue;

                if (colorFrequency.ContainsKey(pixel))
                    colorFrequency[pixel]++;
                else
                    colorFrequency[pixel] = 1;
            }
        }

        return colorFrequency.Count > 0
            ? colorFrequency.OrderByDescending(x => x.Value).First().Key
            : AccentColor;
    }

    private void ComputeIgnoreWindows()
    {
        EnumWindows((hWnd, lParam) =>
        {
            if (hWnd == this.Handle || hWnd == IntPtr.Zero)
                return true;


            if (GetTypeWindow(this.Handle) == 0) return true;

            if (!IsWindowVisible(hWnd)) return true;

            string className = "";
            StringBuilder classNameRb = new StringBuilder(256);
            GetClassName(hWnd, classNameRb, classNameRb.Capacity);

            className = classNameRb.ToString();

            int length = GetWindowTextLength(hWnd);
            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);

            string windowTitle = sb.ToString();

            GetWindowThreadProcessId(hWnd, out uint pid);
            string processName = "";
            try
            {
                var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
            }
            catch { }

            if (processName == "TextInputHost")
            {
                windowsToIgnore.Add(hWnd, true);
                return true;
            }

            if (className == "Progman" && processName == "explorer")
            {
                windowsToIgnore.Add(hWnd, true);
                return true;
            }

            return true;
        }, IntPtr.Zero);
    }

    private void IncrementCount(WindowItem activeWindow, WindowItem targetWindow, int increments = 1)
    {
        if (windowsReferences.TryGetValue((activeWindow.Handle, targetWindow.Handle), out int count))
        {
            Task.Run(() =>
            {
                lock (windowsReferences)
                {
                    windowsReferences[(activeWindow.Handle, targetWindow.Handle)] = count + increments;
                }
            });
        }
        else
        {
            Task.Run(() =>
            {
                lock (windowsReferences)
                {
                    windowsReferences[(activeWindow.Handle, targetWindow.Handle)] = increments;
                }
            });
        }
    }

    private int CountWindowReferences(IntPtr activeWindow, IntPtr targetWindow)
    {
        if (activeWindow == targetWindow) return int.MaxValue;
        if (!windowsReferences.TryGetValue((activeWindow, targetWindow), out int count))
            return 0;

        return count;
    }

    private int CountWindowReferences(WindowItem activeWindow, WindowItem targetWindow)
        => CountWindowReferences(activeWindow.Handle, targetWindow.Handle);

    private void OnListBox1KeyDown(object sender, KeyEventArgs e)
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
        if (e.KeyCode == Keys.Left && listBox1.SelectedItem is WindowItem windowToHide)
        {
            int i = listBox1.SelectedIndex;
            HideWindow(windowToHide);
            listBox1.Items.Remove(windowToHide);
            ComputeLastSelectedIndex(i);

            windowToHide.IsIconic = true;
            listBox1.Items.Add(windowToHide);
            e.Handled = true;
            return;
        }
        if (e.KeyCode == Keys.Right && listBox1.SelectedItem is WindowItem windowToShow)
        {
            windowToShow.IsIconic = false;
            ShowWindow(windowToShow);
            e.Handled = true;
            return;
        }
        if (e.Shift && e.KeyCode == Keys.Enter && e.Alt && listBox1.SelectedItem is WindowItem itemDebug)
        {
            StringBuilder classNameRb = new StringBuilder(256);
            var value = GetClassName(itemDebug.Handle, classNameRb, classNameRb.Capacity);
            MessageBox.Show($"""
                title: {itemDebug.Title}
                type: {itemDebug.TypeWindow}
                class: {classNameRb.ToString()}
                int: {value}
            """);

            this.Hide();
            return;
        }
        if (e.Shift && e.KeyCode == Keys.Enter && listBox1.SelectedItem is WindowItem itemInforceSaltsIncrements)
        {
            int adderForClustering = 40;
            if (isOneWindowMode && !itemInforceSaltsIncrements.HighRelevance)
            {
                FocusInOneWindow(itemInforceSaltsIncrements);
                if (listBox1.Items.Count > 0)
                {
                    IncrementCount((WindowItem)listBox1.Items[0], itemInforceSaltsIncrements, adderForClustering - 1);
                }
            }
            else
            {
                FocusWindow(itemInforceSaltsIncrements);
                if (listBox1.Items.Count > 0)
                {
                    IncrementCount((WindowItem)listBox1.Items[0], itemInforceSaltsIncrements, adderForClustering);
                }
            }
            

            this.Hide();
            return;
        }
        if (e.Control && e.KeyCode == Keys.Enter && listBox1.SelectedItem is WindowItem windowToFocusWithControl)
        {
            // inverted:
            if (isOneWindowMode)
            {
                FocusWindow(windowToFocusWithControl);
                if (listBox1.Items.Count > 0)
                {
                    IncrementCount((WindowItem)listBox1.Items[0], windowToFocusWithControl);
                }
            }
            else
            {
                FocusInOneWindow(windowToFocusWithControl);
            }

            return;
        }
        if (e.KeyCode == Keys.Enter && listBox1.SelectedItem is WindowItem item)
        {
            if (isOneWindowMode && !item.HighRelevance)
            {
                FocusInOneWindow(item);
            }
            else
            {
                FocusWindow(item);
                if (listBox1.Items.Count > 0)
                {
                    IncrementCount((WindowItem)listBox1.Items[0], item);
                }
            }

            this.Hide();
            return;
        }
        if (e.Shift && e.KeyCode == Keys.Delete && listBox1.SelectedItem is WindowItem item3)
        {
            int i = listBox1.SelectedIndex;
            listBox1.Items.Remove(item3);
            ComputeHeightSize();
            ComputeLastSelectedIndex(i);
            KillWindowProcess(item3.Handle);
            return;
        }
        if (e.KeyCode == Keys.Delete && listBox1.SelectedItem is WindowItem item2)
        {
            int i = listBox1.SelectedIndex;
            listBox1.Items.Remove(item2);
            ComputeHeightSize();
            ComputeLastSelectedIndex(i);
            CloseWindow(item2.Handle);
            return;
        }
        if (e.KeyCode == Keys.F2 && listBox1.SelectedItem is WindowItem itemForRename)
        {
            var renameWindowModal = new RenameWindow()
            {
                SuggestName = itemForRename.ToStringWithoutShortcut(),
                IsOriginalSuggestName = itemForRename.IsOriginalTitle,
                SuggestShortcut = itemForRename.Shortcut
            };

            var renameResult = renameWindowModal.ShowDialog();

            if (renameResult != DialogResult.OK) return;

            if (renameWindowModal.NewName == string.Empty)
            {
                windowsRenames.Remove(itemForRename.Handle);
            }
            else
            {
                windowsRenames[itemForRename.Handle] = renameWindowModal.NewName;
            }

            if (string.IsNullOrEmpty(renameWindowModal.Shortcut))
            {
                windowsShortcuts.Remove(itemForRename.Handle);
            }
            else
            {
                windowsShortcuts[itemForRename.Handle] = renameWindowModal.Shortcut;
            }

            this.LoadWindowList();
        }
        if (e.KeyCode == Keys.F3)
        {
            Forms.SetWindowsAliasesForm.ShowSettings(listBox1.Items.Cast<WindowItem>());
            return;
        }
        if (e.KeyCode == Keys.F11)
        {
            isOneWindowMode = !isOneWindowMode;
            UpdateOneWindowModeIndicator();
            return;
        }
    }

    private void ComputeLastSelectedIndex(int i)
    {
        if (listBox1.Items.Count == 0) return;

        listBox1.SelectedIndex = i >= listBox1.Items.Count ? listBox1.Items.Count - 1 : i;
    }

    private void UpdateOneWindowModeIndicator()
    {
        if (isOneWindowMode)
        {
            lbSelectedWindow.Text = "[F11] Single Window Mode";
            lbSelectedWindow.ForeColor = Color.FromArgb(255, 180, 100);
        }
        else
        {
            lbSelectedWindow.Text = "...";
            lbSelectedWindow.ForeColor = Color.FromArgb(144, 145, 226);
        }
    }

    private void ComputeHeightSize()
    {
        Action func = () =>
        {
            var height = (listBox1.Items.Count * (listBox1.ItemHeight)) + (this.Padding.Vertical);
            this.Height = height < 345 ? 345 : height;
        };

        if (this.InvokeRequired) this.Invoke(func);
        else func.Invoke();
    }

    private void OnListBox1MeasureItem(object sender, MeasureItemEventArgs e)
    {
        e.ItemHeight = listBox1.ItemHeight;

        if (e.Index > 0 && e.Index < listBox1.Items.Count)
        {
            WindowItem current = (WindowItem)listBox1.Items[e.Index];
            WindowItem previous = (WindowItem)listBox1.Items[e.Index - 1];
            if (!current.HighRelevance && previous.HighRelevance)
            {
                e.ItemHeight += 5;
            }
        }
    }

    private void OnListBox1DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        ListBox lb = (ListBox)sender;
        WindowItem item = (WindowItem)lb.Items[e.Index];
        string text = item.ToStringWithoutShortcut();
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        Color selectedColor = TertiaryColor;
        Color foreColor = selected ? Color.White : lb.ForeColor;
        Color shortcutColor = ShortcutColor;

        if (item.IsIconic)
        {
            foreColor = IncrementColor(foreColor, multiplierOpacityDecrement);
            shortcutColor = IncrementColor(shortcutColor, multiplierOpacityDecrement);
            selectedColor = IncrementColor(selectedColor, multiplierOpacityDecrement);
        }

        Color backColor = selected ? selectedColor : lb.BackColor;

        // Aplica margin-top no primeiro item não-HighRelevance após um item HighRelevance
        int marginTop = 0;
        if (!item.HighRelevance && e.Index > 0)
        {
            WindowItem previousItem = (WindowItem)lb.Items[e.Index - 1];
            if (previousItem.HighRelevance)
                marginTop = 5;
        }

        // Desenha a faixa de margin-top com a cor de fundo do ListBox
        if (marginTop > 0)
        {
            Rectangle marginRect = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, marginTop);
            e.Graphics.FillRectangle(new SolidBrush(lb.BackColor), marginRect);
        }

        Rectangle effectiveBounds = new Rectangle(e.Bounds.X, e.Bounds.Y + marginTop, e.Bounds.Width, e.Bounds.Height - marginTop);

        e.Graphics.FillRectangle(new SolidBrush(backColor), effectiveBounds);

        int iconSize = effectiveBounds.Height - 4;
        Image icon = ((WindowItem)lb.Items[e.Index]).OutIcon;

        Rectangle iconRect = new Rectangle(effectiveBounds.X + 2, effectiveBounds.Y + 2, iconSize, iconSize);

        if (icon != null)
            e.Graphics.DrawImage(icon, iconRect);

        int textX = iconRect.Right + 4;

        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            Rectangle shortRect = new Rectangle(textX, effectiveBounds.Y, 15, effectiveBounds.Height);
            TextRenderer.DrawText(e.Graphics, item.Shortcut, shortcutFont, shortRect, shortcutColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            textX += 12;
        }

        Rectangle textRect = new Rectangle(textX, effectiveBounds.Y, effectiveBounds.Width - textX, effectiveBounds.Height);
        TextRenderer.DrawText(e.Graphics, text, lb.Font, textRect, foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        if (selected)
        {
            using (Pen borderPen = new Pen(IncrementColor(selectedColor, 2f), 1.5f))
            {
                borderPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                Rectangle borderRect = new Rectangle(effectiveBounds.X, effectiveBounds.Y, effectiveBounds.Width - 1, effectiveBounds.Height - 1);
                e.Graphics.DrawRectangle(borderPen, borderRect);
            }
        }

        if (item.HighRelevance)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int diameter = 6;
            int marginRight = 6;
            int circleX = effectiveBounds.Right - diameter - marginRight;
            int circleY = effectiveBounds.Top + (effectiveBounds.Height - diameter) / 2;
            Rectangle circleRect = new Rectangle(circleX, circleY, diameter, diameter);

            using (Brush whiteBrush = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(whiteBrush, circleRect);
            }
        }
    }

    //private void ListBox1_DrawItem(object sender, DrawItemEventArgs e)
    //{
    //    if (e.Index < 0) return;

    //    ListBox lb = (ListBox)sender;
    //    string text = lb.Items[e.Index].ToString();

    //    bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

    //    Color backColor = selected ? TertiaryColor : lb.BackColor;
    //    Color foreColor = selected ? Color.White : lb.ForeColor;

    //    using (SolidBrush bg = new SolidBrush(backColor))
    //        e.Graphics.FillRectangle(bg, e.Bounds);

    //    TextRenderer.DrawText(e.Graphics, text, lb.Font, e.Bounds, foreColor,
    //        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

    //    if (selected)
    //    {
    //        using (Pen borderPen = new Pen(IncrementColor(TertiaryColor, 1.3f)))
    //        {
    //            Rectangle borderRect = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
    //            e.Graphics.DrawRectangle(borderPen, borderRect);
    //        }
    //    }

    //}

    private void MainForm_Paint(object sender, PaintEventArgs e)
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

    private void MainForm_Load(object sender, EventArgs e)
    {
        ApplyRoundedRegion(20);
    }

    private void MainForm_Resize(object sender, EventArgs e)
    {
        ApplyRoundedRegion(20);
    }

    public void KillWindowProcess(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        try
        {
            Process proc = Process.GetProcessById((int)pid);
            proc.Kill();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao encerrar processo: " + ex.Message);
        }
    }

    private void MainForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Alt)
        {
            MessageBox.Show($"Alt + KeyCode: {e.KeyCode} (KeyValue: {(int)e.KeyCode})");
        }
        else
        {
            MessageBox.Show($"KeyCode: {e.KeyCode} (KeyValue: {(int)e.KeyCode})");
        }
    }

    private void OnLeave(object sender, EventArgs e)
    {
        this.Hide();
    }

    private (Image Icon, Image IconIconic) GetWindowIconImageEnhanced(IntPtr hWnd)
    {
        if (iconsWindows.ContainsKey(hWnd))
        {
            return iconsWindows[hWnd];
        }
        else
        {
            Icon? icon = GetWindowIcon(hWnd);

            Image image = icon is null ? DefaultWindowIconImage : icon.ToBitmap();
            Image imageIconic = icon is null ? DefaultWindowIconImageIconic : icon.ToBitmap();

            if (imageIconic != DefaultWindowIconImageIconic)
            {
                ChangeOpacityImage(imageIconic, multiplierOpacityDecrement - 0.2f);
            }
            iconsWindows[hWnd] = (image, imageIconic);
            return (image, imageIconic);
        }
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    const int GWL_EXSTYLE = -20;
    const long WS_EX_TOOLWINDOW = 0x00000080;
    const long WS_EX_APPWINDOW = 0x00040000;
    const long WS_EX_TOPMOST = 0x00000008;

    /// <returns>0 - invalid, 1 - normal, 2 - topmost</returns>
    static int GetTypeWindow(IntPtr hWnd)
    {
        long exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        if ((exStyle & WS_EX_TOOLWINDOW) != 0) return 0;
        //if ((exStyle & WS_EX_APPWINDOW) != 0) return 0;
        if ((exStyle & WS_EX_TOPMOST) != 0) return 2;
        return 1;
    }

    private bool IsOtherWindow(IntPtr hWnd)
    {
        StringBuilder classNameRb = new StringBuilder(256);
        GetClassName(hWnd, classNameRb, classNameRb.Capacity);

        string className = classNameRb.ToString().Trim();

        if (className == "Windows.UI.Core.CoreWindow")
        {
            return true;
        }

        if (className == "Xaml_WindowedPopupClass")
        {
            return true;
        }

        return false;
    }


    private void LoadWindowList()
    {
        listBox1.Items.Clear();
        listBox1.SuspendLayout();
        IntPtr? firstHandle = null;
        var winSettings = WinSettingsStore.LoadFromCache();
        
        EnumWindows((hWnd, lParam) =>
        {
            if (hWnd == this.Handle || hWnd == IntPtr.Zero)
                return true;

            var typeWindow = GetTypeWindow(hWnd);

            if (typeWindow == 0) return true;

            if (windowsToIgnore.Contains(hWnd))
            {
                return true;
            }

            if (!IsWindow(hWnd))
            {
                return true;
            }

            if (IsOtherWindow(hWnd))
            {
                return true;
            }

            if (IsWindowVisible(hWnd))
            {
                int countReferences = int.MaxValue;
                if (firstHandle == null) firstHandle = hWnd;
                else countReferences = CountWindowReferences(firstHandle.Value, hWnd);

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;
                StringBuilder sb = new StringBuilder(length + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                var images = GetWindowIconImageEnhanced(hWnd);

                string? renamedTitleFetchResult = windowsRenames.ContainsKey(hWnd) ? (string)windowsRenames[hWnd] : null;
                string? shortcut = windowsShortcuts.ContainsKey(hWnd) ? windowsShortcuts[hWnd] : null;
                if (shortcut is null)
                {
                    string programPath = windowsPaths.TryGetValue(hWnd, out string? existingPath)
                        ? existingPath
                        : WinHelper.GetPathFromHandle(hWnd);
                    
                    if (existingPath is null)
                    {
                        windowsPaths[hWnd] = programPath;
                    }

                    string windowTitle = sb.ToString();
                    var matchedSetting = winSettings.FirstOrDefault(setting => setting.Matches(windowTitle, programPath));

                    if (matchedSetting is not null)
                    {
                        shortcut = matchedSetting.Shortcut;
                    }
                }

                listBox1.Items.Add(new WindowItem
                {
                    Handle = hWnd,
                    TypeWindow = typeWindow,
                    Title = sb.ToString(),
                    RenamedTitle = renamedTitleFetchResult,
                    Icon = images.Icon,
                    IconIconic = images.IconIconic,
                    IsIconic = IsIconic(hWnd),
                    Shortcut = shortcut ?? string.Empty,
                    CountReferences = countReferences
                });
            }

            return true;
        }, IntPtr.Zero);

        if (listBox1.Items.Count > 1)
        {
            var list = listBox1.Items.Cast<WindowItem>().ToList();
            var activeWindow = (WindowItem)listBox1.Items[0];

            listBox1.Items.Clear();
            int deltaChecks = 4;
            double average = list
                .Where(itemForAverage => itemForAverage != activeWindow)
                .Average(itemForAverage => itemForAverage.CountReferences);

            list
                .Select(x =>
                {
                    if (x == activeWindow) return x;

                    x.HighRelevance = (x.CountReferences - deltaChecks) > average;
                    return x;
                })
                .OrderBy(item => item.TypeWindow)
                .ThenByDescending(item => item.CountReferences)
                .ToList()
                .ForEach(item => listBox1.Items.Add(item));
        }

        listBox1.ResumeLayout();
        ComputeHeightSize();
    }

    private void buttonFocus_Click(object sender, EventArgs e)
    {
        if (listBox1.SelectedItem is WindowItem item)
            SetForegroundWindow(item.Handle);
    }

    private void SelectNextItem()
    {
        if (listBox1.SelectedIndex < listBox1.Items.Count - 1)
        {
            listBox1.SelectedIndex++;
        }
        else
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
                this.PerformLayout();
                this.LoadWindowList();
                if (listBox1.Items.Count > 0) this.listBox1.SelectedIndex = 0;
                UpdateOneWindowModeIndicator();
                this.Show();
                this.Activate();
                this.BringToFront();
                Task.Delay(20).ContinueWith(t => Invoke(new Action(() => this.Invalidate())));
            }
        }

        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterHotKey(this.Handle, HOTKEY_ID);
        base.OnFormClosing(e);
    }

    public class WindowItem
    {
        public required IntPtr Handle { get; set; }
        public required string Title { get; set; }
        public required int TypeWindow { get; set; }
        public required int CountReferences { get; set; }
        public bool HighRelevance { get; set; } = false;
        public required string? RenamedTitle { get; set; }
        public required string Shortcut { get; set; } = string.Empty;
        public required bool IsIconic { get; set; }
        public required Image Icon
        {
            get => field;
            set => field = Resizer.ResizeImage(value, Resizer.DefaultIconViewSize, Resizer.DefaultIconViewSize);
        }
        public required Image IconIconic
        {
            get => field;
            set => field = Resizer.ResizeImage(value, Resizer.DefaultIconViewSize, Resizer.DefaultIconViewSize);
        }
        public Image OutIcon => IsIconic ? IconIconic : Icon;
        public bool IsOriginalTitle => string.IsNullOrEmpty(RenamedTitle) && string.IsNullOrEmpty(Shortcut);

        #region Methods
        public string ToStringWithoutShortcut()
        {
            return !string.IsNullOrEmpty(RenamedTitle) ? RenamedTitle! : Title;
        }

        public override string ToString()
        {
            string outTitle = !string.IsNullOrEmpty(RenamedTitle) ? RenamedTitle! : Title;

            if (!string.IsNullOrEmpty(Shortcut))
            {
                return $"{Shortcut}{outTitle}";
            }

            return outTitle;
        }
        #endregion
    }
}
