#nullable enable
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The UI's language (#193): English unless Arabic is asked for, "?lang=ar" in the page URL on WebGL or
    /// "-lang ar" on the desktop command line, or chosen in Settings (#1036, kept in settings.txt). The content text
    /// (events, names) is English only until #55. Any other value, or a bare flag, is English.</summary>
    public static class UiLanguage
    {
        public const string Flag = "lang";
        public const string English = "en", Arabic = "ar";
        /// <summary>On the UI root while the UI is Arabic: the Arabic face, the advanced text generator and the mirrored rows
        /// (StoryOfTheLand.uss).</summary>
        public const string ArabicClass = "lang-ar";

        // A whole query parameter with a whole value (UrlFlags' rule): "?lang=arx" and "?slang=ar" are not the flag.
        static readonly Regex ArabicQuery = new Regex(@"[?&]" + Flag + "=" + Arabic + @"(?=&|#|$)", RegexOptions.CultureInvariant);

        // The flag with any value, or none: the address names a language for this visit.
        static readonly Regex AnyQuery = new Regex(@"[?&]" + Flag + @"(?:=[^&#]*)?(?=&|#|$)", RegexOptions.CultureInvariant);

        /// <summary>The language at start (#1036): a flag in the address or on the command line (the store page's "?lang=ar"
        /// link) holds for that visit, over the choice <paramref name="stored"/> in Settings; with neither, English.</summary>
        public static string AtStart(string? stored, string? url, string[] args, bool webgl)
        {
            bool flag = webgl ? AnyQuery.IsMatch(url ?? "") : Array.IndexOf(args, "-" + Flag) >= 0;
            return flag ? Requested(url, args, webgl) : stored == Arabic ? Arabic : English;
        }

        public static string Requested(string? url, string[] args, bool webgl)
        {
            if (webgl) return ArabicQuery.IsMatch(url ?? "") ? Arabic : English;
            var i = Array.IndexOf(args, "-" + Flag);
            return i >= 0 && i + 1 < args.Length && args[i + 1] == Arabic ? Arabic : English;
        }

        /// <summary>Whether <paramref name="e"/> is inside the Arabic UI (an ancestor carries <see cref="ArabicClass"/>): code
        /// that paints a sign at one side of an element puts it on the other side there.</summary>
        public static bool Within(UnityEngine.UIElements.VisualElement? e)
        {
            for (; e != null; e = e.parent) if (e.ClassListContains(ArabicClass)) return true;
            return false;
        }

        public static string AtStart(string? stored) =>
            AtStart(stored, Application.absoluteURL, Environment.GetCommandLineArgs(), Application.platform == RuntimePlatform.WebGLPlayer);
    }
}
