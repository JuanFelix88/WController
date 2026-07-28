using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WController.Util;

public static class WinHelper
{
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Obtém o caminho completo do executável associado ao WindowItem.
    /// </summary>
    /// <param name="windowItem">O item da janela</param>
    /// <returns>O caminho completo do executável ou string vazia se não for possível obter</returns>
    public static string GetPathFrom(MainForm.WindowItem windowItem)
        => windowItem is null ? string.Empty : GetPathFromHandle(windowItem.Handle);

    /// <summary>
    /// Obtém o caminho completo do executável associado ao handle da janela.
    /// </summary>
    /// <param name="hWnd">O handle da janela</param>
    /// <returns>O caminho completo do executável ou string vazia se não for possível obter</returns>
    public static string GetPathFromHandle(IntPtr hWnd)
        => TryGetProcessPath(hWnd) ?? string.Empty;

    private static string? TryGetProcessPath(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return null;

        try
        {
            GetWindowThreadProcessId(hWnd, out var processId);
            if (processId == 0) return null;
            using var process = Process.GetProcessById((int)processId);
            return process.MainModule?.FileName;
        }
        catch { return null; }
    }

    public static string GetSoftwareNameFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        try
        {
            return Path.GetFileNameWithoutExtension(path);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
