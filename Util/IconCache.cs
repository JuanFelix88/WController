using System.Collections.Generic;
using System.Drawing;

namespace WController.Util;

internal static class IconCache
{
    private static readonly Dictionary<string, Image> _cache = new Dictionary<string, Image>();

    public static Image? GetIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        if (_cache.TryGetValue(iconPath, out Image cached))
            return cached;

        Image? resized = Resizer.ResizeImageFromFile(iconPath, 24, 24);
        if (resized is null)
            return null;

        _cache[iconPath] = resized;
        return resized;
    }

    public static void Clear()
    {
        foreach (var img in _cache.Values)
            img.Dispose();

        _cache.Clear();
    }
}
