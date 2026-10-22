#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StoryOfTheLand.Sim;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>Fills the top bar and runs End Turn. The bar shows the family's ten resources (palestine/model.yaml, in the
    /// UI's language: F5 step 4), Livelihood with the money in use beside its name (its sign; its name, sign and smaller
    /// unit in the tip); the date is the sim's own (PlayerText.Date in the UI's language). The root gets FamilyClass.</summary>
    public sealed class HudPresenter : System.IDisposable
    {
        readonly VisualElement root; readonly SimBridge bridge; readonly UiText text;
        /// <summary>On the "root" element while a family game is played (StoryOfTheLand.uss).</summary>
        public const string FamilyClass = "family";
        /// <summary>Each resource's sign in the family bar.</summary>
        public static readonly IReadOnlyDictionary<string, string> FamilyGlyphs = new Dictionary<string, string>
        {
            ["land"] = "region", ["home"] = "house", ["family"] = "people", ["documents"] = "ledger", ["livelihood"] = "coin",
            ["memory"] = "book", ["sumud"] = "peaks", ["community"] = "crowd", ["safety"] = "shield", ["solidarity"] = "rings",
        };
        readonly VisualElement familyMeters;
        /// <summary>The family bar's resource meters and their captions in the UI's language.</summary>
        readonly List<(string id, VisualElement element, string caption)> familySaid = new List<(string, VisualElement, string)>();
        readonly Button endTurn; readonly System.Action onEndTurn; readonly System.Action? chapterEnded, decisionWaiting;
        readonly VisualElement bar, hudRoot, nav;
        readonly System.Collections.Generic.List<VisualElement> meters;

        // The tiers go by the bar's logical width (1080p reference, match 0.5: the panel's scale is the plain mean of the two
        // ratios, (w/1920 + h/1080) / 2, so logical width = w / that; TopBarTests logs it). The family bar holds ten resources
        // and the money. With their captions one row needs about 2250 px with the widest values (21:9 at 2560 is about 2217),
        // so below NarrowBelowPx the one-row bar drops the captions (F6): each meter is its icon, value and change, its name in
        // its tooltip and in the tip under the bar (MeterTip), which keeps 16:9 1920 and 16:10 on one row. Below IconRowBelowPx
        // the stacked bar comes back with its captions; below CompactBelowPx it also drops the icons:
        //   NarrowBelowPx 2250 | 21:9 ~2217 | 16:9 1920 | 16:10 ~1911, ~1819 | 1700x1080 ~1803 | IconRowBelowPx 1800 | 1650x1080
        //   ~1775 | CompactBelowPx 1700 | 4:3 ~1646
        //   | 5:4 ~1585 | TwoRowBelowPx 1400 | square ~1382 | half a 1080p screen (960x1080) ~1280 | 800x1000 ~1192
        //   | ThreeRowBelowPx 1100.
        // PromoHudTests.EveryWindowShapeFitsTheBarAfterATurn proves each tier holds the widest late-game values and changes.

        /// <summary>Below this width the 70 px one-row bar (promo 03's 6.5%) cannot hold the eleven meters with their captions:
        /// down to IconRowBelowPx it shows them icon first (IconRowClass), and below that the HUD's root gets NarrowClass, a
        /// taller bar (124 px) whose meters stack the change under the value, with the panels below it moved down to match
        /// (StoryOfTheLand.uss .hud-narrow).</summary>
        public const float NarrowBelowPx = 2250f;
        /// <summary>The resource whose meter carries the money in use (its sign beside its name), and that meter's class.</summary>
        public const string MoneyMeter = "livelihood", MoneyClass = "money-meter";
        /// <summary>A meter's name under it on the icon-first bar (playtest 2: the icons alone were not read).</summary>
        public const string ShortClass = "meter-short";
        public const string NarrowClass = "hud-narrow";
        /// <summary>From this width up to NarrowBelowPx the one-row bar's meters are icon first (StoryOfTheLand.uss .top-bar--icons):
        /// no caption, the name in the meter's tooltip and its tip (MeterTip).</summary>
        public const float IconRowBelowPx = 1800f;
        public const string IconRowClass = "top-bar--icons";
        /// <summary>On the "root" element with IconRowClass: the icon-first bar is 88 px, not 70, for each meter's name under it
        /// (playtest 2), and the decision card's backdrop band under it is shorter to match (StoryOfTheLand.uss).</summary>
        public const string IconBarRootClass = "hud-icon-bar";
        /// <summary>Below this width the meters also drop their icons (StoryOfTheLand.uss .top-bar--compact): a square window does not
        /// have the room for them on the stacked bar.</summary>
        public const float CompactBelowPx = 1700f;
        public const string CompactClass = "top-bar--compact";
        /// <summary>Below this width (windows narrower than about square, e.g. a browser snapped to half a 1080p screen, about
        /// 1360 logical px, and a square one, about 1382) one row cannot hold the badge, the meters, New Game and End Turn: the root also gets
        /// TwoRowClass, and the bar wraps into two rows, the date and the meters above, the screen buttons and End Turn
        /// below (StoryOfTheLand.uss .hud-two-row).</summary>
        public const float TwoRowBelowPx = 1400f;
        /// <summary>Playtest 3: below this logical height (a 16:9 window at 115% text and up: 1080 / 1.3 is about 831) the
        /// stacked one-row bar is too tall a share of the window: the root also gets FlatClass, and each meter keeps its
        /// change beside its value, a line shorter (StoryOfTheLand.uss .hud-flat, FlatBarPx).</summary>
        public const float FlatBelowPx = 1000f;
        public const string FlatClass = "hud-flat";
        /// <summary>StoryOfTheLand.uss .hud-flat .top-bar height (the stacked bar's is 124 px).</summary>
        public const float FlatBarPx = 88f;
        public const string TwoRowClass = "hud-two-row";
        /// <summary>Below this width (a phone held upright: 390x844 is about 790 logical px) the badge and the meters do not
        /// fit one row either: the root also gets ThreeRowClass, and the bar takes three rows, the badge, the meters (eleven,
        /// wrapped to three lines), then the screen buttons and End Turn (StoryOfTheLand.uss .hud-three-row; #210).</summary>
        public const float ThreeRowBelowPx = 1100f;
        public const string ThreeRowClass = "hud-three-row";
        /// <summary>On the one-row bar with captions, when the meters leave enough room, the bar gets RoomyClass: promo-sized icons (38 px)
        /// and values (25 px) (StoryOfTheLand.uss .top-bar--roomy). It turns on only when the measured spare room covers the
        /// predicted growth (RoomyGrowth) plus SpareShare, and off when the spare room falls below SpareShare, so it never
        /// flips back and forth.</summary>
        public const string RoomyClass = "top-bar--roomy";
        /// <summary>The share of the meters' width kept free after them (room for the browser's text measuring wider than the
        /// editor's; PromoHudTests.EveryWindowShapeFitsTheBarAfterATurn asserts it).</summary>
        public const float SpareShare = 0.05f;

        /// <summary>The meters' predicted growth when roomy: each of the <paramref name="icons"/> shown icons 8 px wider, values
        /// 22 -> 25 px (about 14% wider), and each wrapped caption back to its one-line width (<paramref name="captionGrowth"/>).</summary>
        public static float RoomyGrowth(float valueWidths, float captionGrowth, int icons) => icons * 8f + 0.14f * valueWidths + captionGrowth;

        /// <summary>The top bar's static texts and their strings (#193): the screen buttons, New Game and End Turn. The
        /// meters' captions are hud.caption.*, the same words their tooltips say.</summary>
        static readonly (string name, string key)[] Texts =
        {
            ("nav-why", "hud.nav.why"), ("nav-how-to-play", "hud.nav.how_to_play"), ("nav-settings", "hud.nav.settings"),
            ("new-game", "hud.new_game"), ("end-turn", "hud.end_turn"),
        };

        /// <summary>The owner's text floor (#624, 15 October 2026): the bar's small text (the meters' captions, SmallTextPx in the
        /// stylesheet) stays at least this many pixels on screen. The panel scales
        /// with the window (about 0.67 at 1280x720, 0.62 at 1024x768), so at small windows the text is set larger in panel
        /// px (SmallTextSize).</summary>
        public const float TextFloorPx = 12f;
        /// <summary>The bar's small text in the stylesheet (StoryOfTheLand.uss .meter-name).</summary>
        public const float SmallTextPx = 15f;

        /// <summary>The small text's size in panel px when one panel px is <paramref name="screenPxPerPanelPx"/> pixels on
        /// screen: the stylesheet's SmallTextPx, or the whole px that keeps it at TextFloorPx on screen if that is larger.</summary>
        public static float SmallTextSize(float screenPxPerPanelPx) =>
            screenPxPerPanelPx > 0f ? UnityEngine.Mathf.Max(SmallTextPx, UnityEngine.Mathf.Ceil(TextFloorPx / screenPxPerPanelPx - 0.01f)) : SmallTextPx;

        readonly List<Label> smallText;
        /// <summary>The plate's turn and date line (StoryOfTheLand.uss .date-row Label, 15 px): raised with the bar's small text (the
        /// owner's second answer, 15 October 2026). The plate keeps its width; the turn and the date take a line each, and the
        /// plate grows.</summary>
        readonly List<Label> plateText;
        readonly VisualElement? dateRow;
        /// <summary>The plate's padding while the floor raises its text (the stylesheet's is 2 px top and bottom, 10-12 at the
        /// sides): PlateSidePx at both sides keeps every line at least 0 px from the 18 px corner zones (TopBarTests), each the
        /// corner scrolls' reach (about 14 px in) plus 4, and PlateEdgePx at the top and bottom keeps it inside the frame's
        /// inner rule (7 px in). The plate grows to
        /// hold them (its height is a minimum), rather than shift its lines.</summary>
        public const float PlateSidePx = 18f, PlateEdgePx = 9f;
        readonly IVisualElementScheduledItem floorCheck;
        float smallTextSize = SmallTextPx;

        /// <summary>The left icon column (promo 03): the screens' buttons (ScreenNav.Screens).</summary>
        public const string RailName = "left-column";
        readonly HoverTip? railTips, barTips;
        /// <summary>The meters' explanations under the bar (#1037): their words and the turn's causes.</summary>
        readonly MeterTip? meterTip;

        /// <summary>The panels under the bar (StoryOfTheLand.uss: top 456 px under the three-row bar), which PlaceUnderBar moves
        /// down by what the three-row bar grows past its 440 px.</summary>
        readonly List<VisualElement> underBar;
        /// <summary>The bar's items, whose rows set the three-row bar's height (RowsHeight).</summary>
        readonly List<VisualElement> barItems;
        /// <summary>StoryOfTheLand.uss .hud-three-row .top-bar height.</summary>
        const float ThreeRowBarPx = 440f;
        /// <summary>The gap the panels keep under a plate that reaches them (PlateHang): the flat tier's, its panels' 104 px top
        /// under the stylesheet's 98 px plate.</summary>
        const float PlateGapPx = 6f;
        /// <summary>The left column, whose stylesheet top is the panels' (every tier gives them one), and the backdrop's band from
        /// the bar to that top (Game.uxml event-scrim-top), which PlaceUnderBar stretches when the panels move below the plate.</summary>
        readonly VisualElement? column, band;

        /// <param name="chapterEnded">Playtest (F6): End Turn on a finished chapter, which the sim would refuse, shows the
        /// chapter's end (GameRoot: the pause menu's chapter-end page) instead of going dim with no word; null keeps the press
        /// a plain End Turn.</param>
        /// <param name="decisionWaiting">End Turn while a card waits for an answer (SimBridge.Undecided), which never ends the
        /// turn: says so and opens the card (GameRoot). Null: the press does nothing.</param>
        public HudPresenter(VisualElement root, SimBridge bridge, UiText text, System.Action? chapterEnded = null, System.Action? decisionWaiting = null)
        {
            this.root = root; this.bridge = bridge; this.text = text;
            endTurn = root.Q<Button>("end-turn");
            this.chapterEnded = chapterEnded; this.decisionWaiting = decisionWaiting;
            onEndTurn = () =>
            {
                if (bridge.State.GameOver != null && chapterEnded != null) chapterEnded();
                else if (bridge.Undecided) decisionWaiting?.Invoke();
                else bridge.EndTurn();
            };
            endTurn.clicked += onEndTurn;
            endTurn.generateVisualContent += PaintHourglass;
            bar = root.Q<VisualElement>("top-bar");
            hudRoot = root.Q<VisualElement>("root");
            bar.RegisterCallback<GeometryChangedEvent>(OnBarGeometry);
            hudRoot.EnableInClassList(FamilyClass, true);
            familyMeters = root.Q<VisualElement>("family-meters");
            BuildFamilyMeters();
            meters = familyMeters.Children().ToList();
            nav = root.Q<VisualElement>("nav");
            foreach (var m in meters) m.RegisterCallback<GeometryChangedEvent>(OnMeterGeometry);
            foreach (var (name, key) in Texts) root.Q<TextElement>(name).text = text[key];
            // The left column's buttons are icon-only (HUD round 2, #398): their name, kept as their (clear) text, is also
            // their tooltip, which HoverTip shows beside the disc on hover and on focus.
            var rail = root.Q<VisualElement>(RailName)?.Query<Button>().ToList() ?? new List<Button>();
            foreach (var b in rail) b.tooltip = b.text;
            if (rail.Count > 0 && root.Q<Label>(HoverTip.TipName) != null) railTips = new HoverTip(root, rail, () => UiLanguage.Within(hudRoot) ? HoverTip.Side.Left : HoverTip.Side.Right);   // the column is at the right in Arabic
            // New Game in the bar is icon-only too (HUD round 2, part 3); its tip opens toward the bar's middle: left of it,
            // or right of it in the mirrored Arabic bar.
            var newGame = root.Q<Button>("new-game");
            if (newGame != null)
            {
                newGame.tooltip = newGame.text;
                if (root.Q<Label>(HoverTip.TipName) != null)
                    barTips = new HoverTip(root, new[] { newGame }, () => hudRoot.ClassListContains(UiLanguage.ArabicClass) ? HoverTip.Side.Right : HoverTip.Side.Left);
            }
            if (root.Q(MeterTip.TipName) != null) meterTip = new MeterTip(root, bridge, text, meters);
            // The text floor: checked now and twice a second, since a window resized at the same shape changes the panel's
            // scale without changing its layout (no geometry event).
            smallText = bar.Query<Label>(className: "meter-name").ToList();
            smallText.AddRange(bar.Query<Label>(className: ShortClass).ToList());   // the names under the icons keep the floor too
            underBar = new[] { "right-column", "left-column", "event-gutter", "event-layer", "screen", "toast" }
                .SelectMany(c => root.Query(className: c).ToList()).ToList();
            barItems = bar.Children().ToList();
            column = root.Q(RailName); band = root.Q("event-scrim-top");
            foreach (var k in barItems) k.RegisterCallback<GeometryChangedEvent>(OnRowGeometry);   // a row grows inside the fixed bar
            dateRow = bar.Q<VisualElement>(className: "date-row");
            plateText = dateRow?.Query<Label>().ToList() ?? new List<Label>();
            dateRow?.parent?.RegisterCallback<GeometryChangedEvent>(OnPlateGeometry);   // a wider raised plate needs room in the bar
            ApplyTextFloor();
            floorCheck = root.schedule.Execute(ApplyTextFloor).Every(500);
            SetCompact(bar.layout.width);   // the bar may already be laid out (the loading screen shows it) and never resize
            bridge.Changed += Render;
            Render();
        }

        /// <summary>Unsubscribes from the bridge and the button (the owner calls this when it is disabled).</summary>
        public void Dispose()
        {
            railTips?.Dispose(); barTips?.Dispose(); meterTip?.Dispose();
            floorCheck.Pause();
            Pulse(false);
            foreach (var c in counts.Values) c.Pause();
            counts.Clear();
            foreach (var l in smallText) { l.style.fontSize = StyleKeyword.Null; l.style.whiteSpace = StyleKeyword.Null; l.style.maxWidth = StyleKeyword.Null; }
            foreach (var k in barItems) k.UnregisterCallback<GeometryChangedEvent>(OnRowGeometry);
            foreach (var e in underBar) e.style.marginTop = StyleKeyword.Null;
            bar.style.height = StyleKeyword.Null;
            if (band != null) { band.style.top = StyleKeyword.Null; band.style.height = StyleKeyword.Null; }
            foreach (var l in plateText) { l.style.fontSize = StyleKeyword.Null; l.style.whiteSpace = StyleKeyword.Null; }
            if (dateRow != null) { dateRow.style.flexDirection = StyleKeyword.Null; if (dateRow.parent != null) PadPlate(dateRow.parent, false); }
            bridge.Changed -= Render; endTurn.clicked -= onEndTurn; endTurn.generateVisualContent -= PaintHourglass;
            bar.UnregisterCallback<GeometryChangedEvent>(OnBarGeometry);
            dateRow?.parent?.UnregisterCallback<GeometryChangedEvent>(OnPlateGeometry);
            foreach (var m in meters) m.UnregisterCallback<GeometryChangedEvent>(OnMeterGeometry);
            bar.RemoveFromClassList(RoomyClass); bar.RemoveFromClassList(IconRowClass); hudRoot.RemoveFromClassList(IconBarRootClass);
            bar.RemoveFromClassList(CompactClass); hudRoot.RemoveFromClassList(NarrowClass); hudRoot.RemoveFromClassList(FlatClass); hudRoot.RemoveFromClassList(TwoRowClass); hudRoot.RemoveFromClassList(ThreeRowClass);
            hudRoot.RemoveFromClassList(FamilyClass);
            familyMeters.Clear();
            ScreenNav.DisableCovered(hudRoot);
        }

        // The bar's width does not depend on the class, so this settles in one pass.
        void OnBarGeometry(GeometryChangedEvent e) => SetCompact(e.newRect.width);

        // The raised plate sizes to its widest line (PadPlate), so the tiers re-read the room it takes (FloorGrowth). The
        // plate's width does not depend on the tier, so this settles in one pass.
        void OnPlateGeometry(GeometryChangedEvent e) { if (smallTextSize > SmallTextPx && e.oldRect.width != e.newRect.width) SetCompact(bar.layout.width); }

        void OnMeterGeometry(GeometryChangedEvent e) => UpdateRoomy();

        // The rows' height does not depend on the bar's (RowsHeight), so this settles in one pass; the plate is one of the items
        // (PlateHang).
        void OnRowGeometry(GeometryChangedEvent e) => PlaceUnderBar();

        /// <summary>Roomy only on the one-row bar with its icons and captions; see RoomyClass.</summary>
        void UpdateRoomy()
        {
            var meters = this.meters.Where(m => m.resolvedStyle.display != DisplayStyle.None).ToList();   // a hidden meter takes no room
            if (meters.Count == 0) return;
            var oneRow = !hudRoot.ClassListContains(NarrowClass) && !bar.ClassListContains(CompactClass) && !bar.ClassListContains(IconRowClass);
            // Panel coordinates (logical px), so the meters and the screen buttons compare directly.
            float first = meters[0].worldBound.xMin, last = meters[meters.Count - 1].worldBound.xMax, limit = nav.worldBound.xMin;
            if (!oneRow || float.IsNaN(first) || float.IsNaN(limit)) { bar.RemoveFromClassList(RoomyClass); return; }
            var used = last - first; var spare = limit - last;
            if (bar.ClassListContains(RoomyClass))
            {
                if (spare < SpareShare * used) bar.RemoveFromClassList(RoomyClass);
            }
            else
            {
                var values = meters.Sum(m => m.Q<Label>(className: "meter-value")?.worldBound.width ?? 0f);
                var captions = meters.Sum(m =>
                {
                    var c = m.Q<Label>(className: "meter-name");
                    if (c == null || string.IsNullOrEmpty(c.text)) return 0f;
                    var oneLine = c.MeasureTextSize(c.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                    return UnityEngine.Mathf.Max(0f, oneLine - c.worldBound.width);
                });
                if (spare - RoomyGrowth(values, captions, meters.Count) >= SpareShare * used + 8f) bar.AddToClassList(RoomyClass);
            }
        }

        /// <summary>Sets the small text to SmallTextSize for the panel's current scale and re-picks the tier, since larger
        /// captions need more room (TierWidth).</summary>
        void ApplyTextFloor()
        {
            var p = root.panel;
            if (p == null) return;
            var size = SmallTextSize(p.scaledPixelsPerPoint / DevicePixelRatio());
            if (size == smallTextSize) return;
            smallTextSize = size;
            SetCompact(bar.layout.width);
        }

        /// <summary>Whether the floor raises the text at this bar width: at every tier, the three-row bar of a phone held upright
        /// (about 0.49 scale, 25 px) included (#642).</summary>
        bool FloorRaises(float width) => smallTextSize > SmallTextPx;

        /// <summary>The room the raised text takes beyond what the tiers were set for (the 15 px captions, wrapped at their 120 px
        /// max-width, and the 230 px plate): the plate widened for its raised lines and, with <paramref name="captions"/>, each
        /// caption on one line at the larger size (text width scales with font size). The tiers read the bar's width less
        /// this; the icon-first bar shows no caption, so its edge reads it without them.</summary>
        float FloorGrowth(bool captions)
        {
            float growth = RaisedPlateWidth() - PlateWidthPx;
            if (captions) foreach (var l in smallText)
            {
                if (string.IsNullOrEmpty(l.text) || l.ClassListContains(ShortClass)) continue;   // the captions' room only
                var current = l.resolvedStyle.fontSize;
                if (!(current > 0f)) continue;
                var oneLine = l.MeasureTextSize(l.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                growth += oneLine * smallTextSize / current - UnityEngine.Mathf.Min(oneLine * SmallTextPx / current, CaptionMaxPx);
            }
            return UnityEngine.Mathf.Max(0f, growth);
        }
        // StoryOfTheLand.uss .meter .meter-name max-width (170 on the three-row bar, where the tier is fixed), .date-box width.
        const float CaptionMaxPx = 120f, PlateWidthPx = 230f;
        /// <summary>The widest date line's width per px of font size, with a little to spare: the Arabic 31 October 1956 is
        /// about 7.8 times its size (196 px at 25 px), the English "30 September 1956" 7.2 (TopBarScoreLayoutTests.WorstDate).</summary>
        const float DateLinePerPx = 8f;

        /// <summary>The raised plate's laid-out width (PadPlate lets it size to its widest line, at least the stylesheet's 230 px;
        /// at 23 px, 960x600, the date is wider), or 230 before it is laid out.</summary>
        float RaisedPlateWidth()
        {
            var w = dateRow?.parent?.layout.width ?? float.NaN;
            return w > PlateWidthPx ? w : PlateWidthPx;
        }

        /// <summary>The largest raised small text the 70 px one-row bar holds: at 19 px (1366x657) the Arabic turn box left the
        /// bar (TopBarTests.TheBarFitsEveryWindowInEnglishAndArabic), so above this the bar takes the stacked tier.</summary>
        public const float OneRowSmallMaxPx = 18f;

        /// <summary>Inline styles for the floor, only while it raises the text: its size; the captions on one line on the one-row
        /// and stacked bars (a wrapped caption at the larger size would leave the bar), where FloorGrowth has made room (the
        /// two-row bar keeps the stylesheet's wrapping).</summary>
        void StyleSmallText(bool raised)
        {
            bool oneLine = raised && !hudRoot.ClassListContains(TwoRowClass);
            foreach (var l in smallText)
            {
                l.style.fontSize = raised ? new StyleLength(smallTextSize) : new StyleLength(StyleKeyword.Null);
                l.style.whiteSpace = oneLine ? new StyleEnum<WhiteSpace>(WhiteSpace.NoWrap) : new StyleEnum<WhiteSpace>(StyleKeyword.Null);
                l.style.maxWidth = oneLine ? new StyleLength(StyleKeyword.None) : new StyleLength(StyleKeyword.Null);
            }
            foreach (var l in plateText)
            {
                l.style.fontSize = raised ? new StyleLength(smallTextSize) : new StyleLength(StyleKeyword.Null);
                // One line each; the plate widens for them (PadPlate).
                l.style.whiteSpace = raised ? new StyleEnum<WhiteSpace>(WhiteSpace.NoWrap) : new StyleEnum<WhiteSpace>(StyleKeyword.Null);
            }
            // Raised, the turn and the date take a line each (a raised date squeezed beside the turn was clipped in Arabic),
            // and the plate's padding keeps every line inside its gilt frame's inner rule and at least 0 px from the 18 px corner
            // zones, which keep 4 px clear of the scrolls (PlateSidePx, PlateEdgePx; reviews of #624 in WebGL); the plate grows
            // to hold them.
            if (dateRow != null)
            {
                dateRow.style.flexDirection = raised ? new StyleEnum<FlexDirection>(FlexDirection.Column) : new StyleEnum<FlexDirection>(StyleKeyword.Null);
                if (dateRow.parent != null) PadPlate(dateRow.parent, raised);
            }
        }

        void PadPlate(VisualElement plate, bool raised)
        {
            // Raised, the plate sizes to its widest line (the turn and the date, a line each, never wrapped), at least 230 px.
            plate.style.width = raised ? new StyleLength(StyleKeyword.Auto) : new StyleLength(StyleKeyword.Null);
            // At least the widest date line too, so the plate keeps its width when the month changes (PromoHudTests): at 25 px
            // (a phone upright, #642) the Arabic 31 October 1956 is about 196 px, wider than 230 px less the padding.
            plate.style.minWidth = raised ? new StyleLength(UnityEngine.Mathf.Max(PlateWidthPx, DateLinePerPx * smallTextSize + 2f * PlateSidePx + 2f))
                                          : new StyleLength(StyleKeyword.Null);
            StyleLength Px(float v) => raised ? new StyleLength(v) : new StyleLength(StyleKeyword.Null);
            plate.style.paddingTop = Px(PlateEdgePx); plate.style.paddingBottom = Px(PlateEdgePx);
            plate.style.paddingLeft = Px(PlateSidePx); plate.style.paddingRight = Px(PlateSidePx);
        }

        /// <summary>The three-row bar takes its rows' height when that is more than its 440 px (StoryOfTheLand.uss), so the raised
        /// text of a smaller phone fits it (#642, the owner: "down to 375x667"), and the panels under it move down by what it
        /// grew: a top margin, not a top, so the stylesheet's other placements (the toast at the bottom beside a wide screen)
        /// still apply. Set here, not as height: auto in the stylesheet: there Yoga sized the wrapping bar as if each quarter-width
        /// meter took a line of its own (about 1300 px). Its items' geometry events re-run this (OnRowGeometry), and so does SetCompact.
        /// The panels also move below a raised plate that reaches them (PlateHang), and the backdrop's band then reaches down
        /// to them from the bar, so it still dims the hanging plate alone (TopBarTests.WithACardOpenTheBackdropMeetsTheBar).</summary>
        void PlaceUnderBar()
        {
            // A hidden bar (the start screen) moves nothing: the start screen's toast keeps its own top (review of #859).
            bool shown = bar.resolvedStyle.display != DisplayStyle.None;
            var rows = hudRoot.ClassListContains(ThreeRowClass) && shown ? RowsHeight() : 0f;
            var grown = rows - ThreeRowBarPx;
            bar.style.height = grown > 0.5f ? new StyleLength(rows) : new StyleLength(StyleKeyword.Null);
            var hang = shown ? PlateHang() : 0f;
            var push = UnityEngine.Mathf.Max(grown, hang);
            var margin = push > 0.5f ? new StyleLength(push) : new StyleLength(StyleKeyword.Null);
            foreach (var e in underBar) e.style.marginTop = margin;
            if (band == null || column == null) return;
            // The band spans the bar's bottom to the panels' top; the three-row bar grows with its panels, so only the plate's
            // push stretches it.
            var reach = hang > 0.5f && hang > grown ? column.resolvedStyle.top + hang - (bar.worldBound.yMax - hudRoot.worldBound.yMin) : 0f;
            band.style.top = reach > 0f ? new StyleLength(-reach) : new StyleLength(StyleKeyword.Null);
            band.style.height = reach > 0f ? new StyleLength(reach) : new StyleLength(StyleKeyword.Null);
        }

        /// <summary>How far the panels under the bar move down so that they start PlateGapPx below the hanging date plate, when
        /// the plate reaches their stylesheet top: the raised plate (two padded lines, StyleSmallText) is about 109 px tall at
        /// 1366x657, where the flat bar's panels start at 104 (TopBarTests, LeftColumnTests). 0 when it does not reach them,
        /// or before layout.</summary>
        float PlateHang()
        {
            var plate = dateRow?.parent;
            if (plate == null || column == null) return 0f;
            float bottom = plate.worldBound.yMax - hudRoot.worldBound.yMin, top = column.resolvedStyle.top;
            return bottom > top ? bottom + PlateGapPx - top : 0f;   // false for NaN
        }

        /// <summary>The height the bar's rows take: each row's tallest item (its children laid out in the flow, grouped by the
        /// rows they share), with the bar's padding and border. It does not depend on the bar's own height (the rows' spacing,
        /// align-content, is not counted), so a bar set to it keeps it. NaN before layout.</summary>
        float RowsHeight()
        {
            float total = 0f, top = float.NaN, bottom = float.NaN;
            foreach (var k in bar.Children().Where(k => k.resolvedStyle.position == Position.Relative && k.resolvedStyle.display != DisplayStyle.None).OrderBy(k => k.layout.y))
            {
                float t = k.layout.y - k.resolvedStyle.marginTop, b = k.layout.yMax + k.resolvedStyle.marginBottom;
                if (t < bottom) { top = UnityEngine.Mathf.Min(top, t); bottom = UnityEngine.Mathf.Max(bottom, b); continue; }   // the same row
                if (bottom > top) total += bottom - top;
                top = t; bottom = b;
            }
            if (bottom > top) total += bottom - top;
            var s = bar.resolvedStyle;
            return total + s.paddingTop + s.paddingBottom + s.borderTopWidth + s.borderBottomWidth;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // The panel scales by the canvas's device pixels, so a screen pixel here is a CSS pixel: the device's pixels / the
        // browser's devicePixelRatio (DevicePixelRatio.jslib).
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern float StoryOfTheLandDevicePixelRatio();
        static float DevicePixelRatio() { var r = StoryOfTheLandDevicePixelRatio(); return r > 0f ? r : 1f; }
#else
        static float DevicePixelRatio() => 1f;
#endif

        void SetCompact(float width)
        {
            if (!(width > 0)) return;   // NaN before layout: wait for it
            var raised = FloorRaises(width);
            var tierWidth = raised ? width - FloorGrowth(captions: true) : width;   // the owner's text floor needs room (#624)
            var iconWidth = raised ? width - FloorGrowth(captions: false) : width;
            bool tooTall = raised && smallTextSize > OneRowSmallMaxPx;
            bool captioned = !tooTall && tierWidth >= NarrowBelowPx, stacked = !captioned && (tooTall || iconWidth < IconRowBelowPx);
            bar.EnableInClassList(CompactClass, stacked && tierWidth < CompactBelowPx);
            bar.EnableInClassList(IconRowClass, !captioned && !stacked);
            hudRoot.EnableInClassList(IconBarRootClass, !captioned && !stacked);
            hudRoot.EnableInClassList(NarrowClass, stacked);
            hudRoot.EnableInClassList(FlatClass, stacked && width >= TwoRowBelowPx && hudRoot.layout.height < FlatBelowPx);
            hudRoot.EnableInClassList(TwoRowClass, width < TwoRowBelowPx);
            hudRoot.EnableInClassList(ThreeRowClass, width < ThreeRowBelowPx);
            StyleSmallText(raised);
            PlaceUnderBar();
            ScreenNav.DisableCovered(hudRoot);   // two rows or one: what an open screen covers changes with it (#400)
        }

        /// <summary>End Turn's hourglass, in the button's left padding (StoryOfTheLand.uss .end-turn), painted into the button so it
        /// stays a leaf sized by its text (UiIcon.Paint).</summary>
        static void PaintHourglass(MeshGenerationContext m)
        {
            if (!m.visualElement.enabledInHierarchy) return;   // no bright sign on a disabled End Turn (game over)
            UiIcon.Paint(m, "hourglass", new UnityEngine.Rect(UiLanguage.Within(m.visualElement) ? m.visualElement.layout.width - 32f : 10f,
                (m.visualElement.layout.height - 22f) / 2f, 22f, 22f), UiIcon.IsFocused(m.visualElement));   // the right side in Arabic (#193)
        }

        void Set(string name, string value) => root.Q<Label>(name).text = value;

        /// <summary>The family bar's meters, made from the chapter's resources in model.yaml's order, with the money beside
        /// Livelihood. Each is built as Game.uxml builds a meter (icon, caption, value and change).</summary>
        void BuildFamilyMeters()
        {
            bool arabic = FamilyGame.Arabic(text);
            familyMeters.Clear();
            foreach (var i in bridge.Content.Indicators)
            {
                var caption = FamilyGame.Resource(bridge.Content, i.Id, arabic);
                var meter = new VisualElement { name = "meter-family-" + i.Id };
                meter.AddToClassList("meter");
                meter.Add(new UiIcon { Glyph = FamilyGlyphs.TryGetValue(i.Id, out var g) ? g : "region" }.WithClass("meter-icon"));
                var body = new VisualElement().WithClass("meter-body");
                body.Add(new Label(caption).WithClass("meter-name"));
                var row = new VisualElement().WithClass("meter-row");
                row.Add(new Label { name = "family-" + i.Id + "-value" }.WithClass("meter-value"));
                row.Add(new Label { name = "family-" + i.Id + "-delta" }.WithClass("meter-delta"));
                body.Add(row);
                meter.Add(body);
                // Playtest 2: on the icon-first bar each meter's name shows under it, small (StoryOfTheLand.uss .meter-short).
                meter.Add(new Label(caption) { name = "family-" + i.Id + "-short", pickingMode = PickingMode.Ignore }.WithClass(ShortClass));
                familyMeters.Add(meter);
                familySaid.Add((i.Id, meter, caption));
                // The owner's choice (a), 2026-10-21: the money in use is no meter of its own (the family's model has no amount
                // of money; a bare sign read as a missing number): Livelihood carries its sign, "Livelihood · £P", and its
                // tooltip the money's name, sign, smaller unit and note (Render).
                if (i.Id == MoneyMeter) meter.AddToClassList(MoneyClass);
            }
        }

        /// <summary>The bar (F5 step 4): each resource's level and change, said in its tooltip (HudModel.Spoken); the money in
        /// use; the sim's date in the UI's language.</summary>
        public void Render()
        {
            var c = bridge.Content; var s = bridge.State; var prev = bridge.Session.Previous;
            bool arabic = FamilyGame.Arabic(text);
            Set("year", s.Date.Year.ToString(CultureInfo.InvariantCulture)); Set("turn", text.Format("hud.turn", s.Turn));
            // Playtest 3: a turn is a month and brings that month's cards (a 30 December card on the December turn), so the
            // plate names the month, not its first day.
            Set("date", text.Format("hud.date", PlayerText.Month(s.Date, arabic)));
            foreach (var (id, element, caption) in familySaid)
            {
                var m = HudModel.Level(text, s.Indicators[id], prev == null ? (double?)null : prev.Indicators[id]);
                var valueLabel = element.Q<Label>("family-" + id + "-value");
                var points = (int)PlayerText.ShownPoints(s.Indicators[id]);
                // F7: with motion on, a value that moved counts to its new number and flashes green or red (the end state at once
                // otherwise, and in tests: SettingsPresenter.MotionClass is set only in a player or Play mode).
                if (Motion && shownPoints.TryGetValue(id, out var was) && was != points) CountTo(valueLabel, was, points);
                else valueLabel.text = m.Value;
                shownPoints[id] = points;
                var delta = element.Q<Label>("family-" + id + "-delta");
                delta.text = m.Delta;
                delta.EnableInClassList("delta-up", m.Sign > 0);
                delta.EnableInClassList("delta-down", m.Sign < 0);
                element.tooltip = HudModel.Spoken(text, caption, m);
                if (id != MoneyMeter) continue;
                // The money in use, by the era and the family's region (Family.CurrencyAt), beside Livelihood's name.
                var sign = CurrencySign(c, s, arabic);
                var named = sign.Length > 0 ? text.Format("hud.livelihood_money", caption, sign) : caption;
                element.Q<Label>(className: "meter-name").text = named;
                element.Q<Label>(className: ShortClass).text = named;
                var tip = CurrencyTip(c, s, text);
                if (tip.Length > 0) element.tooltip += " " + tip;
            }
            // A finished chapter keeps End Turn pressable when it can say so (chapterEnded); without it, dim, with why in its tooltip.
            endTurn.SetEnabled(s.GameOver == null || chapterEnded != null);
            // A card without an answer: End Turn stays pressable (it keeps the focus) but dim, and says what it waits for.
            var undecided = bridge.Undecided;
            endTurn.EnableInClassList(WaitingClass, undecided);
            endTurn.tooltip = s.GameOver != null ? text["hud.end_turn_over"] : undecided ? text["hud.end_turn_wait"] : "";
            Pulse(Motion && s.GameOver == null && !undecided);
            meterTip?.Refresh();
        }

        /// <summary>F7: whether the HUD moves (the root's SettingsPresenter.MotionClass: a player or Play mode, reduce motion off).</summary>
        bool Motion => hudRoot.ClassListContains(SettingsPresenter.MotionClass);

        /// <summary>Each meter's value as last shown, in whole points, and its running count.</summary>
        readonly Dictionary<string, int> shownPoints = new Dictionary<string, int>();
        readonly Dictionary<Label, IVisualElementScheduledItem> counts = new Dictionary<Label, IVisualElementScheduledItem>();
        public const string FlashUpClass = "meter-flash-up", FlashDownClass = "meter-flash-down", GlowClass = "end-turn-glow", WaitingClass = "end-turn-waiting";
        const float CountSeconds = 0.5f;

        /// <summary>F7: counts <paramref name="label"/> from <paramref name="from"/> to <paramref name="to"/> points over
        /// CountSeconds, flashing it green (up) or red (down) meanwhile.</summary>
        void CountTo(Label label, int from, int to)
        {
            if (counts.TryGetValue(label, out var running)) running.Pause();
            var flash = to > from ? FlashUpClass : FlashDownClass;
            label.RemoveFromClassList(FlashUpClass); label.RemoveFromClassList(FlashDownClass);
            label.AddToClassList(flash);
            var start = UnityEngine.Time.realtimeSinceStartup;
            IVisualElementScheduledItem? item = null;
            item = label.schedule.Execute(() =>
            {
                var t = UnityEngine.Mathf.Clamp01((UnityEngine.Time.realtimeSinceStartup - start) / CountSeconds);
                label.text = PlayerText.Percent(UnityEngine.Mathf.Round(UnityEngine.Mathf.Lerp(from, to, t)) * 0.01);   // points back to a level
                if (t < 1f) return;
                label.RemoveFromClassList(flash);
                item?.Pause();
                counts.Remove(label);
            }).Every(30);
            counts[label] = item;
        }

        IVisualElementScheduledItem? pulse;
        int glowSteps;
        /// <summary>The glow's on and off steps before it stays off: each step is a 0.45 s transition the panel repaints every
        /// frame for, so an idle month must not keep one running (playtest 3: the frame rate fell while waiting on End Turn).</summary>
        public const int GlowSteps = 6;

        /// <summary>F7: End Turn glows on and off, three times, once every card is answered (nothing left but to end the month).</summary>
        void Pulse(bool on)
        {
            if (on && pulse == null)
            {
                glowSteps = 0;
                pulse = endTurn.schedule.Execute(() =>
                {
                    endTurn.ToggleInClassList(GlowClass);
                    if (++glowSteps < GlowSteps) return;
                    endTurn.RemoveFromClassList(GlowClass);
                    pulse?.Pause();   // kept (not null) until the month changes, so the next Render does not start it again
                }).Every(900);
            }
            if (on || pulse == null) return;
            pulse.Pause(); pulse = null;
            endTurn.RemoveFromClassList(GlowClass);
        }

        /// <summary>The money's short form in the bar: its sign (£P, ج.ف), or its name where it has no sign.</summary>
        public static string CurrencySign(Content c, GameState s, bool arabic)
        {
            var cur = Family.CurrencyAt(c, s);
            if (cur == null) return "";
            var sign = arabic ? cur.SymbolAr : cur.Symbol;
            return sign.Length > 0 ? sign : arabic ? cur.NameAr : cur.Name;
        }

        /// <summary>The money's tip: its name, sign and smaller unit (Family.CurrencyAt) in the UI's language; in English its
        /// note too (currency.tsv's notes are English only, so the Arabic tip has none).</summary>
        public static string CurrencyTip(Content c, GameState s, UiText t)
        {
            var cur = Family.CurrencyAt(c, s);
            if (cur == null) return "";
            if (FamilyGame.Arabic(t)) return t.Format("hud.currency_tip", cur.NameAr, cur.SymbolAr, cur.SubunitAr);
            var tip = t.Format("hud.currency_tip", cur.Name, cur.Symbol, cur.Subunit);
            return cur.Note.Length == 0 ? tip : tip + " " + cur.Note;
        }
    }
}
