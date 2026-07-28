using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WController.Plugin.ProjectsSearch.Util;
internal static class DirIgnore
{
    public static bool IsIgnored(string path)
    {
        string dirName = System.IO.Path.GetFileName(path).ToLowerSafe();
        if (dirName == "bin") return true;
        if (dirName == "obj") return true;
        if (dirName == "dist") return true;
        if (dirName == "out") return true;
        if (dirName == ".next") return true;
        if (dirName == ".git") return true;
        if (dirName == ".vs") return true;
        if (dirName == "node_modules") return true;
        if (dirName == "packages") return true;
        return false;
    }
}
