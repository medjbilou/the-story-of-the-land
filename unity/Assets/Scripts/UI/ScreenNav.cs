#nullable enable
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The side-menu buttons open the People, Why, How to play and Settings screens, one at a time; pressing the open
    /// screen's button again closes it. Every press toggles, synchronously, with no debounce: two presses in one frame (a
    /// double click, or a click plus Enter) open and close again, so the result depends only on the number of presses
    /// (HudTests.TwoPressesInOneFrameToggleTwice; playtest 1, item 11).</summary>
    public sealed class ScreenNav : System.IDisposable
    {
        public static readonly string[] Screens = { "why", "how-to-play", "settings" };
        /// <summary>The screens opened like the others but without a button of their own in Game.uxml: People (PeoplePresenter
        /// adds its left-column button) and Credits (opened from Settings, CreditsPresenter). A screen with no button returns
        /// the focus to Settings' when it closes.</summary>
        public static readonly string[] Extra = { "people", "credits" };
        static IEnumerable<string> All => System.Linq.Enumerable.Concat(Screens, Extra);
        /// <summary>The class on the open screen's button (StoryOfTheLand.uss lights its ring).</summary>
        public const string OpenClass = "nav-open";
        /// <summary>The class on the "root" element while any screen is open: StoryOfTheLand.uss hides the decision layer under it,
        /// and in the two-row (narrow) HUD, where every screen takes the full width, the minimap column too (#210).</summary>
        public const string ScreenOpenClass = "screen-open";

        /// <summary>Disables each HUD panel that a class on the "root" element hides, and enables it again when none does: a
        /// child of an undisplayed element can still take the keyboard focus, so hiding alone leaves its buttons in Tab's
        /// reach (#400). The start screen, the chapter and village picker and the pause menu (PausePresenter) cover the top bar,
        /// the left column, the minimap column and the decision layer; any open screen covers the decision layer (it sits where the card does), and in the
        /// two-row HUD also the minimap column; the open decision card's backdrop (EventCardPresenter.OpenClass, with no
        /// start screen or screen over it: ModalClass, set here) covers the minimap column. Called wherever one of those
        /// classes changes (StartScreenPresenter, FamilyPickerPresenter, Open, HudPresenter's tier, EventCardPresenter), so no
        /// caller re-enables a panel another still covers.</summary>
        public static void DisableCovered(VisualElement root)
        {
            var r = root.Q("root") ?? root;
            bool start = r.ClassListContains(StartScreenPresenter.OpenClass) || r.ClassListContains(FamilyPickerPresenter.OpenClass)
                || r.ClassListContains(PausePresenter.OpenClass);
            bool screen = r.ClassListContains(ScreenOpenClass);
            bool full = screen && r.ClassListContains(HudPresenter.TwoRowClass);
            bool modal = r.ClassListContains(EventCardPresenter.OpenClass) && !start && !screen;
            r.EnableInClassList(EventCardPresenter.ModalClass, modal);
            r.Q("top-bar")?.SetEnabled(!start);
            r.Q("left-column")?.SetEnabled(!start);   // the screens' buttons: usable wherever the top bar is
            r.Q("right-column")?.SetEnabled(!(start || full || modal));
            var layer = r.Q("event-layer");
            if (layer != null)
            {
                bool was = layer.enabledSelf, now = !(start || screen);
                layer.SetEnabled(now);
                if (!was && now) (layer.userData as System.Action)?.Invoke();   // EventCardPresenter: the open card takes the focus
            }
        }

        /// <summary>Asks to be told, synchronously, whenever the decision layer comes out from under the start screen or a
        /// screen (DisableCovered enables it again); null stops it.</summary>
        public static void OnUncovered(VisualElement layer, System.Action? action) => layer.userData = action;

        /// <summary>Asks to be told, synchronously, whenever <paramref name="screen"/> is opened (a presenter that skips
        /// rendering while its screen is hidden catches up then).</summary>
        public static void OnOpened(VisualElement screen, System.Action action) => screen.userData = action;

        /// <summary>True when <paramref name="screen"/> is open (ScreenNav shows it inline; the stylesheet hides it).</summary>
        public static bool IsOpen(VisualElement screen) =>
            screen.style.display.keyword == StyleKeyword.Undefined && screen.style.display.value == DisplayStyle.Flex;
        readonly VisualElement root;
        readonly List<(Button, System.Action)> hooks = new List<(Button, System.Action)>();
        readonly List<Button> closes = new List<Button>();
        readonly EventCallback<NavigationCancelEvent> onCancel;
        readonly EventCallback<GeometryChangedEvent> onGeometry;

        public ScreenNav(VisualElement root)
        {
            this.root = root;
            // Escape (the UI map's Cancel, from any focused element or the panel) closes the open screen. NavigationGuard
            // handles only NavigationMoveEvent, so the two never meet. GameRoot also polls the Escape key for the case where
            // no element has the focus (CloseOnEscape).
            onCancel = e => { if (Close()) e.StopPropagation(); };
            root.RegisterCallback(onCancel, TrickleDown.TrickleDown);
            onGeometry = e => FitCloseButtons();
            root.RegisterCallback(onGeometry);
            foreach (var s in All)
            {
                var screen = s;
                var b = root.Q<Button>("nav-" + screen);
                if (b != null)
                {
                    System.Action h = () => Toggle(screen);
                    b.clicked += h;
                    hooks.Add((b, h));
                }

                // an X on the screen itself closes it: a phone has no Escape, and re-tapping the lit rail button is not
                // discoverable (playtest v0.1.1). The same Close() as Escape, so the focus return is identical.
                var header = root.Q("screen-" + screen)?.Q(className: "screen-header");
                if (header == null) continue;
                var x = new Button(() => Close()) { name = "screen-close-" + screen, text = "×" };   // the multiplication sign, not a Latin letter (Arabic review of F5)
                x.AddToClassList("screen-close");
                header.Add(x);
                closes.Add(x);
            }
            Open(null);
            FitCloseButtons();
        }

        /// <summary>The X's hit area: 44 screen px at every window size (#1035's ruling), so in panel units it grows as
        /// the panel shrinks (<paramref name="scale"/> is screen px per panel px; the 1280x720 window scales about 2/3,
        /// so 44 panel px would render as about 29 px there). On the web a screen pixel is a CSS pixel: the canvas is
        /// drawn in device pixels, so the target is <paramref name="dpr"/> device px per CSS px (HudPresenter.cs's text
        /// floor counts the same way, DevicePixelRatio.jslib). Off the web <paramref name="dpr"/> is 1.</summary>
        internal static float CloseTargetPanelPx(float scale, float dpr = 1f) =>
            scale > 0f ? 44f * (dpr > 0f ? dpr : 1f) / scale : 44f;

#if UNITY_WEBGL && !UNITY_EDITOR
        // The panel scales by the canvas's device pixels, so a screen pixel here is a CSS pixel: the device's pixels / the
        // browser's devicePixelRatio (DevicePixelRatio.jslib; the same import as HudPresenter's text floor).
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern float StoryOfTheLandDevicePixelRatio();
        static float DevicePixelRatio() { var r = StoryOfTheLandDevicePixelRatio(); return r > 0f ? r : 1f; }
#else
        static float DevicePixelRatio() => 1f;
#endif

        void FitCloseButtons()
        {
            var px = CloseTargetPanelPx(root.panel?.scaledPixelsPerPoint ?? 1f, DevicePixelRatio());
            var r = new StyleLength(new Length(px / 2f));
            foreach (var x in closes)
            {
                x.style.width = px; x.style.height = px;
                x.style.borderTopLeftRadius = r; x.style.borderTopRightRadius = r;
                x.style.borderBottomLeftRadius = r; x.style.borderBottomRightRadius = r;

                // Pad both sides of the header here, not in the stylesheet: a grown X (66 panel px at 1280x720, 89 at
                // 390x844) would cover the title's end under the old fixed 52 px, and one inline rule serves both
                // languages. Also grow the header so the X (top 6 px + px) fits inside it, with its 4 px bottom
                // padding and 1 px border.
                var header = x.parent;
                if (header == null) continue;
                header.style.paddingLeft = new StyleLength(px + 8f);
                header.style.paddingRight = new StyleLength(px + 8f);
                header.style.minHeight = new StyleLength(px + 6f + 5f);
            }
        }

        public void Dispose()
        {
            foreach (var (b, h) in hooks) b.clicked -= h;
            hooks.Clear();
            foreach (var x in closes) x.RemoveFromHierarchy();
            closes.Clear();
            root.UnregisterCallback(onCancel, TrickleDown.TrickleDown);
            root.UnregisterCallback(onGeometry);
            var r = root.Q("root") ?? root;
            r.RemoveFromClassList(ScreenOpenClass);
            DisableCovered(root);
        }

        /// <summary>Closes the open screen (Escape); false when none was open. When the focus was on the screen's contents it
        /// goes to the screen's top-bar button, so the next Tab carries on from there (#41 M1); an open decision card
        /// takes it instead (EventCardPresenter, when its cover closes).</summary>
        public bool Close()
        {
            if (Current == null) return false;
            // Playtest 3: one Escape closes one level. It reaches here twice in a player (GameRoot's key poll and the UI's Cancel
            // event), which closed Credits and then Settings under it.
            if (UnityEngine.Application.isPlaying && UnityEngine.Time.frameCount == closedFrame) return true;
            closedFrame = UnityEngine.Time.frameCount;
            // Audit m8: a screen opened from another (Credits from Settings) closes back to it, one level at a time.
            if (parent != null)
            {
                var back = parent; var opener = parentFocus;
                Open(back);
                if (opener?.panel != null) FocusReturn.To(opener);
                return true;
            }
            var screen = root.Q<VisualElement>("screen-" + Current); var button = root.Q<Button>("nav-" + Current) ?? root.Q<Button>("nav-settings");
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            var inside = focused != null && (focused == screen || screen.Contains(focused));
            Open(null);
            if (inside && !(root.Q("root") ?? root).ClassListContains(EventCardPresenter.ModalClass)) FocusReturn.To(button);
            return true;
        }

        /// <summary>The open screen's name, or null.</summary>
        public string? Current { get; private set; }

        /// <summary>After every Open, Close and Toggle, once the screens show what Current says (the pause menu comes back when
        /// the screen it opened closes).</summary>
        public event System.Action? Changed;

        public void Toggle(string screen)
        {
            Boot.DiagLog.NavLine($"toggle {screen}: {Current ?? "none"} -> {(Current == screen ? "none" : screen)}");
            Open(Current == screen ? null : screen);
        }

        /// <summary>The screen a button-less screen (Credits) was opened from, and the control that opened it: Close goes back.</summary>
        string? parent; VisualElement? parentFocus;
        int closedFrame = -1;

        public void Open(string? screen)
        {
            bool child = screen != null && Current != null && Current != screen && root.Q<Button>("nav-" + screen) == null;
            parent = child ? Current : null;
            parentFocus = child ? root.panel?.focusController?.focusedElement as VisualElement : null;
            Current = screen;
            foreach (var s in All)
            {
                var element = root.Q<VisualElement>("screen-" + s);
                if (element == null) continue;
                element.style.display = s == screen ? DisplayStyle.Flex : DisplayStyle.None;
                root.Q<Button>("nav-" + s)?.EnableInClassList(OpenClass, s == screen);   // the open screen's button is lit
            }
            (root.Q("root") ?? root).EnableInClassList(ScreenOpenClass, screen != null);
            DisableCovered(root);
            if (screen != null) (root.Q<VisualElement>("screen-" + screen).userData as System.Action)?.Invoke();
            Changed?.Invoke();
        }
    }
}
