#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>One historical figure (content/palestine/figures.yaml, written by tools/figures_to_yaml.py from
    /// ../content/figures.tsv; sourced in audits/figures_sources.md).</summary>
    public sealed class Figure
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string NameAr { get; set; } = "";
        /// <summary>YYYY or YYYY-MM; "" when unknown.</summary>
        public string Born { get; set; } = "";
        /// <summary>YYYY or YYYY-MM; "" when unknown or alive.</summary>
        public string Died { get; set; } = "";
        public List<string> Chapters { get; set; } = new List<string>();
        public string Role { get; set; } = "";
        public string RoleAr { get; set; } = "";
        public string Bio { get; set; } = "";
        public string BioAr { get; set; } = "";
        public List<string> RelatedEvents { get; set; } = new List<string>();
        public List<string> Sources { get; set; } = new List<string>();
        /// <summary>The bio tells of killing (the figure's acts or what was done to them): it opens with the content note.</summary>
        public bool Sensitive { get; set; }

        public string NameIn(bool arabic) => arabic ? NameAr : Name;
        public string RoleIn(bool arabic) => arabic ? RoleAr : Role;
        public string BioIn(bool arabic) => arabic ? BioAr : Bio;

        /// <summary>The figure's years in the UI's language: "1882–1935" ("من 1882 إلى 1935"), "Born 1962", "Died 1948", or
        /// "" when neither is known. A year-month shows its year.</summary>
        public string Years(UiText t)
        {
            string Year(string s) => s.Length >= 4 ? s.Substring(0, 4) : s;
            if (Born.Length > 0 && Died.Length > 0) return t.Format("people.years", Year(Born), Year(Died));
            if (Born.Length > 0) return t.Format("people.born", Year(Born));
            return Died.Length > 0 ? t.Format("people.died", Year(Died)) : "";
        }
    }

    /// <summary>The figures file: every figure, and those of a chapter or an event.</summary>
    public sealed class Figures
    {
        public const string FileName = "palestine/figures.yaml";
        public List<Figure> All { get; private set; } = new List<Figure>();

        sealed class Doc { public List<Figure> Figures { get; set; } = new List<Figure>(); }

        /// <summary>Parses figures.yaml (a missing or empty text gives no figures: the People screen then says so).</summary>
        public static Figures Parse(string? yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml)) return new Figures();
            var doc = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build().Deserialize<Doc>(yaml!);
            return new Figures { All = doc?.Figures ?? new List<Figure>() };
        }

        /// <summary>The figures of <paramref name="chapter"/>, in the file's order (the order of their times).</summary>
        public IReadOnlyList<Figure> Of(string chapter) => All.Where(f => f.Chapters.Contains(chapter)).ToList();

        /// <summary>The figures whose related events include <paramref name="eventId"/>.</summary>
        public IReadOnlyList<Figure> For(string eventId) => All.Where(f => f.RelatedEvents.Contains(eventId)).ToList();
    }

    /// <summary>F5 step 7: the People screen (شخصيات), opened from the left column in the family game (its button is made
    /// here) and from a card's "People in this event" line. It lists the current
    /// chapter's figures, each a card in the ornamented frame (UiFrame, no portrait: the name stands where a portrait
    /// would), with the years, the role and the bio in the UI's language, the content note (docs/CONTENT_POLICY.md rule 9)
    /// over a bio that tells of killing, and the sources as short links that open in the browser.</summary>
    public sealed class PeoplePresenter : IDisposable
    {
        public const string Screen = "people", ButtonName = "nav-people", CardClass = "person-figure";
        /// <summary>Owner, 2026-10-21: each figure's picture (Resources/People/&lt;id&gt;), above its name: a portrait in the game's
        /// ink-and-wash style where the face is well known, else a still life from the figure's story (ASSETS.yaml).</summary>
        public const string PortraitClass = "figure-portrait", PortraitFolder = "People/";
        /// <summary>Playtest 3: on the card a figure line opened the screen at (ShowFigure), until the screen opens again.</summary>
        public const string ShownClass = "person-figure-shown";
        readonly VisualElement root, screen; readonly ScrollView list; readonly Button button;
        readonly UiText text; readonly bool arabic;
        string? pendingFigure;

        /// <summary>Adds the People button to the left column, after Why (before ScreenNav is made, so it hooks the button), and
        /// fills the screen with the chapter's figures.</summary>
        public PeoplePresenter(VisualElement root, Figures figures, string chapter, UiText text)
        {
            this.root = root; this.text = text; arabic = FamilyGame.Arabic(text);
            screen = root.Q<VisualElement>("screen-" + Screen);
            list = screen.Q<ScrollView>("people-list");
            screen.Q<Label>("people-title").text = text["people.title"];
            screen.Q<Label>("people-intro").text = text["people.intro"];
            var rail = root.Q<VisualElement>(HudPresenter.RailName);
            button = new Button { name = ButtonName, text = text["hud.nav.people"] };
            button.AddToClassList("nav-button"); button.AddToClassList("rail-button");
            button.Add(new UiIcon { Glyph = "people" }.WithClass("nav-disc"));
            var why = rail.Q<Button>("nav-why");
            rail.Insert(why != null ? rail.IndexOf(why) + 1 : rail.childCount, button);
            list.Clear();
            var shown = figures.Of(chapter);
            if (shown.Count == 0) list.Add(new Label(text["people.none"]) { name = "people-none" });
            foreach (var f in shown) list.Add(Card(f));
            ScreenNav.OnOpened(screen, ScrollToTop);
        }

        /// <summary>Every opening starts at the first card (playtest: the Arabic screen opened part-way down, the first card
        /// cut off). Set now and again after the next layout, which can move it; a card's figure line (ShowFigure, called
        /// after the screen opens) still scrolls to that card.</summary>
        void ScrollToTop()
        {
            pendingFigure = null;
            list.Query(className: ShownClass).ForEach(c => c.RemoveFromClassList(ShownClass));
            list.scrollOffset = Vector2.zero;
            list.schedule.Execute(Settle);
        }

        void Settle()
        {
            var card = pendingFigure != null ? list.Q<VisualElement>("figure-" + pendingFigure) : null;
            if (card == null) { list.scrollOffset = Vector2.zero; return; }
            // Playtest 3: in the player the screen just shown has no layout yet on the first tick, so ScrollTo did nothing:
            // until the card is laid out, try again on the list's next geometry change.
            if (!(card.layout.height > 0) || !(list.contentViewport.layout.height > 0)) { list.RegisterCallback<GeometryChangedEvent>(SettleOnce); return; }
            list.ScrollTo(card);
        }

        void SettleOnce(GeometryChangedEvent _)
        {
            list.UnregisterCallback<GeometryChangedEvent>(SettleOnce);
            Settle();
        }

        VisualElement Card(Figure f)
        {
            var card = new VisualElement { name = "figure-" + f.Id };
            card.AddToClassList(CardClass);
            var picture = Resources.Load<Texture2D>(PortraitFolder + f.Id);
            if (picture != null)
            {
                var portrait = new VisualElement { name = "figure-portrait-" + f.Id };
                portrait.AddToClassList(PortraitClass);
                portrait.style.backgroundImage = picture;
                card.Add(portrait);
            }
            var name = new Label(f.NameIn(arabic)) { name = "figure-name-" + f.Id };
            name.AddToClassList("title"); name.AddToClassList("figure-name");
            card.Add(name);
            var years = f.Years(text);
            if (years.Length > 0) { var y = new Label(years); y.AddToClassList("figure-years"); card.Add(y); }
            var role = new Label(f.RoleIn(arabic)); role.AddToClassList("figure-role"); card.Add(role);
            card.Add(new VisualElement().WithClass("gold-rule"));
            if (f.Sensitive) { var note = new Label(text["people.content_note"]) { name = "figure-note-" + f.Id }; note.AddToClassList("content-note"); card.Add(note); }
            var bio = new Label(f.BioIn(arabic)); bio.AddToClassList("figure-bio"); card.Add(bio);
            card.Add(SourceLinks.Row(f.Sources, text["people.sources"], text, root));
            card.Add(new UiFrame { Filigree = true });
            return card;
        }

        /// <summary>Scrolls the screen to <paramref name="id"/>'s card (a card's figure line opens the screen there).</summary>
        public void ShowFigure(string id)
        {
            var card = list.Q<VisualElement>("figure-" + id);
            if (card == null) return;
            pendingFigure = id;
            card.AddToClassList(ShownClass);
            list.schedule.Execute(Settle);
        }

        public void Dispose()
        {
            button.RemoveFromHierarchy();
            list.UnregisterCallback<GeometryChangedEvent>(SettleOnce);
            if (screen.userData is Action) screen.userData = null;
            list.Clear();
        }
    }

    /// <summary>A row of source links: a caption, then one small button per URL showing its host, which opens the address
    /// in the browser (the event card and the People screen).</summary>
    public static class SourceLinks
    {
        public const string RowClass = "source-row", LinkClass = "source-link";

        /// <summary>A row of source links under <paramref name="caption"/>: each opens its address in the browser. Audit m3:
        /// a link names its page, not only its site (three "en.wikipedia.org" told nothing apart): "Wikipedia: Tantura", or
        /// the site and the page's last path word where two links share a site; the full address is its tooltip, shown
        /// beside it on hover and focus (HoverTip) when <paramref name="root"/> holds the tip label.</summary>
        public static VisualElement Row(IEnumerable<string> urls, string caption, UiText? text = null, VisualElement? root = null)
        {
            var row = new VisualElement();
            row.AddToClassList(RowClass);
            var label = new Label(caption); label.AddToClassList("source-caption");
            row.Add(label);
            var list = urls.ToList();
            var names = Names(list, text);
            var buttons = new List<VisualElement>();
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                // The tip: the address, after the article's full title where the link names a Wikipedia article.
                var title = WikipediaTitle(u, full: true);
                var b = new Button(() => Application.OpenURL(u)) { text = names[i], tooltip = title != null ? title + "\n" + u : u };
                b.AddToClassList(LinkClass);
                row.Add(b);
                buttons.Add(b);
            }
            if (root?.Q<Label>(HoverTip.TipName) != null)
            {
                var tips = new HoverTip(root, buttons, () => UiLanguage.Within(row) ? HoverTip.Side.Left : HoverTip.Side.Right);   // a right-to-left row reads leftwards
                row.RegisterCallback<DetachFromPanelEvent>(_ => tips.Dispose());   // a card or a list built again takes its tip down
            }
            return row;
        }

        /// <summary>Each address's link text, kept short (owner, 2026-10-22: the long names took a whole line): "Wikipedia"
        /// (sources.wikipedia in the UI's language), else its site; links that read alike are numbered. The tip keeps the
        /// article's full title and the address.</summary>
        public static IReadOnlyList<string> Names(IReadOnlyList<string> urls, UiText? text = null)
        {
            var sites = urls.Select(FamilyGame.Host).ToList();
            bool IsVideo(string site) => site == "youtube.com" || site == "youtu.be" || site == "m.youtube.com";
            int videos = sites.Count(IsVideo), video = 0;
            var names = urls.Select((u, i) =>
            {
                var title = WikipediaTitle(u);
                // The lead (2026-10-21): in the Arabic UI an English article is not cut short in a right-to-left line: it reads
                // «ويكيبيديا (بالإنجليزية)», its full title in the tip; an Arabic article keeps its title.
                if (title != null && text != null && FamilyGame.Arabic(text) && !UiDirection.HasRightToLeft(title) && text.Has("sources.wikipedia_en"))
                    return text["sources.wikipedia_en"];
                if (title != null) return text != null && text.Has("sources.wikipedia") ? text["sources.wikipedia"] : "Wikipedia";
                // Playtest 3: a video's address names no title ("youtube.com: watch" twice): "YouTube video", numbered when several.
                if (IsVideo(sites[i]) && text != null && text.Has("sources.youtube"))
                    return videos > 1 ? text.Format("sources.youtube_n", ++video) : text["sources.youtube"];
                return sites[i];
            }).ToList();
            // Never two links alike: a name that still repeats is numbered.
            for (int i = 0; i < names.Count; i++)
            {
                var same = Enumerable.Range(0, names.Count).Where(k => names[k] == names[i]).ToList();
                if (same.Count < 2) continue;
                var name = names[i];
                for (int n = 0; n < same.Count; n++) names[same[n]] = name + " (" + (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            }
            return names;
        }

        /// <summary>A Wikipedia article's title from its address (/wiki/Title or ?title=Title), spaces for underscores, at
        /// most 40 characters; null for any other address.</summary>
        public static string? WikipediaTitle(string url, bool full = false)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || !u.Host.EndsWith("wikipedia.org", StringComparison.Ordinal)) return null;
            string? raw = null;
            if (u.AbsolutePath.StartsWith("/wiki/", StringComparison.Ordinal)) raw = u.AbsolutePath.Substring(6);
            else
                foreach (var part in u.Query.TrimStart('?').Split('&'))
                    if (part.StartsWith("title=", StringComparison.Ordinal)) raw = part.Substring(6);
            if (string.IsNullOrEmpty(raw)) return null;
            var t = Uri.UnescapeDataString(raw!).Replace('_', ' ');
            return full ? t : Shorten(t);
        }

        static string Shorten(string s) => s.Length <= 40 ? s : s.Substring(0, 39).TrimEnd() + "…";
    }

    static class ElementExtensions
    {
        public static T WithClass<T>(this T e, string className) where T : VisualElement { e.AddToClassList(className); return e; }
    }
}
