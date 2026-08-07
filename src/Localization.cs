using System.Globalization;

namespace WheelFix
{
    internal static class L
    {
        private static readonly bool UseItalian =
            string.Equals(
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                "it",
                System.StringComparison.OrdinalIgnoreCase);

        public static string Text(string english, string italian)
        {
            return UseItalian ? italian : english;
        }
    }
}
