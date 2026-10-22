#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using StoryOfTheLand.Sim;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>F5 step 3: a new family game is chosen in two steps over the start screen (or over the game, from the top
    /// bar's New game). First the chapter: the seven chapters in time order, each with its title in the UI's language
    /// (chapter.&lt;id&gt;, from the chapter file's header) and its years. Then the family's village: the chapter's places
    /// (Content.Places, FamilyGame.FamilyPlace) in the UI's language, grouped by region after 1948 (inside the 1948 lines,
    /// the West Bank, Gaza; place_regions.tsv), with a search field that filters as it is typed (FamilyGame.Matches, both
    /// names). Picking a village starts the game (<c>start</c>); Back (or Escape) goes back a step, and from the chapters
    /// closes the picker. While it shows, the root has OpenClass: StoryOfTheLand.uss shows it over everything but the toast, and
    /// ScreenNav.DisableCovered takes the HUD under it out of Tab's reach.</summary>
    public sealed class FamilyPickerPresenter : IDisposable
    {
        public const string OpenClass = "picker-open", ElementName = "family-picker";
        public const string ChapterClass = "picker-chapter", VillageClass = "picker-village", RegionClass = "picker-region";

        readonly VisualElement hudRoot, picker, chapters, villages; readonly ScrollView list;
        readonly Label title, intro, count; readonly Button back; readonly TextField search;
        readonly UiText text; readonly bool arabic;
        readonly Func<string, Content> load; readonly Action<string, string> start;
        readonly Action onBack; readonly EventCallback<NavigationCancelEvent> onCancel;
        readonly EventCallback<ChangeEvent<string>> onSearch;
        Content? content;
        /// <summary>The picked chapter's places' names on the picker (FamilyGame.PickerNames).</summary>
        Dictionary<string, string> names = new Dictionary<string, string>();

        /// <summary>The chapter whose villages show, or null on the chapter step (or closed).</summary>
        public string? Chapter { get; private set; }

        /// <param name="load">A chapter's content (GameInputs.Chapter), asked when the chapter is picked.</param>
        /// <param name="start">Starts the family game in a chapter and village (GameRoot.StartFamily).</param>
        public FamilyPickerPresenter(VisualElement root, UiText text, Func<string, Content> load, Action<string, string> start)
        {
            this.text = text; this.load = load; this.start = start; arabic = FamilyGame.Arabic(text);
            hudRoot = root.Q("root") ?? root;
            picker = root.Q<VisualElement>(ElementName);
            title = picker.Q<Label>("picker-title"); intro = picker.Q<Label>("picker-intro"); count = picker.Q<Label>("picker-count");
            chapters = picker.Q<VisualElement>("picker-chapters"); villages = picker.Q<VisualElement>("picker-villages");
            list = picker.Q<ScrollView>("picker-list");
            picker.Q<Label>("picker-search-label").text = text["picker.search"];
            // Made here, not in Game.uxml: its text box takes the UI's face from .lang-ar like every other text (StoryOfTheLand.uss).
            search = new TextField { name = "picker-search" };
            search.AddToClassList("picker-search");
            picker.Q<VisualElement>("picker-search-row").Insert(1, search);
            onSearch = e => Filter(e.newValue);
            search.RegisterValueChangedCallback(onSearch);
            back = picker.Q<Button>("picker-back");
            back.text = text["picker.back"];
            onBack = Back;
            back.clicked += onBack;
            onCancel = e => { if (IsOpen) { Back(); e.StopPropagation(); } };
            picker.RegisterCallback(onCancel);
            chapters.Clear();
            foreach (var (id, from, to) in FamilyGame.Chapters)
            {
                var chapter = id;
                var b = new Button(() => PickChapter(chapter)) { name = "chapter-" + id };
                b.AddToClassList(ChapterClass);
                var t = new Label(text["chapter." + id]) { name = "chapter-title-" + id, pickingMode = PickingMode.Ignore };
                t.AddToClassList("picker-chapter-title");
                var y = new Label(FamilyGame.Years(text, from, to)) { name = "chapter-years-" + id, pickingMode = PickingMode.Ignore };
                y.AddToClassList("picker-chapter-years");
                b.Add(t); b.Add(y);
                chapters.Add(b);
            }
            Hide();
        }

        public bool IsOpen => hudRoot.ClassListContains(OpenClass);

        /// <summary>Opens the picker on its first step, the chapters, with the focus on the first one.</summary>
        public void Open()
        {
            hudRoot.AddToClassList(OpenClass);
            ScreenNav.DisableCovered(hudRoot);
            ShowChapters();
        }

        void ShowChapters()
        {
            Chapter = null; content = null;
            title.text = text["picker.chapter_title"]; intro.text = text["picker.chapter_intro"];
            chapters.style.display = DisplayStyle.Flex; villages.style.display = DisplayStyle.None;
            FocusReturn.To(chapters.Q<Button>());
        }

        /// <summary>The second step: the villages of <paramref name="chapter"/>.</summary>
        public void PickChapter(string chapter)
        {
            content = load(chapter);
            names = FamilyGame.PickerNames(content, arabic, text);
            Chapter = chapter;
            title.text = text["picker.village_title"];
            intro.text = text.Format("picker.village_chapter", text["chapter." + chapter]);
            chapters.style.display = DisplayStyle.None; villages.style.display = DisplayStyle.Flex;
            search.SetValueWithoutNotify("");
            Filter("");
            FocusReturn.To(search);
        }

        /// <summary>Lists the villages whose name matches <paramref name="query"/>, under their regions' headings (a region
        /// with none is left out), and says how many there are.</summary>
        public void Filter(string query)
        {
            list.Clear();
            if (content == null) return;
            int n = 0;
            foreach (var (region, places) in FamilyGame.PlacesByRegion(content, arabic))
            {
                var shown = places.Where(p => FamilyGame.Matches(p, query)).ToList();
                if (shown.Count == 0) continue;
                var heading = new Label(text["picker.region." + region]) { name = "region-" + region };
                heading.AddToClassList("section-title"); heading.AddToClassList(RegionClass);
                list.Add(heading);
                var grid = new VisualElement();
                grid.AddToClassList("picker-grid");
                foreach (var p in shown)
                {
                    var id = p.Id;
                    var b = new Button(() => PickVillage(id)) { name = "village-" + id, text = names.TryGetValue(p.Id, out var shownName) ? shownName : FamilyGame.Name(p, arabic) };
                    b.AddToClassList(VillageClass);
                    grid.Add(b);
                }
                list.Add(grid);
                n += shown.Count;
            }
            if (n == 0) list.Add(new Label(text["picker.none"]) { name = "picker-none" });
            count.text = text.Plural("picker.count", n);
        }

        /// <summary>Starts the family game in the chosen chapter and <paramref name="village"/> and closes the picker.</summary>
        public void PickVillage(string village)
        {
            if (Chapter == null) return;
            var chapter = Chapter;
            Hide();
            start(chapter, village);
        }

        /// <summary>Back: from the villages to the chapters; from the chapters, closes the picker.</summary>
        public void Back()
        {
            if (Chapter != null) ShowChapters();
            else Hide();
        }

        void Hide()
        {
            Chapter = null; content = null;
            list.Clear();
            hudRoot.RemoveFromClassList(OpenClass);
            ScreenNav.DisableCovered(hudRoot);
        }

        public void Dispose()
        {
            Hide();
            back.clicked -= onBack;
            search.UnregisterValueChangedCallback(onSearch);
            search.RemoveFromHierarchy();
            picker.UnregisterCallback(onCancel);
            chapters.Clear();
        }
    }
}
