using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectsSearch.Plugin.Util;

public static class GetAssocExec
{
    [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint AssocQueryString(
        AssocF flags,
        AssocStr str,
        string pszAssoc,
        string pszExtra,
        [Out] StringBuilder pszOut,
        ref uint pcchOut);

    [Flags]
    private enum AssocF { None = 0 }
    private enum AssocStr { Executable = 2 }

    /// <summary>
    /// Retorna o caminho do executável associado ao arquivo informado.
    /// </summary>
    public static string FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string ext = System.IO.Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
            return null;

        uint length = 0;
        AssocQueryString(AssocF.None, AssocStr.Executable, ext, null, null, ref length);

        var sb = new StringBuilder((int)length);
        uint result = AssocQueryString(AssocF.None, AssocStr.Executable, ext, null, sb, ref length);

        return result == 0 ? sb.ToString() : null;
    }
}
