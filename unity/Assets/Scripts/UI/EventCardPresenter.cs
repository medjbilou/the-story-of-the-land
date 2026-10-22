#nullable enable
using System.Collections.Generic;
using System.Linq;
using StoryOfTheLand.Sim;
using UnityEngine.UIElements;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>One clip of an event (content/palestine/footage.yaml, written by tools/footage_to_yaml.py from
    /// ../content/footage.tsv). No video ships: the card opens the publisher's page in the browser, after the content note.</summary>
    public sealed class Clip
    {
        public string EventId { get; set; } = "";
        public string Url { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string Title { get; set; } = "";
        public string TitleAr { get; set; } = "";
        public string Date { get; set; } = "";
        public int DurationS { get; set; }
        public string Description { get; set; } = "";
        public string DescriptionAr { get; set; } = "";
        /// <summary>page (a publisher's page: the browser), or video or photo (a direct Archive Genocide file: FootageViewer).</summary>
        public string Kind { get; set; } = "page";
        /// <summary>A video or photo's file, its poster, its Archive Genocide entry id and its original post ("" when none).</summary>
        public string MediaUrl { get; set; } = "";
        public string PosterUrl { get; set; } = "";
        public string AgEntryId { get; set; } = "";
        public string PostUrl { get; set; } = "";
        /// <summary>The owner's sign-off (docs/footage-review.md): who watched the clip, and the day.</summary>
        public string CheckedBy { get; set; } = "";
        public string CheckedOn { get; set; } = "";

        public string TitleIn(bool arabic) => arabic ? TitleAr : Title;
    }

    /// <summary>The footage file: the clips the owner watched and signed off (owner, 2026-10-22: "Map + checked clips").</summary>
    public sealed class Footage
    {
        public const string FileName = "palestine/footage.yaml";
        public List<Clip> All { get; private set; } = new List<Clip>();

        sealed class Doc { public List<Clip> Footage { get; set; } = new List<Clip>(); }

        /// <summary>Parses footage.yaml (a missing or empty text gives no clips: no card then shows the button).</summary>
        public static Footage Parse(string? yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml)) return new Footage();
            var doc = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build().Deserialize<Doc>(yaml!);
            return new Footage { All = doc?.Footage ?? new List<Clip>() };
        }

        /// <summary>The clips of <paramref name="eventId"/> that carry a whole sign-off, in the file's order. The converter writes
        /// no other, and a hand-edited file still never shows an unchecked clip.</summary>
        public IReadOnlyList<Clip> For(string eventId) =>
            All.Where(c => c.EventId == eventId && c.CheckedBy.Length > 0 && c.CheckedOn.Length > 0).ToList();
    }

    /// <summary>The decision card, in the middle of the screen under the bar (decision cards, 2026-10-13): the first pending
    /// event the family has not yet answered. Its title, text and choices are in the UI's language (title_ar, text_ar and the
    /// choices' Arabic labels in Arabic), filled for the family's village (Family.Fill); a card tagged sensitive opens with
    /// the content note (docs/CONTENT_POLICY.md rule 9) before its text; the people it names (figures.yaml related_events)
    /// are links to the People screen, its signed-off clips "Watch footage" buttons (RenderFootage), and its sources short
    /// links to the browser. No photograph, portrait or price: a
    /// family answers for free. After a choice the card shows what followed (outcome_ar / outcome) under the choice made,
    /// until Continue; a choice with no outcome text goes straight to the next card. A choice is an order: it takes effect
    /// at End Turn.
    /// While it shows, the root has OpenClass: a backdrop dims the map and takes the pointer, and ScreenNav.DisableCovered
    /// takes the panels under it out of reach. Hide collapses it to the "Decision waiting" button, which brings it back; a
    /// new event opens it again by itself. A new event puts the keyboard focus on the card itself, not on an
    /// option, so no choice is lit as if recommended; Tab then reaches the options in order.</summary>
    public sealed class EventCardPresenter : System.IDisposable
    {
        /// <summary>The class on each option button (StoryOfTheLand.uss).</summary>
        public const string OptionClass = "event-option";
        /// <summary>On the "root" element while the card shows (not collapsed): StoryOfTheLand.uss shows the backdrop.</summary>
        public const string OpenClass = "event-open";
        /// <summary>On the "root" element while the card shows and no start screen or screen covers it (set by
        /// ScreenNav.DisableCovered): the panels under the backdrop are disabled and the toast moves clear of the card.</summary>
        public const string ModalClass = "event-modal";

        readonly VisualElement card; readonly SimBridge bridge; readonly UiText text;
        /// <summary>Playtest 2: the card foot's row that says what the choice under the pointer or the focus affects.</summary>
        readonly VisualElement hint;
        /// <summary>A screenshot run (HiddenInCaptures): the card never shows, so the captures show the map.</summary>
        readonly bool hiddenInCapture;
        readonly VisualElement layer, hudRoot;
        readonly Button hide, reopen;
        readonly System.Action onHide, onReopen, onUncovered;
        readonly EventCallback<PointerDownEvent> onHidePointer;
        readonly EventCallback<NavigationSubmitEvent> onHideSubmit;
        readonly EventCallback<NavigationCancelEvent> onCancel;
        /// <summary>The last Hide came from the pointer (PointerBlur's rule: no focus fill left behind for a mouse click).</summary>
        bool hideByPointer;
        /// <summary>The session the card last showed: a new one (New Game, a load) forgets what was collapsed or focused.</summary>
        GameSession? session;
        readonly PressSource presses;
        /// <summary>The event the player collapsed the card on (Hide), or null; a different event opens the card again.</summary>
        string? collapsedFor;
        /// <summary>The event whose first option last took the focus, so an event already open never takes it again.</summary>
        string? focusedFor;
        /// <summary>F5: the family's figures that an event names (Figures.For), and what a figure's link does (opens the
        /// People screen at it); null outside the family game.</summary>
        readonly System.Func<string, System.Collections.Generic.IReadOnlyList<Figure>>? figuresFor;
        readonly System.Action<string>? openFigure;
        /// <summary>F5: the choice whose outcome the card shows now, until Continue.</summary>
        (GameEvent e, EventOption o)? outcome;
        readonly Label chosen, note; readonly VisualElement figures, sources;
        /// <summary>The owner's decision (2026-10-22): the event's signed-off clips (Footage.For), the row of their "Watch
        /// footage" buttons under the people, the clip whose content note is up (Open / Cancel), and what Open does
        /// (Application.OpenURL: in the web player a new tab).</summary>
        readonly System.Func<string, IReadOnlyList<Clip>>? clipsFor;
        readonly System.Action<string> openUrl;
        readonly VisualElement footage;
        Clip? asking;
        /// <summary>The in-game viewer of a direct video or photo file (desktop players: FootageViewer.Plays).</summary>
        readonly FootageViewer viewer;
        static bool Web => UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WebGLPlayer;

        /// <param name="underStartScreen">The start screen goes up over the card once it is built (GameRoot.Begin): the card
        /// takes the focus when the screen closes.</param>
        /// <param name="hiddenInCapture">A screenshot run (HiddenInCaptures): neither the card, its scrim nor "Decision
        /// waiting" ever shows. Shown still names the event; the sim is untouched.</param>
        public EventCardPresenter(VisualElement root, SimBridge bridge, UiText text, bool underStartScreen = false, bool hiddenInCapture = false,
            System.Func<string, System.Collections.Generic.IReadOnlyList<Figure>>? figuresFor = null, System.Action<string>? openFigure = null,
            System.Func<string, IReadOnlyList<Clip>>? clipsFor = null, System.Action<string>? openUrl = null)
        {
            this.figuresFor = figuresFor; this.openFigure = openFigure;
            this.clipsFor = clipsFor; this.openUrl = openUrl ?? UnityEngine.Application.OpenURL;
            this.hiddenInCapture = hiddenInCapture;
            card = root.Q<VisualElement>("event-card"); this.bridge = bridge; this.text = text;
            chosen = card.Q<Label>("event-chosen"); note = card.Q<Label>("event-note");
            figures = card.Q<VisualElement>("event-figures"); sources = card.Q<VisualElement>("event-sources");
            footage = new VisualElement { name = "event-footage" };
            footage.AddToClassList("figure-row");
            figures.parent.Insert(figures.parent.IndexOf(figures) + 1, footage);
            hint = card.Q<VisualElement>("event-hint");
            layer = card.parent; hudRoot = root.Q("root") ?? root;
            viewer = new FootageViewer(layer, text, this.openUrl) { Closed = () => { if (Shown != null && !Collapsed) card.Focus(); } };
            hide = card.Q<Button>("event-hide"); reopen = layer.Q<Button>("event-reopen");
            hide.text = text["event.hide"]; reopen.text = text["event.reopen"];
            onHide = Collapse; onReopen = Reopen;
            onUncovered = () =>
            {
                // The start screen or a screen has closed: the open card takes the focus it could not take under it.
                focusedFor = null; if (!hiddenInCapture && Shown != null && !Collapsed) FocusCard(Shown.Id, force: true);
            };
            onHidePointer = _ => hideByPointer = true;
            onHideSubmit = _ => hideByPointer = false;
            onCancel = e => { if (Escape()) e.StopPropagation(); };
            hide.clicked += onHide; reopen.clicked += onReopen;
            card.focusable = true; card.tabIndex = 0;   // the card takes the focus itself (FocusCard); StoryOfTheLand.uss .event-card:focus
            // Hide with the pointer leaves no focus fill on "Decision waiting" (PointerBlur's rule); with the keyboard it moves there.
            hide.RegisterCallback<PointerDownEvent>(onHidePointer, TrickleDown.TrickleDown);
            hide.RegisterCallback<NavigationSubmitEvent>(onHideSubmit, TrickleDown.TrickleDown);
            // Escape on the open card is Hide (GameRoot also polls the key when nothing has the focus: Escape()).
            card.RegisterCallback<NavigationCancelEvent>(onCancel);
            // A start screen or a screen that covered the layer has closed (ScreenNav.DisableCovered): the open card takes
            // the focus it could not take under it.
            ScreenNav.OnUncovered(layer, onUncovered);
            presses = new PressSource(card.Q<VisualElement>("event-options"));
            bridge.Changed += Render;
            Render();
        }

        /// <summary>True for a screenshot run (?shot, -shot; VisualShots.Requested) that is not also a smoke run: the card is
        /// then never shown, so the captures show the map. A smoke run presses the card's options, so it
        /// keeps the card; with no flag (normal play) the card shows.</summary>
        public static bool HiddenInCaptures(string? url, string[] args, bool webgl) =>
            StoryOfTheLand.Game.Map.VisualShots.Requested(url, args, webgl) && GameRoot.SmokeTurns(url, args, webgl) == 0;

        /// <summary>Unsubscribes from the bridge, removes the option buttons and hides the card (the owner calls this when disabled).</summary>
        public void Dispose()
        {
            bridge.Changed -= Render;
            if (fadeOut != null) { fadeOut.Pause(); fadeOut = null; card.RemoveFromClassList(OutClass); }
            hide.clicked -= onHide; reopen.clicked -= onReopen;
            hide.UnregisterCallback(onHidePointer, TrickleDown.TrickleDown); hide.UnregisterCallback(onHideSubmit, TrickleDown.TrickleDown);
            card.UnregisterCallback(onCancel);
            ScreenNav.OnUncovered(layer, null);
            presses.Dispose();
            card.Q<VisualElement>("event-options")?.Clear(); card.style.display = DisplayStyle.None;
            figures.Clear(); sources.Clear(); note.style.display = DisplayStyle.None; chosen.style.display = DisplayStyle.None;
            footage.RemoveFromHierarchy(); viewer.Dispose();
            reopen.style.display = DisplayStyle.None;
            hudRoot.RemoveFromClassList(OpenClass);
            ScreenNav.DisableCovered(hudRoot);
        }

        /// <summary>True while the card is collapsed to the "Decision waiting" button (Hide).</summary>
        public bool Collapsed => Shown != null && collapsedFor == Shown.Id;

        /// <summary>Hide: the card gives the map back and waits behind the "Decision waiting" button, which takes the focus.</summary>
        public void Collapse()
        {
            if (Shown == null) return;
            collapsedFor = Shown.Id;
            Render();
            if (hideByPointer) { hideByPointer = false; return; }
            reopen.Focus();
            // Tried again once styles resolve: the button was hidden a moment ago and may not take it yet.
            reopen.schedule.Execute(() => { if (Collapsed && reopen.focusController?.focusedElement != reopen) reopen.Focus(); });
        }

        /// <summary>Escape: Hide, when the card is open and in reach. True when it did.</summary>
        public bool Escape()
        {
            if (viewer.IsOpen) { viewer.Close(); return true; }   // the footage viewer over the card goes first
            if (Shown == null || Collapsed || !layer.enabledInHierarchy) return false;
            hideByPointer = false;
            Collapse();
            return true;
        }

        /// <summary>"Decision waiting": the card opens again and takes the focus.</summary>
        public void Reopen()
        {
            collapsedFor = null; focusedFor = null;
            Render();
        }

        /// <summary>The event the card shows, or null when every pending event has a queued answer.</summary>
        public GameEvent? Shown { get; private set; }

        public void Render()
        {
            var c = bridge.Content; var s = bridge.State;
            if (bridge.Session != session) { session = bridge.Session; collapsedFor = null; focusedFor = null; outcome = null; }
            var pending = s.PendingEvents.Select(id => c.Events.First(e => e.Id == id)).ToList();
            var open = pending.Where(e => bridge.QueuedLike(new ChooseEventOption(e.Id, "")) == null).ToList();
            // A family's answered card stays up with what followed until Continue (F5), as long as it is still waiting.
            if (outcome != null && !pending.Contains(outcome.Value.e)) outcome = null;
            Shown = outcome?.e ?? open.FirstOrDefault();
            if (Shown == null) collapsedFor = null;
            var shown = !hiddenInCapture && Shown != null && !Collapsed;
            // F7: a card that comes up slides in and fades in (StoryOfTheLand.uss, only with SettingsPresenter.MotionClass on the root).
            bool wasShown = card.style.display.keyword == StyleKeyword.Undefined && card.style.display.value == DisplayStyle.Flex;
            var arriving = Shown?.Id != lastShownId && shown;
            lastShownId = shown ? Shown?.Id : null;
            bool motion = hudRoot.ClassListContains(SettingsPresenter.MotionClass);
            // F7: a card that goes (answered, hidden) fades and slides out first, its last content kept until it has gone; one
            // that comes back meanwhile stops the fade.
            if (shown && fadeOut != null) { fadeOut.Pause(); fadeOut = null; card.RemoveFromClassList(OutClass); }
            bool fading = !shown && (fadeOut != null || wasShown && motion);
            if (fading && fadeOut == null)
            {
                card.AddToClassList(OutClass);
                fadeOut = card.schedule.Execute(EndFade);
                fadeOut.ExecuteLater(FadeOutMs);
            }
            if (!fading) card.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown && (!wasShown || arriving) && motion) Enter(card, "card-from", 0);
            card.SetEnabled(shown);   // a child of an undisplayed element can still take the focus (#400): a collapsed card's options must not
            reopen.style.display = !hiddenInCapture && Collapsed ? DisplayStyle.Flex : DisplayStyle.None;
            hudRoot.EnableInClassList(OpenClass, shown);
            ScreenNav.DisableCovered(hudRoot);
            if (!fading) RenderFamily(c, s, open.Count, shown);
        }

        /// <summary>The family's card (see the class summary): the text in the UI's language, or what followed the
        /// choice just made, with the content note, the people and the sources.</summary>
        void RenderFamily(Content c, GameState s, int openCount, bool shown)
        {
            var options = card.Q<VisualElement>("event-options");
            options.Clear(); figures.Clear(); sources.Clear(); hint.Clear(); footage.Clear();
            if (Shown == null) { note.style.display = DisplayStyle.None; chosen.style.display = DisplayStyle.None; asking = null; return; }
            var e = Shown; bool arabic = FamilyGame.Arabic(text);
            card.Q<Label>("event-title").text = FamilyGame.Title(c, s, e, arabic);
            note.text = text["event.content_note"];
            note.style.display = FamilyGame.Sensitive(e) ? DisplayStyle.Flex : DisplayStyle.None;
            var after = outcome != null && outcome.Value.e == e ? outcome.Value.o : null;
            chosen.text = after == null ? "" : text.Format("event.you_chose", FamilyGame.Label(c, s, e, after, arabic));
            chosen.style.display = after == null ? DisplayStyle.None : DisplayStyle.Flex;
            card.Q<Label>("event-text").text = after == null ? FamilyGame.Text(c, s, e, arabic) : FamilyGame.Outcome(c, s, e, after, arabic);
            if (after != null)
            {
                // Playtest (F6): what the choice changes, linked to the choice (its own effects: the event's apply whatever is chosen).
                if (after.Effects.Count > 0)
                {
                    var caption = new Label(text["event.effects"]) { name = "event-effects-caption" };
                    caption.AddToClassList("source-caption");
                    options.Add(caption);
                    options.Add(EffectRow(c, after, arabic, "event-effects"));
                    // Playtest 3: the bar can move by more (or less) than these: the card's own effects and the month's other events.
                    var others = new Label(text["event.others"]) { name = "event-others" };
                    others.AddToClassList("source-caption");
                    options.Add(others);
                }
                var next = new Button(Continue) { name = "event-continue", text = text["event.continue"] };
                next.AddToClassList(OptionClass);
                next.generateVisualContent += PaintSign;
                options.Add(next);
            }
            else
                foreach (var o in e.Options)
                {
                    var option = o;
                    var b = new Button(() => Choose(e, option)) { name = "option-" + o.Id, text = FamilyGame.Label(c, s, e, o, arabic) };
                    // Playtest 2: before the choice, what it affects (each resource and its direction, no numbers) in the foot,
                    // while the pointer is over it or it has the keyboard focus. In the foot's row, so the card is no taller
                    // (chips under each choice pushed the tallest card into the toast's place).
                    b.RegisterCallback<PointerEnterEvent>(_ => ShowHint(c, option, arabic));
                    b.RegisterCallback<FocusInEvent>(_ => ShowHint(c, option, arabic));
                    b.RegisterCallback<PointerLeaveEvent>(_ => hint.Clear());
                    b.RegisterCallback<FocusOutEvent>(_ => hint.Clear());
                    b.AddToClassList(OptionClass);
                    b.generateVisualContent += PaintSign;
                    options.Add(b);
                }
            var people = figuresFor?.Invoke(e.Id) ?? System.Array.Empty<Figure>();
            if (people.Count > 0)
            {
                var caption = new Label(text["event.figures"]); caption.AddToClassList("source-caption");
                figures.Add(caption);
                foreach (var f in people)
                {
                    var id = f.Id;
                    var link = new Button(() => openFigure?.Invoke(id)) { name = "event-figure-" + f.Id, text = f.NameIn(arabic) };
                    link.AddToClassList("figure-link");
                    // Owner (2026-10-22): the figure's portrait (PeoplePresenter's picture) shows beside the name, unasked;
                    // pressing it opens the People screen too. A figure with no picture keeps the plain link.
                    var picture = UnityEngine.Resources.Load<UnityEngine.Texture2D>(PeoplePresenter.PortraitFolder + f.Id);
                    if (picture == null) { figures.Add(link); continue; }
                    var chip = new VisualElement { name = "event-figure-chip-" + f.Id };
                    chip.AddToClassList(FigureChipClass);
                    var face = new VisualElement { name = "event-portrait-" + f.Id, tooltip = f.NameIn(arabic) };
                    face.AddToClassList(FigurePortraitClass);
                    face.style.backgroundImage = picture;
                    face.RegisterCallback<ClickEvent>(_ => openFigure?.Invoke(id));
                    chip.Add(face); chip.Add(link);
                    figures.Add(chip);
                }
            }
            RenderFootage(e, arabic);
            if (e.Sources.Count > 0) sources.Add(SourceLinks.Row(e.Sources.Select(x => x.Url), text["event.sources"], text, hudRoot));
            // Playtest (F6): "1 more card this turn", "2 more cards this turn"; Arabic by its own number rule (UiText.Plural).
            int more = openCount - (after == null ? 1 : 0);
            card.Q<Label>("event-count").text = more > 0 ? text.Plural("event.more", more) : "";
            if (shown && focusedFor != e.Id) FocusCard(e.Id, force: false);
        }

        /// <summary>The owner's decision (2026-10-22, "Map + checked clips"): an event with clips the owner watched and signed
        /// off has a "Watch footage" button for each. Pressing one puts the clip's content note in its place with Open and
        /// Cancel; only Open sends the browser to the publisher's page. A keyboard press keeps the focus in the row (Cancel,
        /// then the button again), as the pressed button goes.</summary>
        void RenderFootage(GameEvent e, bool arabic)
        {
            var focused = footage.focusController?.focusedElement as VisualElement;
            bool hadFocus = focused != null && footage.Contains(focused);
            footage.Clear();
            var clips = clipsFor?.Invoke(e.Id) ?? System.Array.Empty<Clip>();
            if (asking != null && !clips.Contains(asking)) asking = null;
            footage.style.display = clips.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            Button? focus = null;
            if (asking != null)
            {
                var clip = asking;
                var warn = new Label(text["event.footage_note"]) { name = "event-footage-note" };
                warn.AddToClassList("content-note");
                footage.Add(warn);
                var open = new Button(() => { asking = null; Watch(clip, arabic); RenderFootage(e, arabic); })
                    { name = "event-footage-open", text = text["event.footage_open"], tooltip = clip.TitleIn(arabic) + "\n" + clip.Url };
                var cancel = new Button(() => { asking = null; RenderFootage(e, arabic); }) { name = "event-footage-cancel", text = text["event.footage_cancel"] };
                foreach (var b in new[] { open, cancel }) { b.AddToClassList("figure-link"); footage.Add(b); }
                focus = cancel;
            }
            else
                for (int i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    var watch = new Button(() => { asking = clip; RenderFootage(e, arabic); })
                    {
                        name = "event-watch-" + i,
                        text = clips.Count > 1 ? text.Format("event.watch_n", i + 1) : text["event.watch"],
                        tooltip = clip.TitleIn(arabic),
                    };
                    watch.AddToClassList("figure-link");
                    footage.Add(watch);
                    focus ??= watch;
                }
            if (hadFocus) focus?.Focus();
        }

        /// <summary>Open, after the content note: a direct video or photo file plays in the viewer over the card; a publisher's
        /// page, and in the web player every clip, opens in the browser (Application.OpenURL: a new tab on the web, where
        /// media.archivegenocide.com's files cannot be read: it sends no CORS header).</summary>
        void Watch(Clip clip, bool arabic)
        {
            if (FootageViewer.Plays(clip, Web)) viewer.Show(clip, arabic);
            else openUrl(FootageViewer.BrowserUrl(clip));
        }

        /// <summary>The footage viewer (tests: it is open on a clip, then closed).</summary>
        public FootageViewer Viewer => viewer;

        /// <summary>A choice's effects as chips: each resource's sign (HudPresenter.FamilyGlyphs), its name in the UI's language
        /// and its change ("+5%"), green up and red down. The change is its own label, so a right-to-left line never reorders
        /// its sign.</summary>
        VisualElement EffectRow(Content c, EventOption o, bool arabic, string name)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList(EffectRowClass);
            foreach (var fx in o.Effects)
            {
                var chip = new VisualElement { name = name + "-" + fx.Target };
                chip.AddToClassList("effect-chip");
                chip.Add(new UiIcon { Glyph = HudPresenter.FamilyGlyphs.TryGetValue(fx.Target, out var g) ? g : "region" }.WithClass("effect-icon"));
                chip.Add(new Label(FamilyGame.Resource(c, fx.Target, arabic)).WithClass("effect-name"));
                var delta = new Label(PlayerText.PercentChange(fx.Delta, 0)).WithClass("effect-delta");
                delta.AddToClassList(fx.Delta > 0 ? "delta-up" : "delta-down");
                chip.Add(delta);
                if (hudRoot.ClassListContains(SettingsPresenter.MotionClass)) Enter(chip, "chip-from", 70L * row.childCount);   // F7: they pop in, one after another
                row.Add(chip);
            }
            return row;
        }

        /// <summary>Playtest 2: what <paramref name="o"/> affects, before it is chosen: a chip per resource it moves, with its sign,
        /// name and direction (↑ or ↓, coloured), no number; nothing for a choice that moves none.</summary>
        public void ShowHint(Content c, EventOption o, bool arabic)
        {
            hint.Clear();
            foreach (var fx in o.Effects.Where(f => f.Delta != 0))
            {
                var chip = new VisualElement { name = "event-hint-" + fx.Target, pickingMode = PickingMode.Ignore };
                chip.AddToClassList("effect-chip");
                chip.Add(new UiIcon { Glyph = HudPresenter.FamilyGlyphs.TryGetValue(fx.Target, out var g) ? g : "region" }.WithClass("effect-icon"));
                chip.Add(new Label(FamilyGame.Resource(c, fx.Target, arabic)) { pickingMode = PickingMode.Ignore }.WithClass("effect-name"));
                var dir = new Label(fx.Delta > 0 ? "↑" : "↓") { pickingMode = PickingMode.Ignore }.WithClass("effect-dir");
                dir.AddToClassList(fx.Delta > 0 ? "delta-up" : "delta-down");
                chip.Add(dir);
                hint.Add(chip);
            }
        }

        string? lastShownId;
        /// <summary>F7: the card's fade-out under way, its class (StoryOfTheLand.uss) and length.</summary>
        IVisualElementScheduledItem? fadeOut;

        /// <summary>The fade-out's end (FadeOutMs after it began): the card goes, and the layer shows what is now open.</summary>
        internal void EndFade()
        {
            if (fadeOut == null) return;
            fadeOut.Pause(); fadeOut = null;
            card.RemoveFromClassList(OutClass);
            card.style.display = DisplayStyle.None;
            Render();
        }
        public const string OutClass = "card-out";
        const long FadeOutMs = 200;

        /// <summary>F7: <paramref name="e"/> starts in its <paramref name="from"/> state (StoryOfTheLand.uss) and moves to its own
        /// after <paramref name="delayMs"/> and EnterMs, by the stylesheet's transition.</summary>
        internal static void Enter(VisualElement e, string from, long delayMs)
        {
            e.AddToClassList(from);
            // Playtest 3: in the web player a frame takes longer than 16 ms, so the class came off before a style pass had drawn
            // the start and nothing moved. It comes off after EnterMs, once the element has been laid out with it (a few tries).
            int tries = 0;
            void Off()
            {
                if (e.layout.width > 0 || ++tries >= 10) e.RemoveFromClassList(from);
                else e.schedule.Execute(Off).ExecuteLater(EnterMs);
            }
            e.schedule.Execute(Off).ExecuteLater(EnterMs + delayMs);
        }

        /// <summary>How long an entering element keeps its start class: a few frames at 30 per second.</summary>
        internal const long EnterMs = 60;

        /// <summary>A named figure on the card: its portrait and its link side by side (StoryOfTheLand.uss).</summary>
        public const string FigureChipClass = "figure-chip", FigurePortraitClass = "figure-face";

        /// <summary>The row of a choice's effect chips (StoryOfTheLand.uss).</summary>
        public const string EffectRowClass = "effect-row";

        /// <summary>F5: Continue under a choice's outcome: the next card, or End Turn when none is left (from the keyboard).</summary>
        void Continue()
        {
            outcome = null;
            Render();
            if (Shown == null && presses.ByKeyboard) FocusReturn.To(hudRoot.Q<Button>("end-turn"));
        }


        /// <summary>A newly shown event puts the keyboard focus on the card (keyboard play, #41): the next Tab reaches its
        /// first option, and no option is lit before the player picks. Not while the layer is out of reach (the start screen
        /// or a screen covers it): then nothing is recorded, and the card takes the focus when the cover closes
        /// (onUncovered, <paramref name="force"/>). It is tried again once styles resolve, as a still-hidden card cannot take
        /// it; that retry takes it from nothing, or, when forced, from whatever the closing cover left focused.</summary>
        void FocusCard(string id, bool force)
        {
            if (!layer.enabledInHierarchy) return;
            focusedFor = id;
            card.Focus();
            card.schedule.Execute(() =>
            {
                if (Shown?.Id != id || Collapsed) return;
                if (!layer.enabledInHierarchy) { if (focusedFor == id) focusedFor = null; return; }   // covered meanwhile: try again when uncovered
                var now = card.focusController?.focusedElement;
                if (card.panel != null && now != card && (force || now == null)) card.Focus();
            });
        }


        /// <summary>The round sign in an option's left padding (StoryOfTheLand.uss .event-option), painted into the button so it
        /// stays a leaf whose wrapped text Yoga measures (UiIcon.Paint). In the Arabic UI it sits in the right padding and
        /// points left (#193).</summary>
        const float SignRadius = 15f, SignLeft = 9f;

        static void PaintSign(MeshGenerationContext m)
        {
            var e = m.visualElement;
            bool rtl = UiLanguage.Within(e);
            var x = rtl ? e.layout.width - SignLeft - SignRadius : SignLeft + SignRadius;
            UiIcon.PaintDisc(m, rtl ? "chevron-back" : "chevron", new UnityEngine.Vector2(x, e.layout.height / 2f), SignRadius);
        }

        void Choose(GameEvent e, EventOption o)
        {
            // The card stays with what followed, when the choice has an outcome to tell (set first: Queue renders).
            // Playtest (F6): a choice with effects always shows them, under its outcome (chips under every choice made the
            // tallest card reach the toast's place at the top, DecisionCardTests.TheToastClearsTheOpenCardsTitleAndOptions).
            var told = FamilyGame.Outcome(bridge.Content, bridge.State, e, o, FamilyGame.Arabic(text)).Length > 0 || o.Effects.Count > 0;
            outcome = told ? (e, o) : ((GameEvent, EventOption)?)null;
            if (bridge.Queue(new ChooseEventOption(e.Id, o.Id)) != null) { outcome = null; Render(); return; }
            // Answered from the keyboard, the focus goes to the next button, or to End Turn with no event left (#41 M1).
            if (presses.ByKeyboard) FocusReturn.To(Shown == null ? hudRoot.Q<Button>("end-turn") : card.Q<VisualElement>("event-options").Q<Button>());
        }
    }
}
