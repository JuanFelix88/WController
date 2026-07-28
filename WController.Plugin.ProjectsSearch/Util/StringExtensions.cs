using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WController.Plugin.ProjectsSearch.Util;
internal static class StringExtensions
{
    private static string RemoveDiacritics(string str)
    {
        var normalizedString = str.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }
        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string ToLowerSafe(this string str)
    {
        if (str is null) return string.Empty;

        var lower = str.ToLowerInvariant();

        return RemoveDiacritics(lower);
    }


}
