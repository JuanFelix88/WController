using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using WController;

internal static class PositionalManagerTests
{
    private static int assertions;
    private static int leakedNavigation;
    private static int menuActivations;
    private static int closeRequests;
    private static IntPtr refusedClose;
    private static readonly WindowProc windowProc = WindowCallback;
    private static readonly List<IntPtr> testWindows = new();

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            TestGeometry();
            TestDirectionalAlignment();
            TestPrefixSearch();
            TestTextEditing();
            TestBorderPresentation();
            TestCloseReplacement();
            if (Array.IndexOf(args, "--desktop") >= 0) TestDesktop();
            Console.WriteLine($"PASS: {assertions} assertions.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
    }

    private static (IntPtr Handle, Rectangle Bounds) Window(int id, int x, int y, int width = 100, int height = 100) =>
        (new IntPtr(id), new Rectangle(x, y, width, height));

    private static void TestGeometry()
    {
        var current = new IntPtr(1);
        var grid = new[] { Window(1, 0, 0), Window(2, 150, 0), Window(3, -150, 0),
            Window(4, 0, 150), Window(5, 0, -150) };
        Assert(PositionalManager.FindNextWindow(current, grid, Keys.Right) == new IntPtr(2), "Right neighbor");
        Assert(PositionalManager.FindNextWindow(current, grid, Keys.Left) == new IntPtr(3), "Left neighbor");
        Assert(PositionalManager.FindNextWindow(current, grid, Keys.Down) == new IntPtr(4), "Lower neighbor");
        Assert(PositionalManager.FindNextWindow(current, grid, Keys.Up) == new IntPtr(5), "Upper neighbor");
        Assert(PositionalManager.FindNextWindow(new IntPtr(2), grid, Keys.Right) == new IntPtr(2), "No wrap at edge");
        Assert(PositionalManager.FindNextWindow(new IntPtr(99), grid, Keys.Right) == IntPtr.Zero, "Closed/missing source");
        Assert(PositionalManager.FindNextWindow(current, grid, Keys.A) == current, "Ignore non-direction");
        Assert(PositionalManager.FindNextWindow(current, Array.Empty<(IntPtr, Rectangle)>(), Keys.Left) == IntPtr.Zero,
            "Empty desktop");
        Assert(PositionalManager.FindNextWindow(current, new[] { Window(1, 0, 0) }, Keys.Right) == current,
            "Single window");
        Assert(PositionalManager.FindNextWindow(current,
            new[] { Window(1, 0, 0), Window(2, 300, 0), Window(3, 100, 100) }, Keys.Right) == new IntPtr(2),
            "Aligned window beats closer diagonal; touching edges are not overlap");
        Assert(PositionalManager.FindNextWindow(current,
            new[] { Window(1, 0, 0), Window(2, -1, 0), Window(3, 200, 100) }, Keys.Right) == new IntPtr(3),
            "Exclude wrong half-plane");
        Assert(PositionalManager.FindNextWindow(current,
            new[] { Window(1, 0, 0), Window(2, 100, -100), Window(3, 100, 100) }, Keys.Right) == new IntPtr(2),
            "Stable tie in Z-order");
        Assert(PositionalManager.FindNextWindow(current,
            new[] { Window(1, -1800, -700), Window(2, -1600, -650) }, Keys.Right) == new IntPtr(2),
            "Negative monitor coordinates");
        Assert(PositionalManager.FindNextWindow(current,
            new[] { Window(1, 0, 0), Window(2, 0, 0), Window(3, 10, 0) }, Keys.Right) == new IntPtr(3),
            "Equal-center overlapping windows do not count as directional neighbors");
        Assert(PositionalManager.FindNextWindow(current,
            new[] { Window(1, 0, 0, 400, 100), Window(2, 150, 0, 50, 100), Window(3, 410, 0) }, Keys.Right) == new IntPtr(3),
            "Direction uses centers, not left edges");
    }

    private static void TestDirectionalAlignment()
    {
        var current = new IntPtr(1);
        foreach (Keys direction in new[] { Keys.Left, Keys.Right, Keys.Up, Keys.Down })
        {
            Rectangle Orient(Rectangle rect) => direction switch
            {
                Keys.Right => new Rectangle(-rect.Right, rect.Top, rect.Width, rect.Height),
                Keys.Up => new Rectangle(rect.Top, rect.Left, rect.Height, rect.Width),
                Keys.Down => new Rectangle(rect.Top, -rect.Right, rect.Height, rect.Width),
                _ => rect
            };

            var source = (current, Orient(new Rectangle(500, 500, 200, 200)));
            var diagonal = (new IntPtr(3), Orient(new Rectangle(490, 280, 200, 200)));
            var fartherAligned = (new IntPtr(4), Orient(new Rectangle(-300, 500, 200, 200)));
            var aligned = (new IntPtr(2), Orient(new Rectangle(0, 510, 200, 180)));
            Assert(PositionalManager.FindNextWindow(current,
                new[] { source, diagonal, fartherAligned, aligned }, direction) == new IntPtr(2),
                $"{direction}: choose nearest aligned window even when diagonal is closer and enumerated first");
            Assert(PositionalManager.FindNextWindow(current, new[] { source, diagonal }, direction) == new IntPtr(3),
                $"{direction}: retain diagonal fallback when no aligned window exists");
        }

        // Layout from the reported screenshot: rows have different widths/centers.
        var screenshot = new[]
        {
            Window(1, 552, 457, 630, 447), // Selected lower-middle window.
            Window(2, 3, 457, 545, 447),   // Zed, lower left.
            Window(3, 640, 0, 632, 447),   // Upper middle.
            Window(4, 0, 0, 631, 447),    // Upper left.
            Window(5, 1188, 457, 460, 447),
            Window(6, 1280, 0, 630, 447),
            Window(7, 1663, 457, 244, 326)
        };
        Assert(PositionalManager.FindNextWindow(current, screenshot, Keys.Left) == new IntPtr(2),
            "Screenshot: left selects lower-left Zed window");
        Assert(PositionalManager.FindNextWindow(current, screenshot, Keys.Right) == new IntPtr(5),
            "Screenshot: right stays in lower row instead of jumping to upper-middle window");
        Assert(PositionalManager.FindNextWindow(current, screenshot, Keys.Up) == new IntPtr(3),
            "Screenshot: up selects upper-middle window");
        Assert(PositionalManager.FindNextWindow(current, screenshot, Keys.Down) == current,
            "Screenshot: down without candidates does not wrap");
        Assert(PositionalManager.FindNextWindow(current,
            new[] { Window(1, 0, 0), Window(2, 500, 99), Window(3, 20, -101) }, Keys.Right) == new IntPtr(2),
            "Partial perpendicular overlap counts as alignment");
    }

    private static void TestPrefixSearch()
    {
        var current = new IntPtr(99);
        var windows = new[] { (new IntPtr(1), "Árvore – projeto"), (new IntPtr(2), "ação"),
            (new IntPtr(3), "EDITOR"), (new IntPtr(4), "Árvore – outro projeto"), (new IntPtr(5), "Cafe\u0301") };
        Assert(PositionalManager.FindPrefixMatch(current, windows, "arv") == new IntPtr(1), "Ignore title accents");
        Assert(PositionalManager.FindPrefixMatch(current, windows, "ÁRV") == new IntPtr(1), "Ignore case and query accents");
        Assert(PositionalManager.FindPrefixMatch(current, windows, "acao") == new IntPtr(2), "Ignore cedilla and tilde");
        Assert(PositionalManager.FindPrefixMatch(current, windows, "editor") == new IntPtr(3), "Case insensitive native title");
        Assert(PositionalManager.FindPrefixMatch(current, windows, "café") == new IntPtr(5), "Decomposed title matches composed query");
        Assert(PositionalManager.FindPrefixMatch(current, windows, "a\u0301r") == new IntPtr(1), "Decomposed query");
        Assert(PositionalManager.FindPrefixMatch(new IntPtr(4), windows, "arvore") == new IntPtr(1), "Multiple matches use Z-order");
        Assert(PositionalManager.FindPrefixMatch(current, windows, "projeto") == current, "Prefix only, not substring");
        Assert(PositionalManager.FindPrefixMatch(current, windows, "my alias") == current, "No alias lookup");
        Assert(PositionalManager.FindPrefixMatch(current, windows, "") == current, "Clearing query preserves selection");
        Assert(PositionalManager.FindPrefixMatch(current, Array.Empty<(IntPtr, string)>(), "x") == current, "No matches preserve selection");
    }

    private static void TestTextEditing()
    {
        Assert(PositionalManager.RemoveLastTextElement("") == "", "Backspace on empty query");
        Assert(PositionalManager.RemoveLastTextElement("abc") == "ab", "Backspace removes character");
        Assert(PositionalManager.RemoveLastTextElement("cafe\u0301") == "caf", "Backspace removes combining accent with character");
        Assert(PositionalManager.RemoveLastTextElement("A🪟") == "A", "Backspace removes surrogate pair");
        Assert(PositionalManager.ComposeDeadKey("´", "e") == "é", "Acute dead key composition");
        Assert(PositionalManager.ComposeDeadKey("~", "a") == "ã", "Tilde dead key composition");
        Assert(PositionalManager.ComposeDeadKey("^", "o") == "ô", "Circumflex dead key composition");
        Assert(PositionalManager.ComposeDeadKey("`", "a") == "à", "Grave dead key composition");
        Assert(PositionalManager.ComposeDeadKey("¨", "u") == "ü", "Diaeresis dead key composition");
        Assert(PositionalManager.ComposeDeadKey("´", " ") == "´", "Dead key plus space is literal accent");
        Assert(PositionalManager.ComposeDeadKey("", "e") == "e", "Plain text unchanged");
    }

    private static void TestBorderPresentation()
    {
        using var font = new Font("Microsoft Sans Serif", 9.75f);
        using (Bitmap ring = PositionalManager.RenderHighlight(new Size(300, 200), 3, 10, Rectangle.Empty, Color.DodgerBlue, "", font))
        {
            Assert(ring.GetPixel(0, 0).A == 0, "Outer corners are transparent");
            Assert(ring.GetPixel(150, 1).A == 255, "Top border opaque");
            Assert(ring.GetPixel(1, 100).A == 255, "Side border opaque");
            Assert(ring.GetPixel(150, 100).A == 0, "Window content stays transparent");
            bool fractional = false;
            for (int y = 0; y < 15; y++)
                for (int x = 0; x < 15; x++)
                {
                    byte alpha = ring.GetPixel(x, y).A;
                    fractional |= alpha > 0 && alpha < 255;
                }
            Assert(fractional, "Curved edges contain fractional alpha for antialiasing");
        }
        using (Bitmap withQuery = PositionalManager.RenderHighlight(new Size(300, 200), 3, 10, new Rectangle(0, 0, 100, 26),
            Color.DodgerBlue, "query", font))
        {
            Assert(withQuery.GetPixel(15, 12).A == 255, "Query tab fills top-left area");
            Assert(withQuery.GetPixel(0, 0).A == 0, "Query tab preserves rounded outer corner");
            Assert(withQuery.GetPixel(150, 100).A == 0, "Query tab does not fill window content");
        }
        using (Bitmap tiny = PositionalManager.RenderHighlight(new Size(8, 6), 3, 10, Rectangle.Empty, Color.Blue, "", font))
            Assert(tiny.GetPixel(4, 3).A == 255, "Small window bitmap remains valid");
        using (Bitmap scaled = PositionalManager.RenderHighlight(new Size(600, 400), 6, 20, new Rectangle(0, 0, 200, 52),
            Color.Blue, "query", font, 192))
            Assert(scaled.GetPixel(30, 24).A == 255 && scaled.GetPixel(300, 200).A == 0, "DPI-scaled tab and transparent center");

        double Luminance(Color color)
        {
            double Linear(byte value) => value / 255.0 <= 0.04045 ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + 0.055) / 1.055, 2.4);
            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }
        foreach (Color background in new[] { Color.Black, Color.White, Color.Red, Color.Blue, Color.Lime,
            Color.Cyan, Color.Yellow, Color.FromArgb(119, 119, 119), Color.FromArgb(0, 120, 215) })
        {
            double luminance = Luminance(background);
            double black = (luminance + 0.05) / 0.05;
            double white = 1.05 / (luminance + 0.05);
            Color foreground = PositionalManager.GetContrastTextColor(background);
            Assert(foreground == (black >= white ? Color.Black : Color.White), "Maximal black/white WCAG contrast");
            Assert((foreground == Color.Black ? black : white) >= 4.5, "Small query text meets WCAG AA contrast");
        }
    }

    private static void TestCloseReplacement()
    {
        var previous = new Rectangle(100, 100, 100, 100);
        Assert(PositionalManager.FindNearestWindow(previous, Array.Empty<(IntPtr, Rectangle)>()) == IntPtr.Zero, "No close replacement exits mode");
        Assert(PositionalManager.FindNearestWindow(previous, new[] { Window(1, 500, 100), Window(2, 210, 100) }) == new IntPtr(2), "Nearest replacement after close");
        Assert(PositionalManager.FindNearestWindow(previous, new[] { Window(1, 0, 100), Window(2, 200, 100) }) == new IntPtr(1), "Replacement ties retain Z-order");
        Assert(PositionalManager.FindNearestWindow(new Rectangle(-1000, -500, 100, 100),
            new[] { Window(1, -900, -500), Window(2, 0, 0) }) == new IntPtr(1), "Replacement handles negative monitor coordinates");
    }

    private static void TestDesktop()
    {
        IntPtr previousForeground = GetForegroundWindow();
        using var dispatcher = new Form();
        _ = dispatcher.Handle;
        using var manager = new PositionalManager(dispatcher, () => { });
        HookProc hookProc = (code, message, data) =>
        {
            if (code >= 0)
            {
                int flags = Marshal.ReadInt32(data, 8);
                bool down = message == new IntPtr(0x100) || message == new IntPtr(0x104);
                bool up = message == new IntPtr(0x101) || message == new IntPtr(0x105);
                if (manager.HandleKeyboardEvent((Keys)Marshal.ReadInt32(data), down, up,
                    (flags & 0x20) != 0, (flags & 0x10) != 0, (uint)Marshal.ReadInt32(data, 4))) return new IntPtr(1);
            }
            return CallNextHookEx(IntPtr.Zero, code, message, data);
        };
        IntPtr hook = IntPtr.Zero;
        try
        {
            var windowClass = new WindowClass
            {
                Size = (uint)Marshal.SizeOf(typeof(WindowClass)),
                Instance = GetModuleHandle(null),
                Procedure = windowProc,
                ClassName = "WController.PositionalManager.Tests"
            };
            Assert(RegisterClassEx(ref windowClass) != 0, "Register native test window class");
            IntPtr first = CreateTestWindow(windowClass, "WCPOS Árvore – origem", 100);
            IntPtr second = CreateTestWindow(windowClass, "WCPOS Órbita – destino", 110);
            ShowWindow(first, 5);
            ShowWindow(second, 5);
            // Test setup only: simulate an Alt gesture so Windows permits this
            // background-launched test process to focus its own native windows.
            Key(Keys.Menu, false);
            Key((Keys)0xE8, false);
            Key((Keys)0xE8, true);
            Pump();
            SetForegroundWindow(first);
            Key(Keys.Menu, true);
            Pump();
            Assert(GetForegroundWindow() == first, "Origin is focused before test");
            hook = SetWindowsHookEx(13, hookProc, GetModuleHandle(null), 0);
            Assert(hook != IntPtr.Zero, "Install real global keyboard hook");

            ActivateMode();
            Assert(manager.IsActive, "Alt+F1 enters mode after Alt release");
            Assert(Selected(manager) == first, "Initially highlight foreground window");
            Assert(GetForegroundWindow() == first, "Showing border does not steal focus");
            Form highlight = (Form)typeof(PositionalManager).GetField("border", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(manager)!;
            Assert(highlight.Visible && !highlight.ShowInTaskbar, "Border visible without taskbar entry");
            Assert(highlight.Region == null, "Per-pixel overlay does not use an aliased window region");
            Assert(menuActivations == 0, "Releasing activation Alt does not open the application's menu");

            Press(Keys.Right);
            Assert(Selected(manager) == second, "Right selects closest window after releasing Alt");
            Assert(GetForegroundWindow() == first, "Arrow changes highlight only");
            bool capsAtEntry = (bool)typeof(PositionalManager).GetField("capsLock", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager)!;
            Key(Keys.LShiftKey, false);
            Press(Keys.W);
            Key(Keys.LShiftKey, true);
            Pump();
            Assert(highlight.Text == (capsAtEntry ? "w" : "W"), "Suppressed Shift still affects query text");
            Press(Keys.Back);
            Assert(highlight.Text.Length == 0, "Backspace clears the query badge");
            Press(Keys.Capital);
            Press(Keys.W);
            Assert(highlight.Text == (capsAtEntry ? "w" : "W"), "Modal Caps Lock affects query text");
            Press(Keys.Back);
            Press(Keys.Capital);
            foreach (Keys key in new[] { Keys.W, Keys.C, Keys.P, Keys.O, Keys.S, Keys.Space, Keys.O, Keys.R, Keys.B }) Press(key);
            Assert(Selected(manager) == second && GetForegroundWindow() == first, "Typing matches accented native prefix without focusing");
            Assert(string.Equals(highlight.Text, "wcpos orb", StringComparison.OrdinalIgnoreCase), "Query displayed in border");
            Assert(highlight.Region == null && highlight.Text.Length > 0, "Query tab uses the per-pixel overlay");
            Assert(WindowFromPoint(new Point(highlight.Left + highlight.Width / 2, highlight.Top + 1)) != highlight.Handle,
                "Border remains click-through");
            string? screenshot = Environment.GetEnvironmentVariable("WCPOS_TEST_SCREENSHOT");
            if (!string.IsNullOrEmpty(screenshot))
            {
                IntPtr previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
                try
                {
                    highlight.Update();
                    using var image = new Bitmap(highlight.Width, highlight.Height);
                    using (Graphics graphics = Graphics.FromImage(image))
                        graphics.CopyFromScreen(highlight.Location, Point.Empty, highlight.Size);
                    image.Save(screenshot, System.Drawing.Imaging.ImageFormat.Png);
                }
                finally { if (previousDpi != IntPtr.Zero) SetThreadDpiAwarenessContext(previousDpi); }
            }
            Press(Keys.X);
            Assert(Selected(manager) == second && highlight.Text.EndsWith("x", StringComparison.OrdinalIgnoreCase), "No match keeps selection and shows query");
            Press(Keys.Back);
            Assert(string.Equals(highlight.Text, "wcpos orb", StringComparison.OrdinalIgnoreCase), "Backspace edits query");
            Assert(manager.IsActive && leakedNavigation == 0, "Search input remains modal");
            int clearIndex = 0;
            foreach (Keys control in new[] { Keys.LControlKey, Keys.RControlKey })
            {
                IntPtr selectionBeforeClear = Selected(manager);
                typeof(PositionalManager).GetField("pendingAccent", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(manager, "´");
                Key(control, false);
                Key(Keys.Back, false);
                Key(Keys.Back, true);
                Key(control, true); // Release before queued editing runs: modifier must be snapshotted.
                Pump();
                Assert(highlight.Text.Length == 0, $"{control}+Backspace clears the entire query and badge");
                Assert((string)typeof(PositionalManager).GetField("pendingAccent", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(manager)! == "", "Ctrl+Backspace clears pending dead-key composition");
                Assert(manager.IsActive && Selected(manager) == selectionBeforeClear && GetForegroundWindow() == first,
                    "Clearing search keeps mode, selection, and app focus");
                Press(Keys.Back);
                Assert(highlight.Text.Length == 0, "Backspace after clearing is harmless");
                Keys[] nextPrefix = clearIndex++ == 0
                    ? new[] { Keys.W, Keys.C, Keys.P, Keys.O, Keys.S, Keys.Space, Keys.A, Keys.R, Keys.V }
                    : new[] { Keys.W, Keys.C, Keys.P, Keys.O, Keys.S, Keys.Space, Keys.O, Keys.R, Keys.B };
                foreach (Keys key in nextPrefix) Press(key);
                Assert(Selected(manager) == (clearIndex == 1 ? first : second), "Typing after Ctrl+Backspace starts a fresh search");
            }
            Key(Keys.Enter, false);
            Pump();
            Assert(!manager.IsActive && GetForegroundWindow() == second, "Enter focuses selected app and exits");
            Assert(!highlight.Visible, "Enter removes border");
            Key(Keys.Enter, false); // Autorepeat after exit must not leak.
            Key(Keys.Enter, true);
            Pump();
            Assert(leakedNavigation == 0, "Enter repeat and release are suppressed after confirmation");

            ActivateMode();
            Press(Keys.Left);
            Assert(highlight.Text.Length == 0, "Reentry resets the search query");
            Assert(Selected(manager) == first, "Left returns to origin spatially");
            Press(Keys.Escape);
            Assert(!manager.IsActive && !highlight.Visible && GetForegroundWindow() == second,
                "Escape removes highlight without changing focus");

            ActivateMode();
            Key(Keys.Menu, false);
            Key(Keys.F1, false);
            Key(Keys.F1, false);
            Key(Keys.F1, true);
            Key(Keys.Menu, true);
            Pump();
            Assert(manager.IsActive && Selected(manager) == second, "Repeated Alt+F1 does not toggle or restart mode");
            Press(Keys.Left);
            ShowWindow(first, 6); // Minimize selection while mode is active.
            Pump(180);
            Assert(!manager.IsActive && !highlight.Visible, "Minimizing selected window cancels and cleans border");

            ActivateMode();
            Assert(manager.IsActive, "Minimized neighbor does not prevent starting on current app");
            Press(Keys.Left);
            Assert(Selected(manager) != first, "Minimized windows are not candidates");
            Press(Keys.Escape);
            ShowWindow(first, 9);
            SetForegroundWindow(second);
            Pump();

            ActivateMode();
            Press(Keys.Left);
            DestroyWindow(first);
            testWindows.Remove(first);
            Pump(180);
            Assert(!manager.IsActive && !highlight.Visible, "Closing selection cancels and cleans border");

            IntPtr third = CreateTestWindow(windowClass, "WCPOS Fechar – teste", 120);
            ShowWindow(third, 4);
            Pump(180);
            SetForegroundWindow(second);
            Pump();
            ActivateMode();
            Press(Keys.Right);
            Assert(Selected(manager) == third, $"Select background test window for closing (active={manager.IsActive}, selected={Selected(manager)}, target={third}, origin={second}, foreground={GetForegroundWindow()})");
            int beforeClose = closeRequests;
            Key(Keys.LControlKey, false);
            Press(Keys.Delete);
            Key(Keys.LControlKey, true);
            Pump();
            Assert(closeRequests == beforeClose && IsWindow(third), "Ctrl+Delete does not close windows");
            foreach (Keys modifier in new[] { Keys.LShiftKey, Keys.LMenu })
            {
                Key(modifier, false);
                Press(Keys.Delete);
                Key(modifier, true);
                Pump();
                Assert(closeRequests == beforeClose && IsWindow(third), $"{modifier}+Delete does not close windows (requests={closeRequests}, before={beforeClose}, alive={IsWindow(third)}, active={manager.IsActive})");
            }
            refusedClose = third;
            Press(Keys.Delete);
            Pump(180);
            Assert(closeRequests == beforeClose + 1 && IsWindow(third) && manager.IsActive, "Application may refuse normal WM_CLOSE");
            Press(Keys.Delete);
            Pump(180);
            Assert(closeRequests == beforeClose + 2 && IsWindow(third), "Another deliberate Delete may retry a refused close");
            Press(Keys.Escape);
            refusedClose = IntPtr.Zero;

            ActivateMode();
            Press(Keys.Right);
            Key(Keys.Delete, false);
            Pump(180);
            Assert(!IsWindow(third), "Delete sends normal close to the selected background window");
            testWindows.Remove(third);
            Assert(manager.IsActive && Selected(manager) == second && GetForegroundWindow() == second,
                "Closing background selection keeps mode and highlights nearest remaining window");
            int afterClose = closeRequests;
            Key(Keys.Delete, false);
            Pump();
            Assert(closeRequests == afterClose && IsWindow(second), "Delete autorepeat never closes another window");
            Key(Keys.Delete, true);
            Pump();

            IntPtr fourth = CreateTestWindow(windowClass, "WCPOS Próxima – teste", 120);
            ShowWindow(fourth, 5);
            SetForegroundWindow(second);
            Pump();
            // A deliberate focus change cancels the old session; start on its origin again.
            if (manager.IsActive) Press(Keys.Escape);
            ActivateMode();
            Assert(Selected(manager) == second, "Origin selected before closing foreground window");
            Press(Keys.Delete);
            Pump(180);
            Assert(!IsWindow(second), "Delete closes foreground selection normally");
            testWindows.Remove(second);
            Assert(manager.IsActive && Selected(manager) == fourth, "Closing origin hands off anchor and keeps mode active");
            Press(Keys.Escape);

            ActivateMode();
            Assert(manager.IsActive, "Can reenter after target closes");
            SetForegroundWindow(previousForeground);
            Pump(180);
            Assert(!manager.IsActive, "External focus change cancels mode");
            Assert(leakedNavigation == 0 && menuActivations == 0, "No captured keys or activation menu leaked");
        }
        finally
        {
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
            manager.Dispose();
            foreach (IntPtr handle in testWindows) DestroyWindow(handle);
            testWindows.Clear();
            SetForegroundWindow(previousForeground);
            GC.KeepAlive(hookProc);
        }
    }

    private static IntPtr Selected(PositionalManager manager) =>
        (IntPtr)typeof(PositionalManager).GetField("selected", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager)!;

    private static void ActivateMode()
    {
        Key(Keys.Menu, false);
        Key(Keys.F1, false);
        Key(Keys.F1, true);
        Key(Keys.Menu, true);
        Pump();
    }

    private static void Press(Keys key)
    {
        Key(key, false);
        Key(key, true);
        Pump();
    }

    private static void Key(Keys key, bool up)
    {
        // Preserve E0 scan codes for Delete/arrows/right-side modifiers. Without
        // EXTENDEDKEY, Windows treats Delete as numpad input and synthesizes Shift-up.
        uint scan = MapVirtualKey((uint)key, 4);
        bool extended = (scan & 0xFF00) == 0xE000 || key == Keys.Delete || key == Keys.Left || key == Keys.Right ||
            key == Keys.Up || key == Keys.Down || key == Keys.RControlKey || key == Keys.RMenu;
        uint flags = (up ? 2u : 0u) | (extended ? 1u : 0u);
        keybd_event((byte)key, (byte)scan, flags, UIntPtr.Zero);
    }

    private static void Pump(int milliseconds = 60)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(2);
        }
    }

    private static IntPtr CreateTestWindow(WindowClass windowClass, string title, int x)
    {
        IntPtr window = CreateWindowEx(0, windowClass.ClassName, title, 0x00CF0000,
            x, 100, 300, 200, IntPtr.Zero, IntPtr.Zero, windowClass.Instance, IntPtr.Zero);
        Assert(window != IntPtr.Zero, "Create native test window");
        testWindows.Add(window);
        return window;
    }

    private static IntPtr WindowCallback(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == 0x0010)
        {
            closeRequests++;
            if (handle == refusedClose) return IntPtr.Zero;
        }
        if (message == 0x100 || message == 0x101 || message == 0x104 || message == 0x105)
        {
            Keys key = (Keys)wParam.ToInt32();
            if (key != Keys.Menu && key != Keys.LMenu && key != Keys.RMenu && (int)key != 0xE8)
                leakedNavigation++;
        }
        if (message == 0x112 && (wParam.ToInt64() & 0xFFF0) == 0xF100) menuActivations++;
        return DefWindowProc(handle, message, wParam, lParam);
    }

    private delegate IntPtr WindowProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        internal uint Size, Style;
        internal WindowProc Procedure;
        internal int ClassExtra, WindowExtra;
        internal IntPtr Instance, Icon, Cursor, Background;
        internal string? MenuName;
        internal string ClassName;
        internal IntPtr SmallIcon;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string title, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? module);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint MapVirtualKey(uint key, uint mapType);
    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);
}
