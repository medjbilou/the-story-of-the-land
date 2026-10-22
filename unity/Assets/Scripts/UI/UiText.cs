#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>UI strings kept outside code (unity/Assets/UI/strings.en.yaml): key -> string.Format pattern.</summary>
    public sealed class UiText
    {
        /// <summary>U+200E LEFT-TO-RIGHT MARK: invisible, takes no room, and reads as a left-to-right letter to the bidi rules.</summary>
        public const char LeftToRightMark = '‎';

        /// <summary><paramref name="s"/> between two left-to-right marks: a number or a sign that must keep its order in a
        /// right-to-left line ("47%", not "%47").</summary>
        public static string LeftToRight(string s) => LeftToRightMark + s + LeftToRightMark;

        // A format item ("{0}", "{1:N0}", "{2,-8}") or an escaped brace, as string.Format reads them.
        static readonly Regex FormatItem = new Regex(@"\{\{|\}\}|\{(\d+)((?:,\s*-?\d+)?(?::[^{}]*)?)\}", RegexOptions.CultureInvariant);

        static readonly char[] Braces = { '{', '}' };

        readonly Dictionary<string, string> map;
        readonly Dictionary<string, bool> arabic = new Dictionary<string, bool>();
        readonly string file;
        UiText(Dictionary<string, string> map, string file) { this.map = map; this.file = file; }

        /// <summary>Parses the strings file. Throws InvalidOperationException naming the file when it is empty or not a map.</summary>
        public static UiText Parse(string yaml, string file = "strings.en.yaml")
        {
            Dictionary<string, string> m;
            try { m = new DeserializerBuilder().WithDuplicateKeyChecking().Build().Deserialize<Dictionary<string, string>>(yaml ?? ""); }
            catch (YamlDotNet.Core.YamlException e) { throw new InvalidOperationException($"{file}: {e.Message}", e); }
            if (m == null || m.Count == 0) throw new InvalidOperationException($"{file}: no strings");
            return new UiText(m, file);
        }

        /// <summary>True when the strings have <paramref name="key"/> (a lookup that must not throw, as GameStart's fallback).</summary>
        public bool Has(string key) => map.ContainsKey(key);

        public string this[string key] =>
            map.TryGetValue(key, out var v) ? v : throw new KeyNotFoundException($"{file} has no key '{key}'");

        /// <summary>string.Format over the key's pattern. In an Arabic pattern (its own first letter, the format items left
        /// out, is Arabic) each value with no right-to-left letter in it is put between two left-to-right marks (#543): in a
        /// right-to-left line the bidi rules would otherwise draw "-$4.2m" as "$4.2m-" and split "15 May 1948" into "May 1948
        /// 15". A value with Arabic in it is left alone: an Arabic word (a seat unit) between marks would make the space next
        /// to it left to right, and a value Format has already made from an Arabic pattern has its own values marked. Every
        /// other pattern, so all of English, gives string.Format's text unchanged.</summary>
        public string Format(string key, params object[] args)
        {
            var pattern = this[key];
            if (!IsArabic(key, pattern)) return string.Format(CultureInfo.InvariantCulture, pattern, args);
            // A brace left once the format items are taken out (a lone "}", "{x}") makes string.Format throw; so does this.
            if (FormatItem.Replace(pattern, "").IndexOfAny(Braces) >= 0)
                throw new FormatException($"{file}: '{key}' is not a valid format pattern: {pattern}");
            return FormatItem.Replace(pattern, m =>
            {
                if (!m.Groups[1].Success) return m.Value.Substring(1);              // "{{" or "}}"
                var value = string.Format(CultureInfo.InvariantCulture, "{0" + m.Groups[2].Value + "}", Arg(args, m.Groups[1].Value));
                return value.Length == 0 || UiDirection.HasRightToLeft(value) ? value : LeftToRightMark + value + LeftToRightMark;
            });
        }

        /// <summary>Format over the plural form of <paramref name="key"/> for the count <paramref name="n"/>, by the Arabic
        /// (CLDR) rule: key.one for 1, key.two for 2, key.few when n % 100 is 3-10, key.many when it is 11-99, key.other
        /// otherwise (0, 100-102, 200...). A form the file does not have falls back to the base key, so English needs only
        /// key and key.one. The arguments are <paramref name="args"/>, or the count alone as {0} when none are given; the
        /// .one and .two forms may leave the count out ("شهرين", not "2 شهرين").</summary>
        public string Plural(string key, long n, params object[] args)
        {
            var form = PluralKey(key, n);
            return Format(map.ContainsKey(form) ? form : key, args.Length == 0 ? new object[] { n } : args);
        }

        static string PluralKey(string key, long n)
        {
            n = Math.Abs(n);
            if (n == 1) return key + ".one";
            if (n == 2) return key + ".two";
            var r = n % 100;
            return key + (r >= 3 && r <= 10 ? ".few" : r >= 11 ? ".many" : ".other");
        }

        static object? Arg(object[] args, string index)
        {
            int i = int.Parse(index, CultureInfo.InvariantCulture);
            if (args == null || i >= args.Length) throw new FormatException("Index (zero based) must be greater than or equal to zero and less than the size of the argument list.");
            return args[i];
        }

        bool IsArabic(string key, string pattern)
        {
            if (!arabic.TryGetValue(key, out var a)) arabic[key] = a = StartsArabic(FormatItem.Replace(pattern, ""));
            return a;
        }

        /// <summary>Whether the first letter of <paramref name="s"/> is Arabic (U+0600-U+06FF, U+0750-U+077F).</summary>
        static bool StartsArabic(string s)
        {
            foreach (var c in s)
                if (char.IsLetter(c)) return c >= '\u0600' && c <= '\u06FF' || c >= '\u0750' && c <= '\u077F';
            return false;
        }
    }
}
