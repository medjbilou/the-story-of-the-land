#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The credits text (content/palestine/credits.yaml, written by tools/credits_to_yaml.py from docs/credits.md):
    /// its sections in English and in Arabic.</summary>
    public sealed class Credits
    {
        public const string FileName = "palestine/credits.yaml";

        public sealed class Block
        {
            /// <summary>text (a paragraph), item (a list item) or quote (a notice shown as written, left to right).</summary>
            public string Kind { get; set; } = "";
            /// <summary>The block's text; &lt;b&gt; is the UI's rich text.</summary>
            public string Text { get; set; } = "";
        }

        public sealed class Section
        {
            public string Title { get; set; } = "";
            public List<Block> Blocks { get; set; } = new List<Block>();
        }

        public List<Section> En { get; set; } = new List<Section>();
        public List<Section> Ar { get; set; } = new List<Section>();

        /// <summary>The sections in the UI's language.</summary>
        public IReadOnlyList<Section> In(bool arabic) => arabic ? Ar : En;

        /// <summary>Parses credits.yaml (a missing or empty text gives no sections).</summary>
        public static Credits Parse(string? yaml) => string.IsNullOrWhiteSpace(yaml) ? new Credits()
            : new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build().Deserialize<Credits>(yaml!) ?? new Credits();
    }

    /// <summary>F5 step 9: the Credits screen, opened from Settings' "Credits and licences" row (it has no button in the left
    /// column). It shows docs/credits.md in the UI's language: each section's heading in gold, its paragraphs and list items
    /// wrapped, and the legal notices (MIT, Copernicus) as written, left to right, in a quieter inset, as credits.md asks.
    /// Closing it (its X, Escape, or Settings' gear) goes back to the map, as every screen does.</summary>
    public sealed class CreditsPresenter : IDisposable
    {
        public const string Screen = "credits";
        /// <summary>An English credits line with Arabic in it (StoryOfTheLand.uss: the Arabic face).</summary>
        public const string MixedClass = "credits-mixed";
        readonly VisualElement screen; readonly ScrollView body; readonly Button open; readonly Action onOpen;

        public CreditsPresenter(VisualElement root, Credits credits, UiText text, ScreenNav nav)
        {
            screen = root.Q<VisualElement>("screen-" + Screen);
            body = screen.Q<ScrollView>("credits-body");
            screen.Q<Label>("credits-title").text = text["credits.title"];
            var settings = root.Q<VisualElement>("screen-settings");
            settings.Q<Label>("settings-credits-label").text = text["settings.credits"];
            open = settings.Q<Button>("settings-credits");
            open.text = text["settings.credits_open"];
            onOpen = () => nav.Open(Screen);
            open.clicked += onOpen;
            // Audit m7: the credits open at their top, not where they were last left.
            ScreenNav.OnOpened(screen, () => body.scrollOffset = UnityEngine.Vector2.zero);
            body.Clear();
            bool arabic = FamilyGame.Arabic(text);
            foreach (var s in credits.In(arabic))
            {
                var heading = new Label(s.Title);
                heading.AddToClassList("section-title"); heading.AddToClassList("credits-heading");
                body.Add(heading);
                foreach (var b in s.Blocks)
                {
                    var words = arabic && b.Kind != "quote" ? KeepLatinTogether(b.Text) : b.Text;
                    var l = new Label(b.Kind == "item" ? "•  " + words : words);
                    l.AddToClassList(b.Kind == "quote" ? "credits-quote" : b.Kind == "item" ? "credits-item" : "credits-text");
                    // A notice is legal text in its original language: left to right in either UI. An Arabic block is right to
                    // left even when it opens with a Latin name ("EB Garamond: ..."), which UiDirection would read as English.
                    // Audit M1: an English line that quotes Arabic («فلسطين .. حكاية الأرض») is set in the Arabic face, which has
                    // Latin too, through the advanced generator: EB Garamond has no Arabic and drew boxes.
                    if (!arabic && UiDirection.HasRightToLeft(words)) l.AddToClassList(MixedClass);
                    if (arabic)
                    {
                        l.AddToClassList(UiDirection.OwnDirectionClass);
                        l.languageDirection = b.Kind == "quote" ? LanguageDirection.LTR : LanguageDirection.RTL;
                    }
                    body.Add(l);
                }
            }
        }

        // A run of Latin text in an Arabic line ("The Story of the Land", "(CC BY 4.0)", a URL): ASCII letters, digits and the
        // punctuation inside names and addresses, from a letter (or an opening bracket) to a letter, digit, full stop or
        // closing bracket.
        static readonly System.Text.RegularExpressions.Regex LatinRun = new System.Text.RegularExpressions.Regex(
            @"\(?[A-Za-z][A-Za-z0-9.,:/_'&()\-]*(?: [A-Za-z0-9(][A-Za-z0-9.,:/_'&()\-]*)*", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>credits.md's own note: a Latin name inside the Arabic text keeps its punctuation in place. Each Latin run is
        /// set between two left-to-right marks, as UiText.Format sets a Latin value in an Arabic line (#543), and a short one
        /// (a licence's name, up to 30 characters) keeps its words on one line (no-break spaces, and a word joiner after a
        /// hyphen), so "(CC BY-SA 4.0)" never breaks inside its brackets.</summary>
        public static string KeepLatinTogether(string arabic) => LatinRun.Replace(arabic, m =>
        {
            var run = m.Value.TrimEnd(',', ':', '.');   // the Arabic sentence's own punctuation stays outside
            var rest = m.Value.Substring(run.Length);
            if (run.Length <= 30) run = run.Replace(' ', '\u00A0').Replace("-", "-\u2060");   // and a word joiner after a hyphen: "CC BY-SA"
            return UiText.LeftToRightMark + run + UiText.LeftToRightMark + rest;
        });

        public void Dispose()
        {
            open.clicked -= onOpen;
            body.Clear();
        }
    }
}
