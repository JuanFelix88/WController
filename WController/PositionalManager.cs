using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WController;

internal sealed class PositionalManager : IDisposable
{
    private readonly Form dispatcher;
    private readonly Action beforeStart;
    private readonly HashSet<Keys> suppressedKeys = new();
    private readonly Timer refreshTimer;
    private readonly IVirtualDesktopManager? desktopManager;
    private HighlightBorder? border;
    private IntPtr origin;
    private IntPtr selected;
    private bool disposed;
    private string searchText = string.Empty;
    private string pendingAccent = string.Empty;
    private bool capsLock;
    private readonly Dictionary<IntPtr, Rectangle> requestedClosures = new();

    internal bool IsActive { get; private set; }

    internal PositionalManager(Form dispatcher, Action beforeStart)
    {
        this.dispatcher = dispatcher;
        this.beforeStart = beforeStart;
        refreshTimer = new Timer { Interval = 100 };
        refreshTimer.Tick += (_, _) => Queue(RefreshSelection);
        try
        {
            desktopManager = (IVirtualDesktopManager)new VirtualDesktopManager();
        }
        catch (COMException ex)
        {
            // Cloaked windows are still excluded if the desktop service is unavailable.
            Debug.WriteLine($"PositionalManager: {ex.Message}");
        }
    }

    // Called by the existing global hook. Keep enumeration/painting outside the hook.
    internal bool HandleKeyboardEvent(Keys key, bool isKeyDown, bool isKeyUp, bool altDown, bool injected, uint scanCode = 0)
    {
        if (disposed || (injected && (int)key == 0xE8)) return false;
        if (isKeyUp) return suppressedKeys.Remove(key);
        if (!isKeyDown) return false;

        if (suppressedKeys.Contains(key))
        {
            if (IsActive && key != Keys.Capital && key != Keys.Delete) QueueKey(key, scanCode, altDown);
            return true;
        }

        if (key == Keys.F1 && altDown && !IsDown(Keys.ControlKey) && !IsDown(Keys.ShiftKey) &&
            !IsDown(Keys.LWin) && !IsDown(Keys.RWin))
        {
            suppressedKeys.Add(key);
            if (!IsActive)
            {
                IsActive = true;
                searchText = pendingAccent = string.Empty;
                capsLock = (GetKeyState((int)Keys.Capital) & 1) != 0;
                IntPtr foreground = GetForegroundWindow();
                Queue(() => Start(foreground));
            }
            // Alt went to the foreground app before F1. An unassigned menu-mask key
            // prevents its release from opening a menu, without swallowing Alt-up
            // (which would leave the application's modifier state stuck).
            keybd_event(0xE8, 0, 0, UIntPtr.Zero);
            keybd_event(0xE8, 0, 2, UIntPtr.Zero);
            return true;
        }

        if (!IsActive) return false;

        // This is a modal keyboard mode: no navigation or other shortcuts leak to
        // the foreground app. Remember key-ups even after Enter/Escape exits.
        suppressedKeys.Add(key);
        if (key == Keys.Capital) capsLock = !capsLock;
        else QueueKey(key, scanCode, altDown);
        return true;
    }

    private static bool IsDown(Keys key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    private static bool IsDirection(Keys key) =>
        key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down;

    private bool IsPressed(Keys key) => suppressedKeys.Contains(key) || IsDown(key);

    private void QueueKey(Keys key, uint scanCode, bool altDown)
    {
        if (IsDirection(key)) { Queue(() => Move(key)); return; }
        if (key == Keys.Enter) { Queue(Confirm); return; }
        if (key == Keys.Escape) { Queue(Cancel); return; }
        bool control = IsPressed(Keys.ControlKey) || IsPressed(Keys.LControlKey) || IsPressed(Keys.RControlKey);
        if (key == Keys.Back) { Queue(() => Backspace(control)); return; }
        if (key == Keys.Delete)
        {
            if (!control && !altDown && !IsPressed(Keys.Menu) && !IsPressed(Keys.LMenu) && !IsPressed(Keys.RMenu) &&
                !IsPressed(Keys.ShiftKey) && !IsPressed(Keys.LShiftKey) && !IsPressed(Keys.RShiftKey) &&
                !IsPressed(Keys.LWin) && !IsPressed(Keys.RWin)) Queue(CloseSelected);
            return;
        }

        bool rightAlt = IsPressed(Keys.RMenu);
        if ((control && !rightAlt) || (altDown && !rightAlt) || IsPressed(Keys.LWin) || IsPressed(Keys.RWin)) return;

        // The hook suppresses modifiers, so snapshot their state ourselves before
        // key-up arrives. Translate with the foreground app's keyboard layout.
        byte[] state = new byte[256];
        GetKeyboardState(state);
        foreach (Keys modifier in new[] { Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
            Keys.ControlKey, Keys.LControlKey, Keys.RControlKey, Keys.Menu, Keys.LMenu, Keys.RMenu })
            state[(int)modifier] = IsPressed(modifier) ? (byte)0x80 : (byte)0;
        state[(int)Keys.ShiftKey] = (IsPressed(Keys.ShiftKey) || IsPressed(Keys.LShiftKey) || IsPressed(Keys.RShiftKey)) ? (byte)0x80 : (byte)0;
        state[(int)Keys.ControlKey] = (control || rightAlt) ? (byte)0x80 : (byte)0;
        state[(int)Keys.Menu] = rightAlt ? (byte)0x80 : (byte)0;
        state[(int)Keys.Capital] = capsLock ? (byte)1 : (byte)0;
        state[(int)key] |= 0x80;
        IntPtr layout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), out _));
        uint scan = scanCode == 0 ? MapVirtualKeyEx((uint)key, 0, layout) : scanCode;
        Queue(() => ReadText(key, scan, state, layout));
    }

    private void ReadText(Keys key, uint scan, byte[] state, IntPtr layout)
    {
        var text = new StringBuilder(16);
        // Flag 4 leaves Windows' dead-key buffer untouched: no composition leaks
        // into the foreground application after the mode ends.
        int count = key == Keys.Packet ? 1 : ToUnicodeEx((uint)key, scan, state, text, text.Capacity, 4, layout);
        if (key == Keys.Packet) text.Append((char)scan);
        if (count == 0 || text.Length == 0) return;
        string value = text.ToString(0, Math.Min(Math.Abs(count), text.Length));
        if (count < 0)
        {
            if (pendingAccent == value)
            {
                searchText += pendingAccent;
                pendingAccent = string.Empty;
            }
            else
            {
                searchText += pendingAccent;
                pendingAccent = value;
            }
        }
        else
        {
            foreach (char character in value) if (char.IsControl(character)) return;
            searchText += ComposeDeadKey(pendingAccent, value);
            pendingAccent = string.Empty;
        }
        SearchByPrefix();
    }

    internal static string ComposeDeadKey(string accent, string value)
    {
        if (accent.Length == 0) return value;
        if (value == " " || value == accent) return accent;
        string mark = accent switch
        {
            "´" or "'" => "\u0301", "`" => "\u0300", "^" => "\u0302", "~" => "\u0303",
            "¨" or "\"" => "\u0308", "ˇ" => "\u030C", "˘" => "\u0306", "˚" or "°" => "\u030A",
            "˙" => "\u0307", "¸" => "\u0327", "˝" => "\u030B", "˛" => "\u0328", "¯" => "\u0304",
            _ => string.Empty
        };
        if (mark.Length == 0 || value.Length == 0) return accent + value;
        string first = StringInfo.GetNextTextElement(value);
        return (first + mark).Normalize(NormalizationForm.FormC) + value.Substring(first.Length);
    }

    internal static string RemoveLastTextElement(string value)
    {
        int[] starts = StringInfo.ParseCombiningCharacters(value);
        return starts.Length == 0 ? string.Empty : value.Substring(0, starts[starts.Length - 1]);
    }

    private void Backspace(bool clearAll)
    {
        if (clearAll) searchText = pendingAccent = string.Empty;
        else if (pendingAccent.Length > 0) pendingAccent = string.Empty;
        else searchText = RemoveLastTextElement(searchText);
        SearchByPrefix();
    }

    private void SearchByPrefix()
    {
        if (!SelectionIsValid()) { Cancel(); return; }
        var windows = new List<(IntPtr Handle, string Name)>();
        EnumWindows((handle, parameter) =>
        {
            if (TryGetBounds(handle, out _))
            {
                var title = new StringBuilder(GetWindowTextLength(handle) + 1);
                GetWindowText(handle, title, title.Capacity);
                windows.Add((handle, title.ToString()));
            }
            return true;
        }, IntPtr.Zero);
        selected = FindPrefixMatch(selected, windows, searchText);
        RefreshSelection();
    }

    // Native titles only. First match in Z-order wins; no match preserves selection.
    internal static IntPtr FindPrefixMatch(IntPtr current, IReadOnlyList<(IntPtr Handle, string Name)> windows, string prefix)
    {
        if (prefix.Length == 0) return current;
        foreach (var window in windows)
            if (CultureInfo.InvariantCulture.CompareInfo.IsPrefix(window.Name, prefix,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace)) return window.Handle;
        return current;
    }

    private void Queue(Action action)
    {
        try
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                if (disposed || !IsActive) return;
                try
                {
                    using (new DpiScope()) action();
                }
                catch (Exception ex)
                {
                    Cancel();
                    Debug.WriteLine($"PositionalManager: {ex}");
                }
            }));
        }
        catch (InvalidOperationException)
        {
            Cancel();
        }
    }

    private void Start(IntPtr foreground)
    {
        beforeStart();
        // Closing an existing WController picker can reveal the real target app.
        if (!TryGetBounds(foreground, out _)) foreground = GetForegroundWindow();
        if (!TryGetBounds(foreground, out Rectangle bounds))
        {
            Cancel();
            return;
        }

        origin = foreground;
        selected = foreground;
        border ??= new HighlightBorder();
        border.BackColor = GetAccentColor();
        border.ShowBorder(bounds, searchText + pendingAccent);
        refreshTimer.Start();
    }

    private void Move(Keys direction)
    {
        if (!SelectionIsValid())
        {
            Cancel();
            return;
        }

        var windows = GetWindowPositions();

        selected = FindNextWindow(selected, windows, direction);
        if (selected == IntPtr.Zero || !TryGetBounds(selected, out Rectangle targetBounds))
        {
            Cancel();
            return;
        }
        border?.ShowBorder(targetBounds, searchText + pendingAccent);
    }

    // Prefer overlap on the perpendicular axis (same row/column), then distance.
    // Diagonals are a fallback. Ties retain Z-order; there is no wraparound.
    internal static IntPtr FindNextWindow(IntPtr current,
        IReadOnlyList<(IntPtr Handle, Rectangle Bounds)> windows, Keys direction)
    {
        Rectangle source = Rectangle.Empty;
        bool found = false;
        foreach (var window in windows)
        {
            if (window.Handle != current) continue;
            source = window.Bounds;
            found = true;
            break;
        }
        if (!found) return IntPtr.Zero;
        if (!IsDirection(direction)) return current;

        IntPtr best = current;
        double bestDistance = double.MaxValue;
        int bestAlignment = int.MaxValue;
        bool horizontal = direction == Keys.Left || direction == Keys.Right;
        foreach (var window in windows)
        {
            if (window.Handle == current) continue;
            double dx = window.Bounds.Left + window.Bounds.Width / 2.0 - (source.Left + source.Width / 2.0);
            double dy = window.Bounds.Top + window.Bounds.Height / 2.0 - (source.Top + source.Height / 2.0);
            bool inDirection = direction == Keys.Right ? dx > 0 :
                direction == Keys.Left ? dx < 0 : direction == Keys.Down ? dy > 0 : dy < 0;
            if (!inDirection) continue;
            bool overlaps = horizontal
                ? window.Bounds.Top < source.Bottom && window.Bounds.Bottom > source.Top
                : window.Bounds.Left < source.Right && window.Bounds.Right > source.Left;
            int alignment = overlaps ? 0 : 1;
            if (alignment > bestAlignment) continue;
            double distance = dx * dx + dy * dy;
            if (alignment == bestAlignment && distance >= bestDistance) continue;
            bestAlignment = alignment;
            bestDistance = distance;
            best = window.Handle;
        }
        return best;
    }

    private List<(IntPtr Handle, Rectangle Bounds)> GetWindowPositions()
    {
        var windows = new List<(IntPtr Handle, Rectangle Bounds)>();
        EnumWindows((handle, _) =>
        {
            if (TryGetBounds(handle, out Rectangle bounds)) windows.Add((handle, bounds));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private void CloseSelected()
    {
        if (!SelectionIsValid()) { Cancel(); return; }
        if (!TryGetBounds(selected, out Rectangle bounds)) return;
        // Match MainForm.CloseWindow: request normal close; never terminate a process.
        if (PostMessage(selected, 0x0010, IntPtr.Zero, IntPtr.Zero)) requestedClosures[selected] = bounds;
        else Debug.WriteLine("PositionalManager: Windows refused the close request.");
    }

    internal static IntPtr FindNearestWindow(Rectangle previous, IReadOnlyList<(IntPtr Handle, Rectangle Bounds)> windows)
    {
        IntPtr nearest = IntPtr.Zero;
        double bestDistance = double.MaxValue;
        foreach (var window in windows)
        {
            double dx = window.Bounds.Left + window.Bounds.Width / 2.0 - (previous.Left + previous.Width / 2.0);
            double dy = window.Bounds.Top + window.Bounds.Height / 2.0 - (previous.Top + previous.Height / 2.0);
            double distance = dx * dx + dy * dy;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = window.Handle;
        }
        return nearest;
    }

    private bool SelectionIsValid()
    {
        bool selectionGone = !TryGetBounds(selected, out _);
        bool requested = requestedClosures.TryGetValue(selected, out Rectangle previous);
        IntPtr oldSelection = selected;
        // Only a close explicitly requested here may hand off the foreground anchor.
        // A save dialog leaves its parent visible: normal focus-change cancellation
        // then releases the keyboard so the dialog can be answered.
        if (requestedClosures.ContainsKey(origin) &&
            (!IsWindow(origin) || !IsWindowVisible(origin) || IsIconic(origin)))
        {
            requestedClosures.Remove(origin);
            origin = GetForegroundWindow();
        }
        if (GetForegroundWindow() != origin) return false;
        if (selectionGone && requested)
        {
            selected = FindNearestWindow(previous, GetWindowPositions());
            requestedClosures.Remove(oldSelection);
        }
        return TryGetBounds(selected, out _);
    }

    private void RefreshSelection()
    {
        if (!SelectionIsValid() || !TryGetBounds(selected, out Rectangle bounds))
        {
            Cancel();
            return;
        }
        border?.ShowBorder(bounds, searchText + pendingAccent);
    }

    private void Confirm()
    {
        IntPtr target = SelectionIsValid() ? selected : IntPtr.Zero;
        Cancel();
        if (target != IntPtr.Zero && !SetForegroundWindow(target))
            Debug.WriteLine("PositionalManager: Windows refused foreground activation.");
    }

    internal void Cancel()
    {
        IsActive = false;
        refreshTimer.Stop();
        border?.Hide();
        selected = origin = IntPtr.Zero;
        searchText = pendingAccent = string.Empty;
        requestedClosures.Clear();
    }

    private bool TryGetBounds(IntPtr handle, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (handle == IntPtr.Zero || !IsWindow(handle) || !IsWindowVisible(handle) || IsIconic(handle) ||
            !IsWindowEnabled(handle) || GetWindowTextLength(handle) == 0 || handle == GetShellWindow())
            return false;
        foreach (Form form in Application.OpenForms)
            if (form.IsHandleCreated && form.Handle == handle) return false;

        long style = IntPtr.Size == 8 ? GetWindowLongPtr64(handle, -20).ToInt64() : GetWindowLong32(handle, -20);
        if ((style & 0x80) != 0 || (style & 0x08000000) != 0) return false; // Tool/no-activate windows.
        if (DwmGetWindowAttribute(handle, 14, out int cloaked, sizeof(int)) >= 0 && cloaked != 0)
            return false;
        if (desktopManager != null &&
            (desktopManager.IsWindowOnCurrentVirtualDesktop(handle, out int current) < 0 || current == 0))
            return false;

        if (DwmGetWindowAttribute(handle, 9, out NativeRect rect, Marshal.SizeOf(typeof(NativeRect))) < 0 &&
            !GetWindowRect(handle, out rect)) return false;
        bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;
        foreach (Screen screen in Screen.AllScreens)
            if (screen.Bounds.IntersectsWith(bounds)) return true;
        return false;
    }

    private static Color GetAccentColor()
    {
        try
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                // AccentColor is ABGR, unlike DwmGetColorizationColor's ARGB.
                if (key?.GetValue("AccentColor") is int value)
                    return Color.FromArgb(value & 255, (value >> 8) & 255, (value >> 16) & 255);
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException ||
            ex is UnauthorizedAccessException || ex is System.IO.IOException)
        {
            Debug.WriteLine($"PositionalManager accent color: {ex.Message}");
        }
        if (DwmGetColorizationColor(out uint color, out _) >= 0)
            return Color.FromArgb((int)((color >> 16) & 255), (int)((color >> 8) & 255), (int)(color & 255));
        return SystemColors.Highlight;
    }

    public void Dispose()
    {
        if (disposed) return;
        Cancel();
        disposed = true;
        refreshTimer.Dispose();
        border?.Dispose();
        suppressedKeys.Clear();
        if (desktopManager != null) Marshal.FinalReleaseComObject(desktopManager);
    }

    internal static Color GetContrastTextColor(Color background)
    {
        double Linear(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        double luminance = 0.2126 * Linear(background.R) + 0.7152 * Linear(background.G) + 0.0722 * Linear(background.B);
        double blackContrast = (luminance + 0.05) / 0.05;
        double whiteContrast = 1.05 / (luminance + 0.05);
        return blackContrast >= whiteContrast ? Color.Black : Color.White;
    }

    private static GraphicsPath RoundedPath(RectangleF bounds, float radius, float? topLeftRadius = null)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) path.AddRectangle(bounds);
        else
        {
            float topLeft = Math.Min((topLeftRadius ?? radius) * 2, Math.Min(bounds.Width, bounds.Height));
            path.AddArc(bounds.Left, bounds.Top, topLeft, topLeft, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
        }
        return path;
    }

    internal static Bitmap RenderHighlight(Size size, int thickness, int radius, Rectangle queryBounds,
        Color accent, string query, Font font, float dpi = 96)
    {
        var image = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        try
        {
            image.SetResolution(dpi, dpi);
            using Graphics graphics = Graphics.FromImage(image);
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var fill = new SolidBrush(accent);
            if (size.Width <= 1 || size.Height <= 1) { graphics.Clear(accent); return image; }

            using var shape = RoundedPath(new RectangleF(0.5f, 0.5f, size.Width - 1, size.Height - 1), radius);
            shape.FillMode = FillMode.Winding;
            if (size.Width > thickness * 2 + 1 && size.Height > thickness * 2 + 1)
            {
                using var hole = RoundedPath(new RectangleF(thickness + 0.5f, thickness + 0.5f,
                    size.Width - thickness * 2 - 1, size.Height - thickness * 2 - 1), Math.Max(0, radius - thickness));
                hole.Reverse();
                shape.AddPath(hole, false);
            }
            if (queryBounds.Width > 1 && queryBounds.Height > 1)
            {
                using var tab = RoundedPath(new RectangleF(queryBounds.X + 0.5f, queryBounds.Y + 0.5f,
                    queryBounds.Width - 1, queryBounds.Height - 1), Math.Max(1, radius / 2f), radius);
                shape.AddPath(tab, false);
            }
            // One winding-filled path unions the tab with the ring without seams
            // or double-blended edges. Fractional alpha smooths both sides of the ring.
            graphics.FillPath(fill, shape);
            if (!queryBounds.IsEmpty && query.Length > 0)
            {
                float scale = dpi / 96f;
                RectangleF textBounds = RectangleF.Inflate(queryBounds, -6 * scale, -4 * scale);
                if (textBounds.Width > 0 && textBounds.Height > 0)
                {
                    using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
                    format.FormatFlags |= StringFormatFlags.NoWrap;
                    format.LineAlignment = StringAlignment.Center;
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    using var textBrush = new SolidBrush(GetContrastTextColor(accent));
                    // ClearType assumes an opaque background; grayscale antialiasing
                    // preserves correct alpha on this transparent overlay.
                    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                    graphics.DrawString(query, font, textBrush, textBounds, format);
                }
            }
            return image;
        }
        catch { image.Dispose(); throw; }
    }

    private sealed class HighlightBorder : Form
    {
        private readonly Font queryFont = new Font("Microsoft Sans Serif", 8.25f);
        private uint dpi;
        private Size renderedSize;
        private Color renderedColor;

        internal HighlightBorder()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                // Layered + transparent makes the entire overlay click-through.
                parameters.ExStyle |= 0x08000000 | 0x80000 | 0x20 | 0x80;
                return parameters;
            }
        }

        internal void ShowBorder(Rectangle bounds, string query)
        {
            Bounds = bounds;
            uint nextDpi = GetDpiForWindow(Handle);
            if (renderedSize != bounds.Size || Text != query || nextDpi != dpi || renderedColor != BackColor)
            {
                dpi = nextDpi;
                Text = query;
                Rectangle queryBounds = Rectangle.Empty;
                if (query.Length > 0)
                {
                    using Graphics graphics = CreateGraphics();
                    SizeF measured = graphics.MeasureString(query, queryFont, int.MaxValue, StringFormat.GenericTypographic);
                    queryBounds = new Rectangle(0, 0, Math.Min(Math.Max(1, Width - Scale(3)), (int)Math.Ceiling(measured.Width) + Scale(12)),
                        Math.Min(Height, (int)Math.Ceiling(measured.Height) + Scale(8)));
                }
                using Bitmap image = RenderHighlight(bounds.Size, Scale(3), Scale(10), queryBounds, BackColor, query, queryFont, dpi);
                Upload(image, bounds.Location);
                renderedSize = bounds.Size;
                renderedColor = BackColor;
            }
            if (!Visible) Show();
            SetWindowPos(Handle, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x10 | 0x40);
        }

        private int Scale(int pixels) => Math.Max(1, (int)Math.Round(pixels * dpi / 96.0));

        private void Upload(Bitmap image, Point location)
        {
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr memory = IntPtr.Zero, bitmap = IntPtr.Zero, previous = IntPtr.Zero;
            try
            {
                if (screen == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                memory = CreateCompatibleDC(screen);
                if (memory == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                bitmap = image.GetHbitmap(Color.FromArgb(0));
                previous = SelectObject(memory, bitmap);
                if (previous == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                var source = Point.Empty;
                Size size = image.Size;
                var blend = new BlendFunction { SourceConstantAlpha = 255, AlphaFormat = 1 };
                if (!UpdateLayeredWindow(Handle, screen, ref location, ref size, memory, ref source, 0, ref blend, 2))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                if (previous != IntPtr.Zero) SelectObject(memory, previous);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memory != IntPtr.Zero) DeleteDC(memory);
                if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) queryFont.Dispose();
            base.Dispose(disposing);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            internal byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr handle, IntPtr screen, ref Point destination, ref Size size,
            IntPtr source, ref Point sourcePoint, uint colorKey, ref BlendFunction blend, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr handle);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr handle, IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr value);
    }

    // DWM frame bounds are physical pixels. Keep enumeration and overlay operations
    // in the same DPI context, without changing the existing app's scaling policy.
    private readonly struct DpiScope : IDisposable
    {
        private readonly IntPtr previous;
        public DpiScope() => previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        public void Dispose()
        {
            if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left, Top, Right, Bottom;
    }

    [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr handle, out int onCurrentDesktop);
        [PreserveSig]
        int GetWindowDesktopId(IntPtr handle, out Guid desktopId);
        [PreserveSig]
        int MoveWindowToDesktop(IntPtr handle, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
    }

    [ComImport, Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")]
    private class VirtualDesktopManager { }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int capacity);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int key);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(uint key, uint scan, byte[] state, [Out] StringBuilder text, int capacity, uint flags, IntPtr layout);
    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr handle, int index);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out NativeRect value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetColorizationColor(out uint color, [MarshalAs(UnmanagedType.Bool)] out bool opaque);
}
