using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Globalization;
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
    const int WINDOW_SEARCH_HOTKEY_ID = 9001;
    const Keys WINDOW_PICKER_KEY = Keys.Oemtilde;
    const int DesktopGroupTitleHeight = 22;
    const int PreviewUpdateDelayMilliseconds = 30;
    const int LoadingBlinkIntervalMilliseconds = 400;
    const int MouseSelectionPollIntervalMilliseconds = 16;
    const int MouseSelectionStepPixels = 25;
    const int MouseSelectionHysteresisPixels = 10;
    const uint SPI_SETCURSORS = 0x0057;
    private static readonly uint[] SystemCursorIds =
    {
        32512, // OCR_NORMAL
        32513, // OCR_IBEAM
        32514, // OCR_WAIT
        32515, // OCR_CROSS
        32516, // OCR_UP
        32642, // OCR_SIZENWSE
        32643, // OCR_SIZENESW
        32644, // OCR_SIZEWE
        32645, // OCR_SIZENS
        32646, // OCR_SIZEALL
        32648, // OCR_NO
        32649, // OCR_HAND
        32650  // OCR_APPSTARTING
    };

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
    static extern IntPtr CopyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyCursor(IntPtr hCursor);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetSystemCursor(IntPtr hCursor, uint id);

    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

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
    const int SW_SHOWNOACTIVATE = 4;
    const int SW_SHOWMINNOACTIVE = 7;

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
        return GetVirtualDesktopName(hWnd, out _);
    }

    private string GetVirtualDesktopName(IntPtr hWnd, out Guid desktopId)
    {
        if (hWnd == IntPtr.Zero)
            throw new ArgumentNullException(nameof(hWnd));

        if (!IsWindow(hWnd))
            throw new ArgumentException("O handle informado não representa uma janela válida.", nameof(hWnd));

        desktopId = GetWindowDesktopId(hWnd);

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
    private Hashtable windowsToIgnore = new Hashtable(10);
    private Dictionary<IntPtr, (Image Icon, Image IconIconic)> iconsWindows = new Dictionary<IntPtr, (Image Icon, Image IconIconic)>(400);
    private Hashtable windowsRenames = new Hashtable();
    private Dictionary<IntPtr, string> windowsShortcuts = new Dictionary<IntPtr, string>();
    private Dictionary<(IntPtr, IntPtr), int> windowsReferences = new Dictionary<(IntPtr, IntPtr), int>();
    private Dictionary<IntPtr, string> windowsPaths = new Dictionary<IntPtr, string>();
    private char? quickShortcutCycleKey;
    private readonly HashSet<IntPtr> quickShortcutCycleVisitedHandles = new();
    private readonly object virtualDesktopCacheSync = new object();
    private Dictionary<Guid, string> virtualDesktopNames = new Dictionary<Guid, string>();
    private bool isVirtualDesktopCacheLoaded;
    private bool isPreviewVisible = true;
    private bool isDesktopGroupedView;
    private bool isCurrentDesktopOnlyView;
    private bool isDesktopGroupingHotkeyInverted;
    private bool isWindowSearchMode;
    private readonly List<WindowItem> windowSearchItems = new();
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
    private readonly SolidBrush listBackgroundBrush;
    private readonly SolidBrush selectedItemBrush;
    private readonly SolidBrush selectedIconicItemBrush;
    private readonly Pen selectedItemBorderPen;
    private readonly Pen selectedIconicItemBorderPen;
    private readonly System.Windows.Forms.Timer previewUpdateTimer;
    private readonly System.Windows.Forms.Timer mouseSelectionTimer;
    private readonly System.Windows.Forms.Timer loadingBlinkTimer;
    private Rectangle previewRectangle;
    private IntPtr previewHandle = IntPtr.Zero;
    private bool isOneWindowMode = false;
    private bool isMouseSelectionMode = false;
    private bool isApplyingMouseSelection = false;
    private bool areSystemCursorsOverridden = false;
    private int mouseSelectionOriginY;
    private int mouseSelectionOriginIndex;
    private int mouseSelectionStep;
    private bool isLoadingTextDimmed;
    private bool isFormClosing;

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

        Color selectedIconicColor = IncrementColor(TertiaryColor, multiplierOpacityDecrement);
        listBackgroundBrush = new SolidBrush(accentColor);
        selectedItemBrush = new SolidBrush(TertiaryColor);
        selectedIconicItemBrush = new SolidBrush(selectedIconicColor);
        selectedItemBorderPen = CreateSelectedItemBorderPen(TertiaryColor);
        selectedIconicItemBorderPen = CreateSelectedItemBorderPen(selectedIconicColor);
        previewUpdateTimer = new System.Windows.Forms.Timer { Interval = PreviewUpdateDelayMilliseconds };
        previewUpdateTimer.Tick += this.OnPreviewUpdateTimerTick;
        mouseSelectionTimer = new System.Windows.Forms.Timer { Interval = MouseSelectionPollIntervalMilliseconds };
        mouseSelectionTimer.Tick += this.OnMouseSelectionTimerTick;
        loadingBlinkTimer = new System.Windows.Forms.Timer { Interval = LoadingBlinkIntervalMilliseconds };
        loadingBlinkTimer.Tick += this.OnLoadingBlinkTimerTick;

        listBox1.DrawMode = DrawMode.OwnerDrawFixed;
        listBox1.MeasureItem += this.OnListBox1MeasureItem;
        listBox1.DrawItem += this.OnListBox1DrawItem;
        listBox1.SelectedIndexChanged += this.OnListBoxSelectedIndexChanged;
        listBox1.KeyPress += this.OnWindowSearchListKeyPress;
        textBoxWindowSearch.TextChanged += this.OnWindowSearchTextChanged;
        textBoxWindowSearch.KeyDown += this.OnWindowSearchKeyDown;
        pnLists.Resize += this.OnWindowListPanelResize;

        ComputeIgnoreWindows();
        LoadWindowList();
        // Preserve the existing physical key mapping used by both window-picker shortcuts.
        RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, WINDOW_PICKER_KEY);
        RegisterHotKey(this.Handle, WINDOW_SEARCH_HOTKEY_ID, MOD_ALT | MOD_SHIFT, WINDOW_PICKER_KEY);
        InstallKeyboardHook();
        //this.KeyPreview = true; // importante para o formulário capturar teclas antes dos controles
        //this.KeyDown += this.MainForm_KeyDown;


        this.Resize += this.MainForm_Resize;
        this.Load += this.MainForm_Load;
        this.Paint += this.MainForm_Paint;
        this.Deactivate += this.OnDeactivate;
        this.VisibleChanged += this.OnMainFormVisibleChanged;

        listBox1.DoubleClick += (s, e) =>
        {
            if (listBox1.SelectedItem is WindowItem item)
            {
                ResetQuickShortcutCycleIfDifferent(item);
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

    private void OpenWindowSearch()
    {
        isWindowSearchMode = true;
        SetWindowSearchLayout(true);
        PerformLayout();

        // Enumerate once when the mode opens. Typing only filters this in-memory snapshot.
        LoadWindowList(false, false);
        windowSearchItems.Clear();
        windowSearchItems.AddRange(listBox1.Items.Cast<WindowItem>());

        if (textBoxWindowSearch.TextLength == 0)
            ApplyWindowSearch();
        else
            textBoxWindowSearch.Clear();
        UpdateOneWindowModeIndicator();

        if (!Visible)
            Show();

        Activate();
        BringToFront();
        textBoxWindowSearch.Focus();
        Task.Delay(20).ContinueWith(t => Invoke(new Action(Invalidate)));
    }

    private void ExitWindowSearchMode()
    {
        if (!isWindowSearchMode) return;

        isWindowSearchMode = false;
        SetWindowSearchLayout(false);
        textBoxWindowSearch.Clear();
        windowSearchItems.Clear();
    }

    private void SetWindowSearchLayout(bool enabled)
    {
        textBoxWindowSearch.Visible = enabled;
        if (!enabled)
        {
            listBox1.Dock = DockStyle.Fill;
            return;
        }

        listBox1.Dock = DockStyle.None;
        UpdateWindowSearchLayout();
        textBoxWindowSearch.BringToFront();
    }

    private void OnWindowListPanelResize(object sender, EventArgs e)
    {
        if (isWindowSearchMode)
            UpdateWindowSearchLayout();
    }

    private void UpdateWindowSearchLayout()
    {
        int searchHeight = textBoxWindowSearch.PreferredHeight;
        textBoxWindowSearch.SetBounds(0, 0, pnLists.ClientSize.Width, searchHeight);
        listBox1.SetBounds(
            0,
            searchHeight,
            pnLists.ClientSize.Width,
            Math.Max(0, pnLists.ClientSize.Height - searchHeight));
    }

    private void OnWindowSearchTextChanged(object sender, EventArgs e)
    {
        if (isWindowSearchMode)
            ApplyWindowSearch();
    }

    private void ApplyWindowSearch()
    {
        string query = textBoxWindowSearch.Text.Trim();
        WindowItem[] matches = query.Length == 0
            ? Array.Empty<WindowItem>()
            : windowSearchItems
                .Select((item, index) => new
                {
                    Item = item,
                    OriginalIndex = index,
                    Score = GetWindowSearchScore(item, query)
                })
                .Where(match => match.Score >= 0)
                .OrderByDescending(match => match.Score)
                .ThenBy(match => match.OriginalIndex)
                .Select(match => match.Item)
                .ToArray();

        listBox1.BeginUpdate();
        try
        {
            listBox1.Items.Clear();
            if (matches.Length > 0)
            {
                listBox1.Items.AddRange(matches);
                listBox1.SelectedIndex = 0;
            }
        }
        finally
        {
            listBox1.EndUpdate();
        }

        if (matches.Length == 0)
        {
            previewUpdateTimer.Stop();
            CloseCurrentPreview();
            lbPreview.Text = "No selected window";
        }

        ComputeHeightSize();
        ApplyRoundedRegion(20);
        listBox1.Invalidate();
    }

    private static int GetWindowSearchScore(WindowItem item, string query)
    {
        CompareInfo compareInfo = CultureInfo.CurrentCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        int score = -1;

        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            score = Math.Max(score, ScoreTextMatch(
                compareInfo,
                item.Shortcut,
                query,
                options,
                exactScore: 1_000_000,
                prefixScore: 950_000,
                wordPrefixScore: 700_000,
                containsScore: 500_000));
        }

        string title = item.ToStringWithoutShortcut();
        score = Math.Max(score, ScoreTextMatch(
            compareInfo,
            title,
            query,
            options,
            exactScore: 900_000,
            prefixScore: 850_000,
            wordPrefixScore: 750_000,
            containsScore: 400_000));

        // Also match the exact textual composition rendered by the existing list.
        score = Math.Max(score, ScoreTextMatch(
            compareInfo,
            item.ToString(),
            query,
            options,
            exactScore: 925_000,
            prefixScore: 875_000,
            wordPrefixScore: 725_000,
            containsScore: 300_000));

        return score;
    }

    private static int ScoreTextMatch(
        CompareInfo compareInfo,
        string candidate,
        string query,
        CompareOptions options,
        int exactScore,
        int prefixScore,
        int wordPrefixScore,
        int containsScore)
    {
        if (compareInfo.Compare(candidate, query, options) == 0)
            return exactScore;

        if (compareInfo.IsPrefix(candidate, query, options))
            return prefixScore - Math.Min(candidate.Length - query.Length, 1_000);

        int wordPrefixIndex = FindWordPrefix(compareInfo, candidate, query, options);
        if (wordPrefixIndex >= 0)
            return wordPrefixScore - Math.Min(wordPrefixIndex, 1_000);

        int containsIndex = compareInfo.IndexOf(candidate, query, options);
        return containsIndex >= 0
            ? containsScore - Math.Min(containsIndex, 1_000)
            : -1;
    }

    private static int FindWordPrefix(
        CompareInfo compareInfo,
        string candidate,
        string query,
        CompareOptions options)
    {
        int startIndex = 0;
        while (startIndex < candidate.Length)
        {
            int matchIndex = compareInfo.IndexOf(candidate, query, startIndex, options);
            if (matchIndex < 0) return -1;
            if (matchIndex == 0 || !char.IsLetterOrDigit(candidate[matchIndex - 1]))
                return matchIndex;
            startIndex = matchIndex + 1;
        }

        return -1;
    }

    private void OnWindowSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (!isWindowSearchMode) return;

        if (e.KeyCode == Keys.Down && listBox1.Items.Count > 0)
        {
            listBox1.SelectedIndex = Math.Min(listBox1.SelectedIndex + 1, listBox1.Items.Count - 1);
        }
        else if (e.KeyCode == Keys.Up && listBox1.Items.Count > 0)
        {
            listBox1.SelectedIndex = Math.Max(listBox1.SelectedIndex - 1, 0);
        }
        else if (e.KeyCode == Keys.Enter && listBox1.SelectedItem is WindowItem selectedWindow)
        {
            ActivateWindowSearchSelection(selectedWindow);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Hide();
        }
        else
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void OnWindowSearchListKeyPress(object sender, KeyPressEventArgs e)
    {
        if (!isWindowSearchMode) return;

        if (e.KeyChar == '\b')
        {
            if (textBoxWindowSearch.TextLength > 0)
                textBoxWindowSearch.Text = textBoxWindowSearch.Text.Substring(0, textBoxWindowSearch.TextLength - 1);
        }
        else if (!char.IsControl(e.KeyChar))
        {
            textBoxWindowSearch.AppendText(e.KeyChar.ToString());
        }
        else
        {
            return;
        }

        textBoxWindowSearch.SelectionStart = textBoxWindowSearch.TextLength;
        textBoxWindowSearch.Focus();
        e.Handled = true;
    }

    private void ActivateWindowSearchSelection(WindowItem selectedWindow)
    {
        ResetQuickShortcutCycleIfDifferent(selectedWindow);
        if (windowSearchItems.Count > 0)
            IncrementCount(windowSearchItems[0], selectedWindow);
        FocusWindow(selectedWindow);
        Hide();
    }

    private static Pen CreateSelectedItemBorderPen(Color selectedColor)
    {
        var pen = new Pen(IncrementColor(selectedColor, 2f), 1.5f)
        {
            DashStyle = DashStyle.Dot
        };
        return pen;
    }

    private void OnListBoxSelectedIndexChanged(object sender, EventArgs e)
    {
        previewUpdateTimer.Stop();

        if (!isWindowSearchMode && isMouseSelectionMode && !isApplyingMouseSelection && listBox1.SelectedIndex >= 0)
            ResetMouseSelectionAnchor();

        bool hasActiveMode = !isWindowSearchMode &&
            (isDesktopGroupingHotkeyInverted || isOneWindowMode || isMouseSelectionMode);
        if (hasActiveMode)
            UpdateOneWindowModeIndicator();

        if (!isPreviewVisible)
        {
            CloseCurrentPreview();
            return;
        }

        if (listBox1.SelectedItem is not WindowItem selectedWindow)
        {
            CloseCurrentPreview();
            lbPreview.Text = "No selected Window";
            pnPreview.BackColor = AccentColor;
            return;
        }

        lbPreview.Text = string.Empty;
        if (!hasActiveMode)
            lbSelectedWindow.Text = selectedWindow.Title;

        // Let the selection repaint before doing synchronous DWM thumbnail work.
        previewUpdateTimer.Start();
    }

    private void OnPreviewUpdateTimerTick(object sender, EventArgs e)
    {
        previewUpdateTimer.Stop();

        if (!Visible || !isPreviewVisible || listBox1.SelectedItem is not WindowItem selectedWindow)
            return;

        CloseCurrentPreview();
        Rectangle previewRect = GetProportionalRectangle(selectedWindow.Handle, previewRectangle);
        previewHandle = WindowPreview.ShowPreview(this.Handle, selectedWindow.Handle, previewRect, 245);
    }

    private void CloseCurrentPreview()
    {
        if (previewHandle == IntPtr.Zero) return;

        WindowPreview.ClosePreview(previewHandle);
        previewHandle = IntPtr.Zero;
    }

    private int fullWidth;

    private void TogglePreviewPanel()
    {
        isPreviewVisible = !isPreviewVisible;

        if (!isPreviewVisible)
        {
            previewUpdateTimer.Stop();
            CloseCurrentPreview();
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

            if (listBox1.SelectedItem is WindowItem)
            {
                previewUpdateTimer.Stop();
                previewUpdateTimer.Start();
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
        previewUpdateTimer.Stop();
        CloseCurrentPreview();
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
        if (isWindowSearchMode)
        {
            if (e.KeyCode == Keys.Enter && listBox1.SelectedItem is WindowItem selectedWindow)
            {
                ActivateWindowSearchSelection(selectedWindow);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Hide();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            return;
        }

        if (TryHandleDesktopGroupActivation(e)) return;
        if (TryHandleKeybindForSingleMatchWindow(e)) return;
        if (TryHandleAppExit(e)) return;
        if (TryHandleWindowToggle(e)) return;
        if (TryHandleWindowActivation(e)) return;
        if (TryHandleWindowRemoval(e)) return;
        if (TryHandleRename(e)) return;
        if (TryHandleViewModes(e)) return;
    }

    private bool TryHandleDesktopGroupActivation(KeyEventArgs e)
    {
        if (!isDesktopGroupedView || !e.Alt || e.Shift || e.Control)
            return false;

        var items = listBox1.Items.Cast<WindowItem>().ToList();
        WindowItem? target;
        char shortcutKey;

        if (IsQuickShortcutInput(e.KeyCode))
        {
            shortcutKey = char.ToLowerInvariant((char)e.KeyValue);
            target = FindQuickShortcutTarget(items, shortcutKey);
            if (target is null)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return true;
            }
        }
        else if (e.KeyCode == Keys.Enter && listBox1.SelectedItem is WindowItem selectedWindow)
        {
            target = selectedWindow;
            ResetQuickShortcutCycleIfDifferent(target);
            shortcutKey = GetQuickShortcutKey(target);
        }
        else
        {
            return false;
        }

        ActivateMatchingWindowsOnDesktop(items, target, shortcutKey);
        e.Handled = true;
        e.SuppressKeyPress = true;
        return true;
    }

    private bool TryHandleKeybindForSingleMatchWindow(KeyEventArgs e)
    {
        if (e.Shift || e.Control || e.Alt || !IsQuickShortcutInput(e.KeyCode))
            return false;

        char input = char.ToLowerInvariant((char)e.KeyValue);
        var items = listBox1.Items.Cast<WindowItem>().ToList();
        WindowItem? item = FindQuickShortcutTarget(items, input);
        if (item is null) return false;

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

    private static bool IsQuickShortcutInput(Keys keyCode)
    {
        return (keyCode >= Keys.A && keyCode <= Keys.Z) ||
               (keyCode >= Keys.D0 && keyCode <= Keys.D9);
    }

    private WindowItem? FindQuickShortcutTarget(IReadOnlyList<WindowItem> items, char input)
    {
        bool MatchKey(WindowItem window) => GetQuickShortcutKey(window) == input;

        int selectedIndex = listBox1.SelectedIndex;
        WindowItem? selectedItem = selectedIndex >= 0 && selectedIndex < items.Count
            ? items[selectedIndex]
            : null;
        bool isRepeatedQuickShortcut = quickShortcutCycleKey == input;

        WindowItem? target = isRepeatedQuickShortcut
            ? FindNextMatchingWindow(items, selectedIndex, candidate =>
                MatchKey(candidate) && !quickShortcutCycleVisitedHandles.Contains(candidate.Handle))
            : null;

        if (target is null)
        {
            target = FindNextMatchingWindow(items, selectedIndex, MatchKey);
            if (target is null) return null;

            BeginQuickShortcutCycle(input, selectedItem, target);
        }
        else
        {
            quickShortcutCycleVisitedHandles.Add(target.Handle);
        }

        return target;
    }

    private void ActivateMatchingWindowsOnDesktop(
        IReadOnlyList<WindowItem> items,
        WindowItem target,
        char shortcutKey)
    {
        List<WindowItem> desktopWindows = items
            .Where(window => window.DesktopId == target.DesktopId)
            .ToList();
        List<WindowItem> matchingWindows = desktopWindows
            .Where(window => GetQuickShortcutKey(window) == shortcutKey)
            .ToList();
        var matchingHandles = new HashSet<IntPtr>(matchingWindows.Select(window => window.Handle));

        Hide();

        foreach (WindowItem window in desktopWindows)
        {
            if (!matchingHandles.Contains(window.Handle) && IsWindow(window.Handle) && !IsIconic(window.Handle))
                ShowWindow(window.Handle, SW_SHOWMINNOACTIVE);
        }

        foreach (WindowItem window in matchingWindows)
        {
            if (IsWindow(window.Handle) && IsIconic(window.Handle))
                ShowWindow(window.Handle, SW_SHOWNOACTIVATE);
        }

        if (IsWindow(target.Handle))
            FocusWindow(target);

        if (items.Count > 0)
            IncrementCount(items[0], target);
    }

    private static WindowItem? FindNextMatchingWindow(
        IReadOnlyList<WindowItem> items,
        int selectedIndex,
        Func<WindowItem, bool> matches)
    {
        if (items.Count == 0) return null;
        if (selectedIndex < 0 || selectedIndex >= items.Count)
            return items.FirstOrDefault(matches);

        for (int offset = 1; offset < items.Count; offset++)
        {
            WindowItem candidate = items[(selectedIndex + offset) % items.Count];
            if (matches(candidate)) return candidate;
        }

        return matches(items[selectedIndex]) ? items[selectedIndex] : null;
    }

    private void BeginQuickShortcutCycle(char input, WindowItem? selectedItem, WindowItem target)
    {
        quickShortcutCycleKey = input;
        quickShortcutCycleVisitedHandles.Clear();

        if (selectedItem is not null)
            quickShortcutCycleVisitedHandles.Add(selectedItem.Handle);

        quickShortcutCycleVisitedHandles.Add(target.Handle);
    }

    private static char GetQuickShortcutKey(WindowItem window)
    {
        return char.ToLowerInvariant(!string.IsNullOrEmpty(window.Shortcut)
            ? window.Shortcut[0]
            : window.ToStringWithoutShortcut()[0]);
    }

    private void ResetQuickShortcutCycleIfDifferent(WindowItem window)
    {
        if (quickShortcutCycleKey is null || quickShortcutCycleKey == GetQuickShortcutKey(window))
            return;

        quickShortcutCycleKey = null;
        quickShortcutCycleVisitedHandles.Clear();
    }

    private bool TryHandleAppExit(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && e.Shift)
        {
            e.Handled = true;
            MessageBox.Show("Program closed.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            RestoreSystemCursors();
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
            ResetQuickShortcutCycleIfDifferent(itemInforceSaltsIncrements);
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
            ResetQuickShortcutCycleIfDifferent(windowToFocusWithControl);
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
            ResetQuickShortcutCycleIfDifferent(itemSpace);
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
            ResetQuickShortcutCycleIfDifferent(item);
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

            this.LoadWindowList(isDesktopGroupedView, isCurrentDesktopOnlyView);
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
            LoadWindowList(isDesktopGroupingHotkeyInverted, isCurrentDesktopOnlyView);
            if (listBox1.Items.Count > 0)
                listBox1.SelectedIndex = 0;
            UpdateOneWindowModeIndicator();
            return true;
        }
        if (e.KeyCode == Keys.F9)
        {
            ToggleMouseSelectionMode();
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

    private void ToggleMouseSelectionMode()
    {
        if (isMouseSelectionMode)
        {
            DisableMouseSelectionMode();
            return;
        }

        if (listBox1.SelectedIndex < 0) return;

        isMouseSelectionMode = true;
        ResetMouseSelectionAnchor();
        ApplyMouseSelectionCursor();
        mouseSelectionTimer.Start();
        UpdateOneWindowModeIndicator();
    }

    private void DisableMouseSelectionMode()
    {
        mouseSelectionTimer.Stop();
        if (!isMouseSelectionMode) return;

        isMouseSelectionMode = false;
        mouseSelectionStep = 0;
        RestoreSystemCursors();
        UpdateOneWindowModeIndicator();
    }

    private void ResetMouseSelectionAnchor()
    {
        if (listBox1.SelectedIndex < 0) return;

        mouseSelectionOriginY = Cursor.Position.Y;
        mouseSelectionOriginIndex = listBox1.SelectedIndex;
        mouseSelectionStep = 0;
    }

    private void ApplyMouseSelectionCursor()
    {
        if (areSystemCursorsOverridden) return;

        bool cursorChanged = false;
        foreach (uint cursorId in SystemCursorIds)
        {
            IntPtr cursorCopy = CopyIcon(Cursors.SizeNS.Handle);
            if (cursorCopy == IntPtr.Zero) continue;

            if (SetSystemCursor(cursorCopy, cursorId))
                cursorChanged = true;
            else
                DestroyCursor(cursorCopy);
        }

        areSystemCursorsOverridden = cursorChanged;
        Cursor.Current = Cursors.SizeNS;
    }

    private void RestoreSystemCursors()
    {
        if (areSystemCursorsOverridden)
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);

        areSystemCursorsOverridden = false;
        Cursor.Current = Cursors.Default;
    }

    private void OnMouseSelectionTimerTick(object sender, EventArgs e)
    {
        if (!Visible)
        {
            mouseSelectionTimer.Stop();
            RestoreSystemCursors();
            return;
        }
        if (!isMouseSelectionMode || listBox1.Items.Count == 0) return;

        ApplyMouseSelectionCursor();
        int deltaY = Cursor.Position.Y - mouseSelectionOriginY;
        int nextStep = deltaY / MouseSelectionStepPixels;

        if (nextStep > mouseSelectionStep && mouseSelectionStep < 0 &&
            deltaY < (mouseSelectionStep * MouseSelectionStepPixels) + MouseSelectionHysteresisPixels)
            nextStep = mouseSelectionStep;
        else if (nextStep < mouseSelectionStep && mouseSelectionStep > 0 &&
                 deltaY > (mouseSelectionStep * MouseSelectionStepPixels) - MouseSelectionHysteresisPixels)
            nextStep = mouseSelectionStep;

        if (nextStep == mouseSelectionStep) return;

        mouseSelectionStep = nextStep;
        int targetIndex = Math.Max(0, Math.Min(listBox1.Items.Count - 1, mouseSelectionOriginIndex + mouseSelectionStep));
        if (targetIndex == listBox1.SelectedIndex) return;

        isApplyingMouseSelection = true;
        try
        {
            listBox1.SelectedIndex = targetIndex;
        }
        finally
        {
            isApplyingMouseSelection = false;
        }
    }

    private void OnMainFormVisibleChanged(object sender, EventArgs e)
    {
        if (!Visible)
        {
            loadingBlinkTimer.Stop();
            isLoadingTextDimmed = false;
            mouseSelectionTimer.Stop();
            RestoreSystemCursors();
            ExitWindowSearchMode();
            return;
        }

        loadingBlinkTimer.Start();
        QueueLoadingStateScan();

        if (isWindowSearchMode || !isMouseSelectionMode || listBox1.SelectedIndex < 0) return;

        ResetMouseSelectionAnchor();
        ApplyMouseSelectionCursor();
        mouseSelectionTimer.Start();
        UpdateOneWindowModeIndicator();
    }

    private void OnLoadingBlinkTimerTick(object sender, EventArgs e)
    {
        if (!Visible || isFormClosing)
            return;

        isLoadingTextDimmed = !isLoadingTextDimmed;
        InvalidateLoadingWindowTitles();
        QueueLoadingStateScan();
    }

    private async void QueueLoadingStateScan()
    {
        if (!Visible || isFormClosing)
            return;

        IntPtr[] handles = listBox1.Items.Cast<WindowItem>()
            .Select(item => item.Handle)
            .Distinct()
            .ToArray();
        Dictionary<IntPtr, bool> loadingStates = await Task.Run(() => handles.ToDictionary(
            handle => handle,
            WindowLoadingDetector.IsLoading));

        if (!Visible || isFormClosing)
            return;

        for (int index = 0; index < listBox1.Items.Count; index++)
        {
            WindowItem item = (WindowItem)listBox1.Items[index];
            if (!loadingStates.TryGetValue(item.Handle, out bool isLoading) || item.IsLoading == isLoading)
                continue;

            item.IsLoading = isLoading;
            InvalidateWindowTitle(index, item);
        }
    }

    private void InvalidateLoadingWindowTitles()
    {
        for (int index = 0; index < listBox1.Items.Count; index++)
        {
            WindowItem item = (WindowItem)listBox1.Items[index];
            if (item.IsLoading)
                InvalidateWindowTitle(index, item);
        }
    }

    private void InvalidateWindowTitle(int index, WindowItem item)
    {
        Rectangle itemBounds = listBox1.GetItemRectangle(index);
        int titleHeight = item.ShowsDesktopTitle ? DesktopGroupTitleHeight : 0;
        Rectangle contentBounds = new Rectangle(
            itemBounds.X,
            itemBounds.Y + titleHeight,
            itemBounds.Width,
            itemBounds.Height - titleHeight);
        Rectangle textBounds = GetWindowTextBounds(contentBounds, item);
        if (!isDesktopGroupedView)
            textBounds.Width = Math.Min(textBounds.Width, item.GetTitleTextWidth(listBox1.Font) + 2);

        if (textBounds.IntersectsWith(listBox1.ClientRectangle))
            listBox1.Invalidate(textBounds, false);
    }

    private void ComputeLastSelectedIndex(int i)
    {
        if (listBox1.Items.Count == 0) return;

        listBox1.SelectedIndex = i >= listBox1.Items.Count ? listBox1.Items.Count - 1 : i;
    }

    private void UpdateOneWindowModeIndicator()
    {
        if (!isWindowSearchMode &&
            (isDesktopGroupingHotkeyInverted || isOneWindowMode || isMouseSelectionMode))
        {
            var activeModes = new List<string>();
            if (isDesktopGroupingHotkeyInverted)
                activeModes.Add("[F8] Desktop Grouping Mode");
            if (isMouseSelectionMode)
                activeModes.Add("[F9] Mouse Selection Mode");
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

            int searchHeight = textBoxWindowSearch.Visible ? textBoxWindowSearch.Height : 0;
            int height = itemsHeight + searchHeight + this.Padding.Vertical;
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

        Color foreColor = selected ? Color.White : lb.ForeColor;
        Color shortcutColor = ShortcutColor;
        if (item.IsIconic)
        {
            foreColor = IncrementColor(foreColor, multiplierOpacityDecrement);
            shortcutColor = IncrementColor(shortcutColor, multiplierOpacityDecrement);
        }

        Brush contentBrush = selected
            ? (item.IsIconic ? selectedIconicItemBrush : selectedItemBrush)
            : listBackgroundBrush;

        if (titleHeight > 0)
        {
            e.Graphics.FillRectangle(listBackgroundBrush, e.Bounds);
            if (selected)
                e.Graphics.FillRectangle(contentBrush, contentBounds);
        }
        else
        {
            e.Graphics.FillRectangle(contentBrush, contentBounds);
        }

        if (item.ShowsDesktopTitle)
        {
            TextRenderer.DrawText(e.Graphics, item.DesktopName, desktopTitleFont, titleBounds,
                lb.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        int iconSize = contentBounds.Height - 4;
        Rectangle iconRect = new Rectangle(contentBounds.X + 2, contentBounds.Y + 2, iconSize, iconSize);
        e.Graphics.DrawImage(item.OutIcon, iconRect);

        int textX = iconRect.Right + 4;
        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            Rectangle shortRect = new Rectangle(textX, contentBounds.Y, 15, contentBounds.Height);
            TextRenderer.DrawText(e.Graphics, item.Shortcut, shortcutFont, shortRect, shortcutColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        Rectangle textRect = GetWindowTextBounds(contentBounds, item);
        DrawWindowText(e.Graphics, lb, item, textRect, foreColor);

        if (selected)
        {
            Pen borderPen = item.IsIconic ? selectedIconicItemBorderPen : selectedItemBorderPen;
            Rectangle borderRect = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width - 1, contentBounds.Height - 1);
            e.Graphics.DrawRectangle(borderPen, borderRect);
        }

        if (item.HighRelevance)
        {
            SmoothingMode previousSmoothingMode = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            const int diameter = 6;
            int circleX = contentBounds.Right - 4 - diameter;
            int circleY = contentBounds.Top + (contentBounds.Height - diameter) / 2;
            e.Graphics.FillEllipse(Brushes.White, new Rectangle(circleX, circleY, diameter, diameter));
            e.Graphics.SmoothingMode = previousSmoothingMode;
        }
    }

    private static Rectangle GetWindowTextBounds(Rectangle contentBounds, WindowItem item)
    {
        int iconSize = contentBounds.Height - 4;
        int textX = contentBounds.X + 2 + iconSize + 4;
        if (!string.IsNullOrEmpty(item.Shortcut))
            textX += 12;

        int relevanceWidth = item.HighRelevance ? 12 : 0;
        int textRight = contentBounds.Right - 4 - relevanceWidth;
        return new Rectangle(textX, contentBounds.Y, Math.Max(0, textRight - textX), contentBounds.Height);
    }

    private void DrawWindowText(Graphics graphics, ListBox listBox, WindowItem item, Rectangle textBounds, Color foreColor)
    {
        const TextFormatFlags textFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        string title = item.ToStringWithoutShortcut();
        Color titleColor = item.IsLoading && isLoadingTextDimmed
            ? IncrementColor(foreColor, multiplierOpacityDecrement - 0.25f)
            : foreColor;

        if (isDesktopGroupedView)
        {
            TextRenderer.DrawText(graphics, title, listBox.Font, textBounds, titleColor, textFlags);
            return;
        }

        int titleWidth = Math.Min(textBounds.Width, item.GetTitleTextWidth(listBox.Font) + 1);
        if (titleWidth >= textBounds.Width)
        {
            TextRenderer.DrawText(graphics, title, listBox.Font, textBounds, titleColor, textFlags);
            return;
        }

        Rectangle titleBounds = new Rectangle(textBounds.X, textBounds.Y, titleWidth, textBounds.Height);
        TextRenderer.DrawText(graphics, title, listBox.Font, titleBounds, titleColor, textFlags);

        Rectangle desktopBounds = new Rectangle(
            titleBounds.Right,
            textBounds.Y,
            textBounds.Right - titleBounds.Right,
            textBounds.Height);
        TextRenderer.DrawText(graphics, $" · {item.DesktopName}", listBox.Font, desktopBounds, foreColor, textFlags);
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


    private void LoadWindowList(bool groupByDesktop = false, bool currentDesktopOnly = false)
    {
        isDesktopGroupedView = groupByDesktop;
        isCurrentDesktopOnlyView = currentDesktopOnly;
        DrawMode requestedDrawMode = groupByDesktop ? DrawMode.OwnerDrawVariable : DrawMode.OwnerDrawFixed;
        if (listBox1.DrawMode != requestedDrawMode)
            listBox1.DrawMode = requestedDrawMode;

        IntPtr foregroundWindow = GetForegroundWindow();
        Guid? desktopFilter = null;
        if (currentDesktopOnly && TryGetWindowDesktop(foregroundWindow, out Guid foregroundDesktopId, out _))
            desktopFilter = foregroundDesktopId;

        var items = new List<WindowItem>();
        IntPtr? firstHandle = null;
        var winSettings = WinSettingsStore.LoadFromCache();

        EnumWindows((hWnd, lParam) =>
        {
            if (hWnd == this.Handle || hWnd == IntPtr.Zero || GetTypeWindow(hWnd) == 0 ||
                windowsToIgnore.Contains(hWnd) || !IsWindow(hWnd) || IsOtherWindow(hWnd) || !IsWindowVisible(hWnd))
                return true;

            int length = GetWindowTextLength(hWnd);
            if (length == 0) return true;

            if (!TryGetWindowDesktop(hWnd, out Guid desktopId, out string desktopName))
                return true;

            if (currentDesktopOnly)
            {
                if (desktopFilter is null)
                    desktopFilter = desktopId;
                else if (desktopId != desktopFilter.Value)
                    return true;
            }

            int countReferences = int.MaxValue;
            if (firstHandle == null) firstHandle = hWnd;
            else countReferences = CountWindowReferences(firstHandle.Value, hWnd);

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
                DesktopId = desktopId,
                DesktopName = desktopName,
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

        listBox1.BeginUpdate();
        listBox1.Items.Clear();
        listBox1.Items.AddRange(orderedItems.ToArray());
        listBox1.EndUpdate();
        ComputeHeightSize();
    }

    private bool TryGetWindowDesktop(IntPtr hWnd, out Guid desktopId, out string desktopName)
    {
        desktopId = Guid.Empty;
        try
        {
            desktopName = GetVirtualDesktopName(hWnd, out desktopId);
            return true;
        }
        catch
        {
            desktopName = string.Empty;
            return false;
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

    private void buttonFocus_Click(object sender, EventArgs e)
    {
        if (listBox1.SelectedItem is WindowItem item)
        {
            ResetQuickShortcutCycleIfDifferent(item);
            SetForegroundWindow(item.Handle);
        }
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
            if (hotkeyId == HOTKEY_ID)
            {
                ToggleWindowList(isDesktopGroupingHotkeyInverted, false);
                if (isMouseSelectionMode)
                    PositionCursorForMouseSelection();
            }
            else if (hotkeyId == WINDOW_SEARCH_HOTKEY_ID)
            {
                OpenWindowSearch();
            }
        }

        base.WndProc(ref m);
    }

    private void PositionCursorForMouseSelection()
    {
        Rectangle screenBounds = Screen.FromControl(this).Bounds;
        Cursor.Position = new Point(screenBounds.Left + (screenBounds.Width / 2), Top);
        ApplyMouseSelectionCursor();
        ResetMouseSelectionAnchor();
    }

    private void ToggleWindowList(bool groupByDesktop, bool currentDesktopOnly)
    {
        if (Visible && !isWindowSearchMode && isDesktopGroupedView == groupByDesktop && isCurrentDesktopOnlyView == currentDesktopOnly)
        {
            SelectNextItem();
            Activate();
            listBox1.Focus();
            return;
        }

        ExitWindowSearchMode();
        PerformLayout();
        LoadWindowList(groupByDesktop, currentDesktopOnly);
        if (listBox1.Items.Count > 0)
            listBox1.SelectedIndex = 0;

        UpdateOneWindowModeIndicator();
        Show();
        Activate();
        BringToFront();
        listBox1.Focus();
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

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        isFormClosing = true;
        previewUpdateTimer.Stop();
        mouseSelectionTimer.Stop();
        loadingBlinkTimer.Stop();
        RestoreSystemCursors();
        CloseCurrentPreview();
        UnregisterHotKey(this.Handle, HOTKEY_ID);
        UnregisterHotKey(this.Handle, WINDOW_SEARCH_HOTKEY_ID);
        UninstallKeyboardHook();

        previewUpdateTimer.Dispose();
        mouseSelectionTimer.Dispose();
        loadingBlinkTimer.Dispose();
        listBackgroundBrush.Dispose();
        selectedItemBrush.Dispose();
        selectedIconicItemBrush.Dispose();
        selectedItemBorderPen.Dispose();
        selectedIconicItemBorderPen.Dispose();
        shortcutFont.Dispose();
        desktopTitleFont.Dispose();

        base.OnFormClosed(e);
    }

    public class WindowItem
    {
        private Font? measuredTitleFont;
        private int measuredTitleWidth;

        public required IntPtr Handle { get; set; }
        public required string Title
        {
            get => field;
            set
            {
                field = value;
                measuredTitleFont = null;
            }
        }
        public required Guid DesktopId { get; set; }
        public required string DesktopName { get; set; }
        public required int TypeWindow { get; set; }
        public required int CountReferences { get; set; }
        public bool HighRelevance { get; set; } = false;
        public bool ShowsDesktopTitle { get; set; }
        public bool IsLoading { get; set; }
        public required string? RenamedTitle
        {
            get => field;
            set
            {
                field = value;
                measuredTitleFont = null;
            }
        }
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

        public int GetTitleTextWidth(Font font)
        {
            if (ReferenceEquals(measuredTitleFont, font))
                return measuredTitleWidth;

            measuredTitleFont = font;
            measuredTitleWidth = TextRenderer.MeasureText(
                ToStringWithoutShortcut(),
                font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding).Width;
            return measuredTitleWidth;
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
