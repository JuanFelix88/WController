using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WController.Agent.UI;
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
    const int MOD_SHIFT = 0x4;
    const int WM_HOTKEY = 0x0312;
    const int HOTKEY_ID = 9000;
    const int GROUPED_HOTKEY_ID = 9001;
    const int DesktopIndicatorWidth = 4;
    const int DesktopIndicatorMargin = 4;
    const int DesktopGroupTitleHeight = 22;

    delegate bool EnumWindowsPrc(IntPtr hWnd, IntPtr lParam);

    [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
    }

    [ComImport, Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")]
    private class VirtualDesktopManager
    {
    }

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsPrc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100;
    const int WM_SYSKEYDOWN = 0x0104;
    const byte VK_APPS = 0x5D;
    const byte VK_LWIN = 0x5B;
    const byte VK_RWIN = 0x5C;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const byte VK_BROWSER_SEARCH = 0xAA;
    const byte VK_LAUNCH_APP1 = 0xB6;
    const byte VK_LAUNCH_APP2 = 0xB7;
    const byte VK_F23 = 0x86;

    private IntPtr keyboardHookId = IntPtr.Zero;
    private LowLevelKeyboardProc? keyboardHookProc;
    private bool isWinKeyDown = false;

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

    /// <summary>
    /// Obtém o nome do desktop virtual que contém a janela de nível superior informada.
    /// </summary>
    /// <exception cref="ArgumentNullException">Lançada quando <paramref name="hWnd"/> é zero.</exception>
    /// <exception cref="ArgumentException">Lançada quando <paramref name="hWnd"/> não representa uma janela válida.</exception>
    /// <exception cref="InvalidOperationException">Lançada quando o desktop da janela não pode ser encontrado.</exception>
    public string GetVirtualDesktopName(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            throw new ArgumentNullException(nameof(hWnd));

        if (!IsWindow(hWnd))
            throw new ArgumentException("O handle informado não representa uma janela válida.", nameof(hWnd));

        Guid desktopId = GetWindowDesktopId(hWnd);

        if (TryGetVirtualDesktopName(desktopId, out string desktopName))
            return desktopName;

        RefreshVirtualDesktopCache();

        if (TryGetVirtualDesktopName(desktopId, out desktopName))
            return desktopName;

        throw new InvalidOperationException("A janela não está associada a um desktop virtual identificável.");
    }

    /// <summary>
    /// Atualiza o cache de nomes dos desktops virtuais a partir do Registro do Explorer.
    /// Chame este método após criar, excluir, reordenar ou renomear desktops.
    /// </summary>
    public void RefreshVirtualDesktopCache()
    {
        const string virtualDesktopsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";
        const int guidSize = 16;

        using RegistryKey? virtualDesktopsKey = Registry.CurrentUser.OpenSubKey(virtualDesktopsPath, writable: false);
        if (virtualDesktopsKey is null)
            throw new InvalidOperationException("A configuração de desktops virtuais do Explorer não foi encontrada.");

        if (virtualDesktopsKey.GetValue("VirtualDesktopIDs") is not byte[] desktopIds || desktopIds.Length == 0 || desktopIds.Length % guidSize != 0)
            throw new InvalidOperationException("A lista de desktops virtuais do Explorer é inválida.");

        Dictionary<Guid, string> updatedDesktopNames = new Dictionary<Guid, string>(desktopIds.Length / guidSize);
        for (int index = 0; index < desktopIds.Length / guidSize; index++)
        {
            byte[] idBytes = new byte[guidSize];
            Array.Copy(desktopIds, index * guidSize, idBytes, 0, guidSize);

            Guid desktopId = new Guid(idBytes);
            using RegistryKey? desktopKey = virtualDesktopsKey.OpenSubKey($@"Desktops\{desktopId:B}", writable: false);
            string? configuredName = desktopKey?.GetValue("Name") as string;
            updatedDesktopNames.Add(desktopId, string.IsNullOrWhiteSpace(configuredName)
                ? $"Desktop {index + 1}"
                : configuredName!);
        }

        lock (virtualDesktopCacheSync)
        {
            virtualDesktopNames = updatedDesktopNames;
            isVirtualDesktopCacheLoaded = true;
        }
    }

    private static Guid GetWindowDesktopId(IntPtr hWnd)
    {
        IVirtualDesktopManager? manager = null;
        try
        {
            manager = (IVirtualDesktopManager)new VirtualDesktopManager();
            int hResult = manager.GetWindowDesktopId(hWnd, out Guid desktopId);
            if (hResult != 0)
                Marshal.ThrowExceptionForHR(hResult);

            return desktopId;
        }
        finally
        {
            if (manager is not null && Marshal.IsComObject(manager))
                Marshal.ReleaseComObject(manager);
        }
    }

    private bool TryGetVirtualDesktopName(Guid desktopId, out string desktopName)
    {
        lock (virtualDesktopCacheSync)
        {
            if (!isVirtualDesktopCacheLoaded)
            {
                desktopName = string.Empty;
                return false;
            }

            bool found = virtualDesktopNames.TryGetValue(desktopId, out string? cachedDesktopName);
            desktopName = cachedDesktopName ?? string.Empty;
            return found;
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
    private static readonly Color[] DesktopIndicatorColors =
    {
        Color.FromArgb(0, 122, 204),
        Color.FromArgb(157, 95, 255),
        Color.FromArgb(0, 220, 140),
        Color.FromArgb(255, 130, 0),
        Color.FromArgb(255, 75, 130),
        Color.FromArgb(235, 200, 0)
    };
    private Hashtable windowsToIgnore = new Hashtable(10);
    private Dictionary<IntPtr, (Image Icon, Image IconIconic)> iconsWindows = new Dictionary<IntPtr, (Image Icon, Image IconIconic)>(400);
    private Hashtable windowsRenames = new Hashtable();
    private Dictionary<IntPtr, string> windowsShortcuts = new Dictionary<IntPtr, string>();
    private Dictionary<(IntPtr, IntPtr), int> windowsReferences = new Dictionary<(IntPtr, IntPtr), int>();
    private Dictionary<IntPtr, string> windowsPaths = new Dictionary<IntPtr, string>();
    private readonly Dictionary<string, Color> desktopIndicatorColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
    private readonly object virtualDesktopCacheSync = new object();
    private Dictionary<Guid, string> virtualDesktopNames = new Dictionary<Guid, string>();
    private bool isVirtualDesktopCacheLoaded;
    private bool isPreviewVisible = true;
    private bool isDesktopGroupedView;
    private bool isDesktopGroupingHotkeyInverted;
    private Icon DefaultWindowIcon;
    private Image DefaultWindowIconImage;
    private Image DefaultWindowIconImageIconic;

    private SearchItemsForm searchItemsForm;
    private AgentChatForm? agentChatForm;
    private System.Timers.Timer temporaryTimerChecker = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
    private System.Timers.Timer temporaryTimer = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
    private float multiplierOpacityDecrement = 0.8f;
    private Font shortcutFont = new Font("Consolas", 10, FontStyle.Underline);
    private Font desktopTitleFont = new Font("Consolas", 10, FontStyle.Regular);
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
        RegisterHotKey(this.Handle, GROUPED_HOTKEY_ID, MOD_ALT | MOD_SHIFT, Keys.Oemtilde);
        InstallKeyboardHook();
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

        // Ctrl+I opens agent chat
        this.KeyPreview = true;
        this.KeyDown += this.OnMainFormKeyDown;
    }

    private void OnMainFormKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.I)
        {
            e.SuppressKeyPress = true;
            OpenAgentChat();
        }
    }

    private void OpenAgentChat()
    {
        if (agentChatForm == null || agentChatForm.IsDisposed)
        {
            agentChatForm = new AgentChatForm();
        }
        agentChatForm.ShowAgent();
    }

    private void OnListBoxSelectedIndexChanged(object sender, EventArgs e)
    {
        if (previewHandle != IntPtr.Zero)
            WindowPreview.ClosePreview(previewHandle);

        if (!isPreviewVisible)
            return;

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

    private int fullWidth;

    private void TogglePreviewPanel()
    {
        isPreviewVisible = !isPreviewVisible;

        if (!isPreviewVisible)
        {
            if (previewHandle != IntPtr.Zero)
            {
                WindowPreview.ClosePreview(previewHandle);
                previewHandle = IntPtr.Zero;
            }

            pnPreview.Visible = false;
            fullWidth = this.Width;
            this.Width = pnLists.Width + this.Padding.Horizontal;
            this.CenterToScreen();
        }
        else
        {
            pnPreview.Visible = true;
            this.Width = fullWidth;
            this.CenterToScreen();

            if (listBox1.SelectedItem is WindowItem selectedWindow)
            {
                Rectangle previewRect = GetProportionalRectangle(selectedWindow.Handle, previewRectangle);
                previewHandle = WindowPreview.ShowPreview(
                    this.Handle,
                    selectedWindow.Handle,
                    previewRect,
                    245);
            }
        }
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

    private void OnListBox1KeyDown(object sender, KeyEventArgs e)
    {
        if (TryHandleKeybindForSingleMatchWindow(e)) return;
        if (TryHandleAppExit(e)) return;
        if (TryHandleWindowToggle(e)) return;
        if (TryHandleWindowActivation(e)) return;
        if (TryHandleWindowRemoval(e)) return;
        if (TryHandleRename(e)) return;
        if (TryHandleViewModes(e)) return;
    }

    private bool TryHandleKeybindForSingleMatchWindow(KeyEventArgs e)
    {
        if (e.Shift || e.Control || e.Alt) return false;
        if (!((e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z) ||
              (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9))) return false;

        char input = char.ToLowerInvariant((char)e.KeyValue);
        bool MatchKey(WindowItem w) =>
            char.ToLowerInvariant((!string.IsNullOrEmpty(w.Shortcut) ? w.Shortcut[0] : w.ToStringWithoutShortcut()[0])) == input;

        var matches = listBox1.Items.Cast<WindowItem>().Where(MatchKey).ToList();

        if (matches.Count != 1) return false;

        var item = matches[0];
        if (isOneWindowMode && !item.HighRelevance)
            FocusInOneWindow(item);
        else
        {
            FocusWindow(item);
            if (listBox1.Items.Count > 0)
                IncrementCount((WindowItem)listBox1.Items[0], item);
        }
        this.Hide();
        e.Handled = true;
        return true;
    }

    private bool TryHandleAppExit(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && e.Shift)
        {
            e.Handled = true;
            MessageBox.Show("Program closed.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Process.GetCurrentProcess().Kill();
            return true;
        }
        if (e.KeyCode == Keys.Escape)
        {
            this.Hide();
            return true;
        }
        return false;
    }

    private bool TryHandleWindowToggle(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left && listBox1.SelectedItem is WindowItem windowToHide)
        {
            int i = listBox1.SelectedIndex;
            HideWindow(windowToHide);
            listBox1.Items.Remove(windowToHide);
            ComputeLastSelectedIndex(i);

            windowToHide.IsIconic = true;
            listBox1.Items.Add(windowToHide);
            e.Handled = true;
            return true;
        }
        if (e.KeyCode == Keys.Right && listBox1.SelectedItem is WindowItem windowToShow)
        {
            windowToShow.IsIconic = false;
            ShowWindow(windowToShow);
            e.Handled = true;
            return true;
        }
        return false;
    }

    private bool TryHandleWindowActivation(KeyEventArgs e)
    {
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
            return true;
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
            return true;
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

            return true;
        }
        if (e.KeyCode == Keys.Space && !e.Shift && !e.Control && !e.Alt && listBox1.SelectedItem is WindowItem itemSpace)
        {
            if (isOneWindowMode && !itemSpace.HighRelevance)
            {
                FocusInOneWindow(itemSpace);
            }
            else
            {
                FocusWindow(itemSpace);
                if (listBox1.Items.Count > 0)
                {
                    IncrementCount((WindowItem)listBox1.Items[0], itemSpace);
                }
            }

            this.Hide();
            return true;
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
            return true;
        }
        return false;
    }

    private bool TryHandleWindowRemoval(KeyEventArgs e)
    {
        if (e.Shift && e.KeyCode == Keys.Delete && listBox1.SelectedItem is WindowItem item3)
        {
            int i = listBox1.SelectedIndex;
            listBox1.Items.Remove(item3);
            ComputeHeightSize();
            ComputeLastSelectedIndex(i);
            KillWindowProcess(item3.Handle);
            return true;
        }
        if (e.KeyCode == Keys.Delete && listBox1.SelectedItem is WindowItem item2)
        {
            int i = listBox1.SelectedIndex;
            listBox1.Items.Remove(item2);
            ComputeHeightSize();
            ComputeLastSelectedIndex(i);
            CloseWindow(item2.Handle);
            return true;
        }
        return false;
    }

    private bool TryHandleRename(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F2 && listBox1.SelectedItem is WindowItem itemForRename)
        {
            var renameWindowModal = new RenameWindow()
            {
                SuggestName = itemForRename.ToStringWithoutShortcut(),
                IsOriginalSuggestName = itemForRename.IsOriginalTitle,
                SuggestShortcut = itemForRename.Shortcut
            };

            var renameResult = renameWindowModal.ShowDialog();

            if (renameResult != DialogResult.OK) return true;

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

            this.LoadWindowList(isDesktopGroupedView);
            return true;
        }
        if (e.KeyCode == Keys.F3)
        {
            Forms.SetWindowsAliasesForm.ShowSettings(listBox1.Items.Cast<WindowItem>());
            return true;
        }
        return false;
    }

    private bool TryHandleViewModes(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F8)
        {
            isDesktopGroupingHotkeyInverted = !isDesktopGroupingHotkeyInverted;
            LoadWindowList(isDesktopGroupingHotkeyInverted);
            if (listBox1.Items.Count > 0)
                listBox1.SelectedIndex = 0;
            UpdateOneWindowModeIndicator();
            return true;
        }
        if (e.KeyCode == Keys.F10)
        {
            TogglePreviewPanel();
            return true;
        }
        if (e.KeyCode == Keys.F11)
        {
            isOneWindowMode = !isOneWindowMode;
            UpdateOneWindowModeIndicator();
            return true;
        }
        return false;
    }

    private void ComputeLastSelectedIndex(int i)
    {
        if (listBox1.Items.Count == 0) return;

        listBox1.SelectedIndex = i >= listBox1.Items.Count ? listBox1.Items.Count - 1 : i;
    }

    private void UpdateOneWindowModeIndicator()
    {
        if (isDesktopGroupingHotkeyInverted || isOneWindowMode)
        {
            var activeModes = new List<string>();
            if (isDesktopGroupingHotkeyInverted)
                activeModes.Add("[F8] Desktop Grouping Mode");
            if (isOneWindowMode)
                activeModes.Add("[F11] Single Window Mode");

            lbSelectedWindow.Text = string.Join(" | ", activeModes);
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
            int itemsHeight = 0;
            for (int index = 0; index < listBox1.Items.Count; index++)
                itemsHeight += listBox1.GetItemHeight(index);

            int height = itemsHeight + this.Padding.Vertical;
            this.Height = height < 345 ? 345 : height;
        };

        if (this.InvokeRequired) this.Invoke(func);
        else func.Invoke();
    }

    private void OnListBox1MeasureItem(object sender, MeasureItemEventArgs e)
    {
        WindowItem item = (WindowItem)listBox1.Items[e.Index];
        e.ItemHeight = listBox1.ItemHeight + (item.ShowsDesktopTitle ? DesktopGroupTitleHeight : 0);
    }

    private void OnListBox1DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        ListBox lb = (ListBox)sender;
        WindowItem item = (WindowItem)lb.Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        int titleHeight = item.ShowsDesktopTitle ? DesktopGroupTitleHeight : 0;
        Rectangle titleBounds = new Rectangle(e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 4, titleHeight);
        Rectangle contentBounds = new Rectangle(e.Bounds.X, e.Bounds.Y + titleHeight, e.Bounds.Width, e.Bounds.Height - titleHeight);

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
        using (Brush backgroundBrush = new SolidBrush(lb.BackColor))
        using (Brush contentBrush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
            e.Graphics.FillRectangle(contentBrush, contentBounds);
        }

        if (item.ShowsDesktopTitle)
        {
            TextRenderer.DrawText(e.Graphics, item.DesktopName, desktopTitleFont, titleBounds, ShortcutColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        int iconSize = contentBounds.Height - 4;
        Image icon = item.OutIcon;
        Rectangle iconRect = new Rectangle(contentBounds.X + 2, contentBounds.Y + 2, iconSize, iconSize);

        e.Graphics.DrawImage(icon, iconRect);

        int textX = iconRect.Right + 4;
        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            Rectangle shortRect = new Rectangle(textX, contentBounds.Y, 15, contentBounds.Height);
            TextRenderer.DrawText(e.Graphics, item.Shortcut, shortcutFont, shortRect, shortcutColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            textX += 12;
        }

        Rectangle indicatorRect = new Rectangle(
            contentBounds.Right - DesktopIndicatorMargin - DesktopIndicatorWidth,
            contentBounds.Y + 3,
            DesktopIndicatorWidth,
            Math.Max(1, contentBounds.Height - 6));
        int relevanceWidth = item.HighRelevance ? 12 : 0;
        int textRight = indicatorRect.Left - DesktopIndicatorMargin - relevanceWidth;
        Rectangle textRect = new Rectangle(textX, contentBounds.Y, Math.Max(0, textRight - textX), contentBounds.Height);
        TextRenderer.DrawText(e.Graphics, item.ToDisplayString(), lb.Font, textRect, foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (selected)
        {
            using (Pen borderPen = new Pen(IncrementColor(selectedColor, 2f), 1.5f))
            {
                borderPen.DashStyle = DashStyle.Dot;
                Rectangle borderRect = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width - 1, contentBounds.Height - 1);
                e.Graphics.DrawRectangle(borderPen, borderRect);
            }
        }

        if (item.HighRelevance)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            const int diameter = 6;
            int circleX = indicatorRect.Left - DesktopIndicatorMargin - diameter;
            int circleY = contentBounds.Top + (contentBounds.Height - diameter) / 2;
            using (Brush whiteBrush = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(whiteBrush, new Rectangle(circleX, circleY, diameter, diameter));
            }
        }

        using (Brush indicatorBrush = new SolidBrush(GetDesktopIndicatorColor(item.DesktopName)))
        {
            e.Graphics.FillRectangle(indicatorBrush, indicatorRect);
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
        WindowEffects.TrySetAttribute(this.Handle, 33, 2);
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


    private void LoadWindowList(bool groupByDesktop = false)
    {
        isDesktopGroupedView = groupByDesktop;
        IntPtr foregroundWindow = GetForegroundWindow();
        var items = new List<WindowItem>();
        IntPtr? firstHandle = null;
        var winSettings = WinSettingsStore.LoadFromCache();

        EnumWindows((hWnd, lParam) =>
        {
            if (hWnd == this.Handle || hWnd == IntPtr.Zero || GetTypeWindow(hWnd) == 0 ||
                windowsToIgnore.Contains(hWnd) || !IsWindow(hWnd) || IsOtherWindow(hWnd) || !IsWindowVisible(hWnd))
                return true;

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
                    windowsPaths[hWnd] = programPath;

                string windowTitle = sb.ToString();
                var matchedSetting = winSettings.FirstOrDefault(setting => setting.Matches(windowTitle, programPath));
                if (matchedSetting is not null)
                {
                    shortcut = matchedSetting.Shortcut;
                    if (!string.IsNullOrEmpty(matchedSetting.IconPath))
                    {
                        var customIcon = Util.IconCache.GetIcon(matchedSetting.IconPath);
                        if (customIcon is not null)
                        {
                            images.Icon = customIcon;
                            images.IconIconic = customIcon;
                        }
                    }
                }
            }

            items.Add(new WindowItem
            {
                Handle = hWnd,
                TypeWindow = GetTypeWindow(hWnd),
                Title = sb.ToString(),
                DesktopName = GetWindowDesktopName(hWnd),
                RenamedTitle = renamedTitleFetchResult,
                Icon = images.Icon,
                IconIconic = images.IconIconic,
                IsIconic = IsIconic(hWnd),
                Shortcut = shortcut ?? string.Empty,
                CountReferences = countReferences
            });

            return true;
        }, IntPtr.Zero);

        UpdateWindowRelevance(items);
        List<WindowItem> orderedItems = groupByDesktop
            ? OrderWindowsByDesktop(items, foregroundWindow)
            : items.OrderBy(item => item.TypeWindow).ToList();

        MarkDesktopGroupTitles(orderedItems, groupByDesktop);
        AssignDesktopIndicatorColors(orderedItems);

        listBox1.BeginUpdate();
        listBox1.Items.Clear();
        listBox1.Items.AddRange(orderedItems.ToArray());
        listBox1.EndUpdate();
        ComputeHeightSize();
    }

    private string GetWindowDesktopName(IntPtr hWnd)
    {
        try
        {
            return GetVirtualDesktopName(hWnd);
        }
        catch
        {
            return "Desktop desconhecido";
        }
    }

    private static void UpdateWindowRelevance(IReadOnlyList<WindowItem> items)
    {
        if (items.Count < 2) return;

        const int deltaChecks = 4;
        double average = items.Skip(1).Average(item => item.CountReferences);
        for (int index = 1; index < items.Count; index++)
            items[index].HighRelevance = (items[index].CountReferences - deltaChecks) > average;
    }

    private static List<WindowItem> OrderWindowsByDesktop(IReadOnlyList<WindowItem> items, IntPtr foregroundWindow)
    {
        if (items.Count == 0) return new List<WindowItem>();

        WindowItem activeWindow = items.FirstOrDefault(item => item.Handle == foregroundWindow) ?? items[0];
        var orderedItems = new List<WindowItem> { activeWindow };
        orderedItems.AddRange(items
            .Where(item => item != activeWindow)
            .OrderBy(item => string.Equals(item.DesktopName, activeWindow.DesktopName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(item => item.DesktopName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.TypeWindow));
        return orderedItems;
    }

    private static void MarkDesktopGroupTitles(IReadOnlyList<WindowItem> items, bool groupByDesktop)
    {
        string? previousDesktopName = null;
        for (int index = 0; index < items.Count; index++)
        {
            WindowItem item = items[index];
            item.ShowsDesktopTitle = groupByDesktop && (index == 0 ||
                !string.Equals(item.DesktopName, previousDesktopName, StringComparison.OrdinalIgnoreCase));
            previousDesktopName = item.DesktopName;
        }
    }

    private void AssignDesktopIndicatorColors(IEnumerable<WindowItem> items)
    {
        desktopIndicatorColors.Clear();
        int colorIndex = 0;
        foreach (string desktopName in items.Select(item => item.DesktopName)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
        {
            desktopIndicatorColors[desktopName] = DesktopIndicatorColors[colorIndex++ % DesktopIndicatorColors.Length];
        }
    }

    private Color GetDesktopIndicatorColor(string desktopName)
    {
        return desktopIndicatorColors.TryGetValue(desktopName, out Color color)
            ? color
            : DesktopIndicatorColors[0];
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
        if (m.Msg == WM_HOTKEY)
        {
            int hotkeyId = m.WParam.ToInt32();
            if (hotkeyId == HOTKEY_ID || hotkeyId == GROUPED_HOTKEY_ID)
            {
                bool groupedHotkeyPressed = hotkeyId == GROUPED_HOTKEY_ID;
                ToggleWindowList(groupedHotkeyPressed != isDesktopGroupingHotkeyInverted);
            }
        }

        base.WndProc(ref m);
    }

    private void ToggleWindowList(bool groupByDesktop)
    {
        if (Visible && isDesktopGroupedView == groupByDesktop)
        {
            SelectNextItem();
            Activate();
            return;
        }

        PerformLayout();
        LoadWindowList(groupByDesktop);
        if (listBox1.Items.Count > 0)
            listBox1.SelectedIndex = 0;

        UpdateOneWindowModeIndicator();
        Show();
        Activate();
        BringToFront();
        Task.Delay(20).ContinueWith(t => Invoke(new Action(Invalidate)));
    }

    private void InstallKeyboardHook()
    {
        keyboardHookProc = LowLevelKeyboardHookCallback;
        using (var curProcess = Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardHookProc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private void UninstallKeyboardHook()
    {
        if (keyboardHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(keyboardHookId);
            keyboardHookId = IntPtr.Zero;
        }
    }

    private IntPtr LowLevelKeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            bool isKeyUp = !isKeyDown;

            if (vkCode == VK_LWIN || vkCode == VK_RWIN)
            {
                isWinKeyDown = isKeyDown;
            }

            // Copilot key configured as Search mode (VK_BROWSER_SEARCH, VK_LAUNCH_APP1, VK_LAUNCH_APP2)
            if (isKeyDown && (vkCode == VK_BROWSER_SEARCH || vkCode == VK_LAUNCH_APP1 || vkCode == VK_LAUNCH_APP2))
            {
                keybd_event(VK_APPS, 0, 0, UIntPtr.Zero);
                keybd_event(VK_APPS, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                return (IntPtr)1;
            }

            // Copilot key default combo (Win + Shift + F23)
            if (isKeyDown && isWinKeyDown && vkCode == VK_F23)
            {
                keybd_event(VK_APPS, 0, 0, UIntPtr.Zero);
                keybd_event(VK_APPS, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterHotKey(this.Handle, HOTKEY_ID);
        UnregisterHotKey(this.Handle, GROUPED_HOTKEY_ID);
        UninstallKeyboardHook();
        base.OnFormClosing(e);
    }

    public class WindowItem
    {
        public required IntPtr Handle { get; set; }
        public required string Title { get; set; }
        public required string DesktopName { get; set; }
        public required int TypeWindow { get; set; }
        public required int CountReferences { get; set; }
        public bool HighRelevance { get; set; } = false;
        public bool ShowsDesktopTitle { get; set; }
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

        public string ToDisplayString()
        {
            return $"{ToStringWithoutShortcut()} · {DesktopName}";
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
