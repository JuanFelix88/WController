using System.Text;

namespace WController.Util;

internal static class Text
{
    public static string RemoveDiacritics(string text)
    {
        return text.Normalize(NormalizationForm.FormD);
    }
}
