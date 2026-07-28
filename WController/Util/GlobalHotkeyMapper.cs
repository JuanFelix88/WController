using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace WController.Util;

public static class GlobalHotkeyMapper
{
    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    static Dictionary<int, Action> handlers = new Dictionary<int, Action>();
    static int currentId;
    static MessageWindow window;

    static GlobalHotkeyMapper()
    {
        window = new MessageWindow();
        window.HotkeyPressed += (id) => { if (handlers.ContainsKey(id)) handlers[id](); };
    }

    public static void RemoveAllListeners()
    {
        foreach (var id in handlers.Keys)
            UnregisterHotKey(window.Handle, id);
        handlers.Clear();
    }

    public static void RemoveListener(Keys keys)
    {
        foreach (var pair in handlers)
            UnregisterHotKey(window.Handle, pair.Key);
        handlers.Clear();
    }

    public static void AddListenerFor(Keys keys, Action action)
    {
        uint modifiers = 0;
        if ((keys & Keys.Alt) == Keys.Alt) modifiers |= 1;
        if ((keys & Keys.Control) == Keys.Control) modifiers |= 2;
        if ((keys & Keys.Shift) == Keys.Shift) modifiers |= 4;
        if ((keys & Keys.LWin) == Keys.LWin || (keys & Keys.RWin) == Keys.RWin) modifiers |= 8;
        Keys key = keys & ~Keys.Modifiers;
        int id = ++currentId;
        RegisterHotKey(window.Handle, id, modifiers, (uint)key);
        handlers[id] = action;
    }

    class MessageWindow : NativeWindow, IDisposable
    {
        const int WM_HOTKEY = 0x0312;
        public event Action<int>? HotkeyPressed;
        public MessageWindow() { CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY) HotkeyPressed?.Invoke(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
        public void Dispose()
        {
            for (int i = 1; i <= currentId; i++) UnregisterHotKey(Handle, i);
            DestroyHandle();
        }
    }
}
