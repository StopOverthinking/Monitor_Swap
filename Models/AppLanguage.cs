using System.Globalization;

namespace MonitorSwap.Models
{
    internal enum AppLanguage
    {
        English,
        Korean
    }

    internal static class AppLanguageExtensions
    {
        public static string ToCode(this AppLanguage language)
        {
            return language == AppLanguage.Korean ? "ko" : "en";
        }

        public static AppLanguage FromCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return AppLanguage.English;
            }

            switch (code.Trim().ToLowerInvariant())
            {
                case "ko":
                case "kr":
                case "korean":
                    return AppLanguage.Korean;
                default:
                    return AppLanguage.English;
            }
        }

        public static AppLanguage FromCulture(CultureInfo culture)
        {
            if (culture == null)
            {
                return AppLanguage.English;
            }

            return string.Equals(culture.TwoLetterISOLanguageName, "ko", System.StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Korean
                : AppLanguage.English;
        }
    }
}
