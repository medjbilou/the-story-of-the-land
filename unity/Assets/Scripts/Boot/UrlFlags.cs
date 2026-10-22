#nullable enable
using System.Globalization;
using System.Text.RegularExpressions;

namespace StoryOfTheLand.Game.Boot
{
    /// <summary>Flags in the WebGL page URL's query: "?golden", "&amp;smoke=5", "?profile", "?camlog". A flag is a whole query
    /// parameter: it starts at '?' or '&amp;' and ends at '=', '&amp;', '#' or the end of the URL (the rule GameRoot's smoke
    /// flag uses), so neither a path such as ".../golden-project/..." nor "?profile-x" or "?golden.y" turns one on.</summary>
    public static class UrlFlags
    {
        public static bool Has(string? url, string name) => Regex.IsMatch(url ?? "", @"[?&]" + Regex.Escape(name) + @"(?=[=&#]|$)");

        /// <summary>The flag's integer value ("smoke=5" -> 5), or 0 when absent.</summary>
        public static int Int(string? url, string name)
        {
            var m = Regex.Match(url ?? "", @"[?&]" + Regex.Escape(name) + @"=(\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }
}
