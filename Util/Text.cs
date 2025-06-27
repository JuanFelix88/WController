using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WController.Util
{
    internal static class Text
    {
        public static string RemoveDiacritics(string text)
        {
            return text.Normalize(NormalizationForm.FormD);
        }
    }
}
