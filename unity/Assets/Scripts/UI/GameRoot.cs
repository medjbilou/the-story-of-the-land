#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using StoryOfTheLand.Game.Boot;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>Starts the game once GameBoot has loaded the inputs and built the map (GameBoot.Ready), creates every screen,
    /// autosaves each turn, and wires the camera and the map pointer to the game panel (UiPointer.IsOverUi) and the minimap
    /// to the camera. Disabling it disposes all of that. The game is The Story of the Land's family game (docs/f5-unity-integration.md):
    /// in a player the start screen comes first; Continue is the family autosave (SaveStore.Family, its chapter on its first
    /// line) and New game opens the chapter and village picker (FamilyPickerPresenter), whose village starts the game
    /// (StartFamily). With no save, the default family game (<see cref="AutomatedChapter"/>, <see cref="AutomatedVillage"/>)
    /// stands behind the start screen until a village is picked; it never runs a turn.
    /// The automated runs (no start screen: smoke, -shot, -golden, the frame and pixel-error profiles, -nostart, and the
    /// EditMode tests) play that default game with the golden seed, and never read or write the player's save. With "smoke"
    /// in the page URL (WebGL, e.g. ?smoke=5) or -smoke N (desktop) it plays N turns through the UI's own buttons and logs
    /// "SMOKE OK ..." (or "SMOKE FAIL ..."). ExecuteAlways only so that disabling it disposes the presenters in edit mode too
    /// (tests); GameBoot raises Ready in play mode and players only.</summary>
    [ExecuteAlways]
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>Turns a bare smoke flag ("?smoke", "-smoke") plays.</summary>
        internal const int DefaultSmokeTurns = 5;
        /// <summary>Maximum time after closing the start screen for the normal-media browser check's first card to become usable.
        /// This proposed budget needs maintainer sign-off (#909).</summary>
        internal const double FirstCardBudgetSeconds = 10;
        /// <summary>The family game the automated runs play, and the one behind the start screen when there is no save.</summary>
        public const string AutomatedChapter = "1947_1949", AutomatedVillage = "tantura";
        /// <summary>The chapter whose map shows the 1948 front lines and the 1949 armistice lines (MapRegions); the others hide them.</summary>
        public const string FrontLineChapter = "1947_1949";

        [SerializeField] GameBoot boot = null!;
        [SerializeField] UIDocument document = null!;
        [SerializeField] TextAsset strings = null!;
        [SerializeField] TextAsset stringsAr = null!;
        [SerializeField] MapScene map = null!;
        [SerializeField] MapPointer pointer = null!;
        [SerializeField] CameraController cameraController = null!;

        /// <summary>Everything Begin created that must be undone, disposed in reverse order.</summary>
        readonly List<IDisposable> owned = new List<IDisposable>();
        MinimapPresenter? minimap;
        /// <summary>The map's region layers (sub-districts, armistice, control; null before Begin): a test aid.</summary>
        internal MapRegions? Regions { get; private set; }
        MapLabelsPresenter? labels;
        ScreenNav? nav;
        StartScreenPresenter? start;
        /// <summary>Settings of the last Begin, whose Narration switch N flips; the chapter intros' player (F5 step 8), added by
        /// the first family game in Play mode or a player.</summary>
        SettingsPresenter? settings; Narrator? narrator;
        /// <summary>The music (MusicPlayer), added by the first Begin in Play mode or a player, not in a measuring run.</summary>
        MusicPlayer? music;
        /// <summary>The chapter and village picker, and the People screen.</summary>
        FamilyPickerPresenter? picker; PeoplePresenter? people;
        /// <summary>The pause menu (F6; null in a smoke run), and whether anything that takes Escape before it was open at the
        /// end of the last frame (Update's Escape).</summary>
        PausePresenter? pause; bool coveredLastFrame;
        /// <summary>The toast of the last Begin and the game-over line, for the end-turn key on a finished game.</summary>
        Action<string>? say; Action? clearToast; string gameOverLine = "";
        /// <summary>Tests: the pause menu of the last Begin.</summary>
        internal PausePresenter? Pause => pause;
        /// <summary>Tests: the picker of the last Begin (null in a smoke run).</summary>
        internal FamilyPickerPresenter? Picker => picker;
        /// <summary>The session whose village the camera framed (once a session: the language switch keeps the view).</summary>
        GameSession? framed;
        EventCardPresenter? card;
        /// <summary>Tests: the decision card of the last Begin.</summary>
        internal EventCardPresenter? Card => card;
        CameraRig? rig;
        /// <summary>Whether the last Begin was a smoke run: the language switch then leaves the UI as it is (SwitchLanguage).</summary>
        bool smokeRun;
        /// <summary>Whether the last Begin's game touches no save (an automated run, the launch check): the language switch
        /// keeps it so.</summary>
        bool noSave;

        public SimBridge? Bridge { get; private set; }
        /// <summary>Where the autosave lives; null (the default) = SaveStore.ForPlatform(). Tests set a temporary folder.</summary>
        internal SaveStore? Store { get; set; }
        /// <summary>The UI's language; null (the default) = UiLanguage.AtStart (a flag, else the choice kept in Settings).
        /// Tests set it, and so does Settings' language switch.</summary>
        internal string? Language { get; set; }
        /// <summary>Where the Settings options are kept; null (the default) = SettingsStore.ForPlatform() in Play mode and
        /// players, none outside Play mode (EditMode tests: the developer's own settings must not leak into a test). Tests set
        /// a temporary folder.</summary>
        internal SettingsStore? Settings { get; set; }
        SettingsStore? SettingsFile() => Settings ?? (Application.isPlaying ? SettingsStore.ForPlatform() : null);

        public void Configure(GameBoot boot, UIDocument document, TextAsset strings, TextAsset stringsAr,
            MapScene map, MapPointer pointer, CameraController cameraController)
        {
            var oldBoot = this.boot;
            this.boot = boot; this.document = document; this.strings = strings; this.stringsAr = stringsAr; this.map = map;
            this.pointer = pointer; this.cameraController = cameraController;
            // An enabled GameRoot is already subscribed to the old boot's Ready (OnEnable): move the subscription.
            if (!isActiveAndEnabled) return;
            if (oldBoot != null) { oldBoot.Ready -= Begin; oldBoot.Failed -= OnBootFailed; }
            if (this.boot != null) { this.boot.Ready += Begin; this.boot.Failed += OnBootFailed; }
        }

        void OnEnable() { if (boot != null) { boot.Ready += Begin; boot.Failed += OnBootFailed; } }

        /// <summary>The loading line in the UI's language from the first frame (Start runs after every OnEnable, so the
        /// document's tree exists).</summary>
        void Start() { if (Application.isPlaying) Started(); }

        /// <summary>Start's work: the loading line in the UI's language (ShowLoading), unless the boot has already failed or
        /// the game has already begun. GameBoot and GameRoot set no execution order, and on the desktop the load reads from
        /// disk, so a failure can land inside GameBoot.Start before this runs: its reason must stay (M3 final review I5).</summary>
        internal void Started()
        {
            if ((boot == null || boot.Error == null) && Bridge == null) ShowLoading();
        }

        /// <summary>A boot failure replaces the loading line with a short reason in the player's language; the loading state
        /// stays, so no half-built panel shows (M3 final review I5). <paramref name="reason"/> is an exception's English text
        /// (a file path, a content error): it stays in GameBoot.Error and the log, never on screen (#637).</summary>
        internal void OnBootFailed(string reason)
        {
            var text = ShowLoading();
            var line = text == null ? null : document.rootVisualElement.Q<Label>("loading");
            if (line != null) Say(line, text!.Format("boot.failed", text["boot.reason_other"]), text["ui.lang"] == UiLanguage.Arabic);
        }

        /// <summary>Puts the loading line in the UI's language (boot.loading, #193). In Arabic the root also gets its mark
        /// (UiLanguage.ArabicClass) now, before Begin, so the line and a boot failure are set in the Arabic face. Returns
        /// the strings, or null before the scene's fields are set.</summary>
        internal UiText? ShowLoading()
        {
            if (document == null || strings == null || stringsAr == null || document.rootVisualElement == null) return null;
            var text = Strings();
            var root = document.rootVisualElement;
            bool arabic = text["ui.lang"] == UiLanguage.Arabic;
            (root.Q("root") ?? root).EnableInClassList(UiLanguage.ArabicClass, arabic);
            var line = root.Q<Label>("loading");
            if (line != null) Say(line, text["boot.loading"], arabic);
            return text;
        }

        /// <summary>A loading-line text, with its own direction in Arabic (UiDirection is not running yet) and none in English.</summary>
        static void Say(Label line, string words, bool arabic)
        {
            line.text = words;
            line.languageDirection = !arabic ? LanguageDirection.Inherit : UiDirection.RightToLeft(words) ? LanguageDirection.RTL : LanguageDirection.LTR;
        }

        /// <summary>The strings of the UI's language (UiLanguage): strings.ar.yaml when Arabic is asked for or chosen, else English.</summary>
        UiText Strings() => UiLang() == UiLanguage.Arabic ? UiText.Parse(stringsAr.text, "strings.ar.yaml") : UiText.Parse(strings.text);

        string UiLang() => Language ?? UiLanguage.AtStart(SettingsFile()?.Read(SettingsPresenter.LanguageKey));

        void OnDisable()
        {
            if (boot != null) { boot.Ready -= Begin; boot.Failed -= OnBootFailed; }
            Shutdown();
        }

        // "smoke" as a whole query parameter: bare ("?smoke", "&smoke=") or with a number; it ends at '&', '#' or the end, so
        // "?smoke-x", "?smoker" and "?smoke=five" are not the flag: UrlFlags' whole-parameter rule, plus digits only (amendment A3).
        static readonly Regex SmokeQuery = new Regex(@"[?&]smoke(?:=(\d*))?(?=&|#|$)", RegexOptions.CultureInvariant);

        /// <summary>Smoke-test turn count: from the page URL on WebGL, else from the command line. "?smoke=N" / "-smoke N"
        /// play N turns (0 = off); a bare "?smoke" / "-smoke" (or "-smoke" followed by another flag) plays DefaultSmokeTurns;
        /// a value that is not a non-negative int (too big, negative, not a number) is off on both. 0 when not asked.</summary>
        internal static int SmokeTurns(string? url, string[] args, bool webgl)
        {
            if (webgl)
            {
                var m = SmokeQuery.Match(url ?? "");
                if (!m.Success) return 0;
                var digits = m.Groups[1].Value;
                return digits.Length == 0 ? DefaultSmokeTurns : int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
            }
            var i = Array.IndexOf(args, "-smoke");
            if (i < 0) return 0;
            if (i + 1 >= args.Length) return DefaultSmokeTurns;
            var value = args[i + 1];
            if (value.StartsWith("-", StringComparison.Ordinal) && !Regex.IsMatch(value, @"^-\d")) return DefaultSmokeTurns;   // the next flag
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var t) ? t : 0;
        }

        public static int SmokeTurns() =>
            SmokeTurns(Application.absoluteURL, Environment.GetCommandLineArgs(), Application.platform == RuntimePlatform.WebGLPlayer);

        /// <summary>GameBoot.Ready: starts the game, smoke-testing when the URL or command line asks, with the start screen
        /// unless an automated run skips it.</summary>
        public void Begin(GameInputs inputs)
        {
            Begin(inputs, SmokeTurns(), Application.isPlaying && !StartScreenPresenter.Skipped());
            // The launch check: "-family <chapter>:<village>" (desktop) or "?family=<chapter>:<village>" (WebGL) opens that
            // family game at once, as the picker would, without touching the player's family save.
            var asked = FamilyRequested(Application.absoluteURL, Environment.GetCommandLineArgs(), Application.platform == RuntimePlatform.WebGLPlayer);
            if (smokeRun || asked == null) return;
            var (chapter, village) = asked.Value;
            try { StartFamily(inputs, chapter, village, keepSave: true); }
            catch (Exception e) { Debug.LogError($"FAMILY FAIL {chapter}:{village}: {e.Message}"); }
        }

        static readonly Regex FamilyQuery = new Regex(@"[?&]family=([a-z0-9_]+):([a-z0-9_]+)(?=&|#|$)", RegexOptions.CultureInvariant);

        /// <summary>The family game a launch check asks for (<c>-family 1947_1949:tantura</c>), or null.</summary>
        internal static (string chapter, string village)? FamilyRequested(string? url, string[] args, bool webgl)
        {
            string? value = null;
            if (webgl) { var m = FamilyQuery.Match(url ?? ""); if (m.Success) value = m.Groups[1].Value + ":" + m.Groups[2].Value; }
            else { var i = Array.IndexOf(args, "-family"); if (i >= 0 && i + 1 < args.Length) value = args[i + 1]; }
            var parts = value?.Split(':');
            return parts != null && parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 ? (parts[0], parts[1]) : ((string, string)?)null;
        }

        /// <summary>The automated runs' game: <see cref="AutomatedChapter"/> for a family of <see cref="AutomatedVillage"/>.</summary>
        internal static GameSession AutomatedSession(GameInputs inputs, ulong seed) =>
            GameSession.New(inputs.Chapter(AutomatedChapter), seed, AutomatedVillage);

        /// <summary>Starts (or restarts) the game over the built map. <paramref name="smoke"/> &gt; 0 plays that many turns;
        /// <paramref name="startScreen"/> shows the start screen first, over the family autosave or the default game. Without
        /// it (the automated runs and the tests) the default game starts with the golden seed and no save is touched.
        /// <paramref name="keep"/> (Settings' language switch, a new family game) goes on with that bridge's game and its
        /// queued orders.</summary>
        /// <param name="keepSave">No autosave (FamilyRequested's launch check).</param>
        /// <param name="offerContinue">With <paramref name="keep"/> and the start screen: whether it offers Continue (the start
        /// screen's language toggle keeps what the screen offered).</param>
        internal void Begin(GameInputs inputs, int smoke, bool startScreen = false, SimBridge? keep = null, bool keepSave = false, bool offerContinue = true)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            foreach (var (value, field) in new (UnityEngine.Object?, string)[] { (document, nameof(document)), (strings, nameof(strings)), (stringsAr, nameof(stringsAr)),
                (map, nameof(map)), (pointer, nameof(pointer)), (cameraController, nameof(cameraController)) })
                if (value == null) throw new InvalidOperationException($"GameRoot: {field} is not assigned (run MapCapture.MakeScene)");
            if (map.Terrain == null) throw new InvalidOperationException("GameRoot: the map is not built (GameBoot builds it before Ready)");
            Shutdown();
            smokeRun = smoke > 0;
            bool showsStart = startScreen && smoke == 0;   // a smoke run has no New game, so no start screen

            var text = Strings();
            var store = Store ?? SaveStore.ForPlatform();
            var notes = new List<string>();
            bool resumed = false;
            GameSession session;
            if (keep != null) { session = keep.Session; resumed = showsStart && offerContinue; }   // the pause menu's Main menu: Continue goes back to it
            else if (showsStart)
            {
                var continued = GameStart.ContinueFamily(inputs.Chapter, store.Family, notes, text);
                resumed = continued != null;
                session = continued ?? AutomatedSession(inputs, GameStart.NewSeed());
            }
            else { session = AutomatedSession(inputs, GoldenRun.Seed); keepSave = true; }   // reproducible, and never the player's save
            noSave = keepSave;
            DiagLog.BootPhase($"session started (turn {session.State.Turn}, replayed)");
            var bridge = Bridge = new SimBridge(session, text);
            if (keep != null) foreach (var order in keep.Queued) bridge.Queue(order);
            var root = document.rootVisualElement;
            bool arabic = text["ui.lang"] == UiLanguage.Arabic;
            // Arabic: the Arabic face, the advanced text generator and the mirrored rows (.lang-ar); each text's paragraph
            // direction comes from its own text (UiDirection, made below once the presenters have rendered).
            (root.Q("root") ?? root).EnableInClassList(UiLanguage.ArabicClass, arabic);   // on .root, whose face it replaces
            var loading = root.Q<Label>("loading");
            if (loading != null) Say(loading, text["boot.loading"], arabic);   // hidden from here on; set for a restart in the other language

            // The toast first: the autosave (below) must report a failed write after the toast has shown the turn's refusals.
            var toast = Own(new ToastPresenter(root, bridge, text));
            say = toast.Show; clearToast = toast.Clear; gameOverLine = text["order.game_over"];
            var chapter = FamilyGame.ChapterOf(session.Content);
            // The People screen, whose left-column button the bar's tips and ScreenNav must find.
            var figures = Figures.Parse(inputs.Read(Figures.FileName));
            people = Own(new PeoplePresenter(root, figures, chapter, text));
            // End Turn on a finished chapter shows its end; with a card unanswered it says so and opens the card.
            Own(new HudPresenter(root, bridge, text, () => ShowChapterEnd(inputs), () => { say?.Invoke(text["hud.end_turn_wait"]); card?.Reopen(); }));
            nav = Own(new ScreenNav(root));                                      // every screen starts hidden (USS); ScreenNav opens one at a time
            Own(new PointerBlur(root));                                    // a mouse click leaves no focus fill behind (keyboard keeps it)
            Own(new FocusScroll(root));                                    // keyboard focus scrolls its ScrollView to it (a11y H1)
            Own(new NavigationGuard(root));                                // the camera keys (WASD, arrows) never move UI focus (B6)
            if (DiagLog.Nav) Own(new NavEventLog(root));                   // ?navlog: every UI pointer/submit event (playtest 2, P2-4)
            card = Own(new EventCardPresenter(root, bridge, text, underStartScreen: showsStart,
                // A screenshot run (?shot) captures the map: the opening card would cover it.
                hiddenInCapture: EventCardPresenter.HiddenInCaptures(Application.absoluteURL, Environment.GetCommandLineArgs(),
                    Application.platform == RuntimePlatform.WebGLPlayer),
                // A card's people open the People screen at the figure.
                figuresFor: figures.For, openFigure: id => { nav?.Open(PeoplePresenter.Screen); people?.ShowFigure(id); },
                // The clips the owner watched and signed off (docs/footage-review.md): "Watch footage", after the content note.
                clipsFor: Footage.Parse(inputs.Read(Footage.FileName)).For));
            Own(new WhyPresenter(root, bridge, text));
            Own(new HowToPlayPresenter(root, text));
            // Presentation only (#40): never the sim or the save. Outside Play mode (EditMode tests) no file: the developer's
            // own settings must not leak into a test. Each Begin reads the file again.
            // The language switch (#1036) builds every screen again in the new language over this game (SwitchLanguage).
            settings = Own(new SettingsPresenter(root, text, SettingsFile(), language => SwitchLanguage(inputs, language), document.panelSettings));
            // F5 step 9: the credits (docs/credits.md), opened from Settings.
            Own(new CreditsPresenter(root, Credits.Parse(inputs.Read(Credits.FileName)), text, nav!));
            // The 1948 sub-districts and front lines and each front's 1949 armistice line (display only: the sim never reads
            // them; docs/visual-v1.md), drawn only in the 1947-1949 chapter, with the legend that names its sources.
            var regions = new MapRegions(map.SubdistrictsFor(inputs.Map.RegionsTsv, inputs.Map.RegionsYaml, Debug.LogWarning),
                string.IsNullOrWhiteSpace(inputs.Map.ArmisticeYaml) ? null : Armistice1949.ReadRuntimeOrYaml(inputs.Map.ArmisticeTsv, inputs.Map.ArmisticeYaml, Debug.LogWarning),
                map.Control);   // the map's read, with the map editor's control overrides (#1009); the road vehicles use it too (#725)
            Regions = regions;
            bool frontLines = chapter == FrontLineChapter;
            var layerLabel = root.Q<Label>("map-layer");
            // The occupation's layers, June 1967 on (display only), drawn by date in every chapter; their legend takes the label
            // outside the 1947-1949 chapter.
            var occupation = map.Occupation;
            var damage = map.Damage;   // the destruction of Gaza from October 2023, as UNOSAT's images record it
            string? occupationLabel = null;
            Action onDate = () =>
            {
                occupation?.Show(bridge.State.Date);
                damage?.Show(bridge.State.Date);
                if (!frontLines)
                {
                    if (MapBorders.Current != null) MapBorders.Use(null);
                    occupationLabel = occupation?.Label(bridge.State.Date, k => text[k]);
                    var war = damage?.Label(bridge.State.Date, k => text[k]);
                    if (war != null) occupationLabel = occupationLabel == null ? war : occupationLabel + "\n" + war;
                    if (occupationLabel != null) layerLabel.text = occupationLabel;
                    ShowLayerLabel();
                    return;
                }
                regions.Apply(bridge.State.Date);
                layerLabel.text = regions.ControlLabel(bridge.State.Date, k => text[k])
                    ?? regions.Label(bridge.State.Date, text.Format);
                ShowLayerLabel();
            };
            // The caption names the borders while they are drawn: by date, and not while the minimap's button hides them.
            void ShowLayerLabel() => layerLabel.style.display = (frontLines ? regions.Shown && minimap?.BordersShown != false : occupationLabel != null)
                ? DisplayStyle.Flex : DisplayStyle.None;
            onDate();
            bridge.Changed += onDate;
            owned.Add(new Undo(() => bridge.Changed -= onDate));
            DiagLog.BootPhase("screens wired");
            // A smoke session never touches the player's save: no autosave, and no New Game (which deletes the autosave).
            NewGamePresenter? newGame = null;
            if (smoke == 0)
            {
                // New game (bar or start screen) opens the chapter and village picker.
                picker = Own(new FamilyPickerPresenter(root, text, inputs.Chapter, (c, village) => StartFamily(inputs, c, village)));
                // Playtest 2: New game over a game in progress asks first (the pause menu's confirm page, made below).
                newGame = Own(new NewGamePresenter(root, bridge, toast, text, OpenPicker,
                    then => { if (pause != null) pause.Confirm(text["restart.confirm"], text["restart.yes"], then); else then(); }));
                // appends: the turn's refusals stay visible.
                if (!keepSave) Own(GameStart.Autosave(bridge, store.Family, text, toast.Append));
                // F6: the pause menu, its save slots beside the family autosave. While it shows the camera keys are off.
                var pauseCam = cameraController;
                pause = Own(new PausePresenter(root, text, nav!, store.Family, inputs.Chapter, () => bridge.Session, s => LoadGame(inputs, s),
                    () => MainMenu(inputs), Application.platform == RuntimePlatform.WebGLPlayer ? null : (Action)Quit, toast.Show,
                    shown => { if (pauseCam != null) pauseCam.enabled = !shown; }, chapters: OpenPicker));
                // Playtest (F6): a month with nothing to decide says so; the chapter's last month shows its end.
                // Playtest 2: and offers to skip to the next event (SkipAhead), the button hidden again by any change.
                var skip = skipButton = root.Q<Button>(SkipName);
                Action<IReadOnlyList<Rejection>> afterTurn = r =>
                {
                    // Playtest 3: Home or Safety falling to 0% is said once, on the turn it happens (the game goes on).
                    foreach (var line in HudModel.ZeroLines(text, bridge.Session.Previous, bridge.State)) toast.Append(line);
                    if (bridge.State.GameOver != null) ShowChapterEnd(inputs);
                    else if (!skipping && r.Count == 0 && bridge.State.PendingEvents.Count == 0)
                    {
                        toast.Append(text["toast.quiet"]);
                        skip?.AddToClassList(SkipShownClass);
                    }
                };
                bridge.TurnEnded += afterTurn;
                owned.Add(new Undo(() => bridge.TurnEnded -= afterTurn));
                if (skip != null)
                {
                    skip.text = text["skip.button"];
                    skip.RemoveFromClassList(SkipShownClass);
                    Action onSkip = SkipAhead;
                    Action hideSkip = () => { if (!skipping) skip.RemoveFromClassList(SkipShownClass); };   // before TurnEnded: a quiet turn shows it again
                    skip.clicked += onSkip;
                    bridge.Changed += hideSkip;
                    owned.Add(new Undo(() => { skip.clicked -= onSkip; bridge.Changed -= hideSkip; skip.RemoveFromClassList(SkipShownClass); }));
                }
            }

            Func<Vector2, bool> overUi = p => UiPointer.IsOverUi(root.panel, p, Screen.height);
            var terrain = map.Terrain;
            var size = terrain.terrainData.size;
            var oldBounds = cameraController.Bounds;
            var oldGround = cameraController.Ground; var oldTop = cameraController.GroundTop;
            cameraController.IsOverUi = overUi;
            cameraController.Bounds = new Rect(terrain.transform.position.x, terrain.transform.position.z, size.x, size.z);
            cameraController.Ground = CameraController.GroundOf(terrain);
            cameraController.GroundTop = CameraController.GroundTopOf(terrain);
            pointer.Map = map;
            pointer.IsOverUi = overUi;
            Action<int, int> onHex = (q, r) => map.ClearSelection();   // the family game gives no map orders: no hex stays selected
            pointer.HexClicked += onHex;
            var camRig = rig = cameraController.GetComponent<CameraRig>();
            // The game opens over the family's village (once a session: the language switch keeps the view).
            if (!ReferenceEquals(framed, session)) { framed = session; FrameVillage(map, camRig, session); cameraController.Settle(); }
            Own(new TopBarInset(root, camRig));                            // the map is framed below the top bar
            var mini = minimap = Own(new MinimapPresenter(root, map.Hexes, text));
            // Audit m14: the region borders are drawn only in the 1947-1949 chapter; elsewhere their button changed nothing, so
            // it is not offered.
            var bordersButton = root.Q<Button>("minimap-borders");
            if (bordersButton != null) bordersButton.style.display = frontLines ? DisplayStyle.Flex : DisplayStyle.None;
            var labelCamera = cameraController.GetComponent<Camera>();
            // The pills, and a locality's today's name, in the UI's language (name_ar, today_name_ar in Arabic); the
            // family's village marked.
            labels = Own(new MapLabelsPresenter(root, map, labelCamera, null,
                p => MapLabelsPresenter.TipOf(p, n => text.Format("map.today", arabic && p.TodayAr != null ? p.TodayAr : n), text["map.mixed"], arabic),
                () => seaAir != null ? seaAir.KeepClear : null,   // the names keep clear of the 1948 Collection's ships (#669)
                null, null, arabic, session.State.Village));
            // A village's dated fate, once it has come, as a badge under its name.
            var fateLabels = labels;
            // Playtest 3: a place founded after the game's date is neither drawn nor named.
            Action showFates = () =>
            {
                fateLabels.SetFates(FamilyGame.Fates(bridge.Content, bridge.State.Date, text));
                map.ShowFoundedBy(bridge.State.Date.Year); fateLabels.SetFoundedBy(bridge.State.Date.Year);
            };
            bridge.Changed += showFates;
            owned.Add(new Undo(() => bridge.Changed -= showFates));
            showFates();
            mini.Clicked += (lon, lat) => { camRig.LookAt = map.Frame.ToWorld(lon, lat, 0); cameraController.Settle(); SyncMinimap(); labels?.Refresh(); };   // the click may come after LateUpdate (review M6)
            // The minimap's whole-map button: the camera's own farthest zoom, over the middle of the map (no new rule).
            mini.Overview += () =>
            {
                var (lon, lat) = mini.Centre;
                camRig.LookAt = map.Frame.ToWorld(lon, lat, 0); camRig.Distance = CameraController.MaxDistance;
                cameraController.Settle(); SyncMinimap(); labels?.Refresh();
            };
            mini.BordersChanged += ShowLayerLabel;
            // Owner (2026-10-22): the pan buttons glide the view sideways by half its distance, along the screen's own left
            // and right (the camera's yaw), as the keys would.
            mini.Pan += dir => camRig.EaseTo(camRig.LookAt + Quaternion.Euler(0, camRig.Yaw, 0) * Vector3.right * (dir * PanShare * camRig.Distance));
            // F7: a card that comes up brings the camera to its place (GameEvent.Place), or to the family's village when it
            // names none, when that lies outside the middle of the view (CameraRig.EaseIntoView: a 0.6 s glide, a jump under
            // reduce motion, nothing in a measuring run).
            string? glidedFor = card?.Shown?.Id;
            var places = session.Content.Places;
            var home = places.FirstOrDefault(p => p.Id == session.State.Village);
            void GlideTo(Place p) { if (Application.isPlaying) camRig.EaseIntoView(map.Frame.ToWorld(p.Lon, p.Lat, 0)); }
            Action glide = () =>
            {
                var shown = card?.Shown;
                if (shown == null || card!.Collapsed || shown.Id == glidedFor) return;
                glidedFor = shown.Id;
                var at = shown.Place != null ? places.FirstOrDefault(p => p.Id == shown.Place) : null;
                if ((at ?? home) is Place target) GlideTo(target);
            };
            bridge.Changed += glide;
            owned.Add(new Undo(() => bridge.Changed -= glide));
            // Owner (2026-10-22): a turn that changes the map shows it. A village whose fate came this month (its badge,
            // FamilyGame.Fates) takes the camera, the family's own village first, and the toast names it. TurnEnded comes
            // after Changed, so this glide wins over the new card's.
            Action<IReadOnlyList<Rejection>> showChange = _ =>
            {
                var before = bridge.Session.Previous;
                if (before == null) return;
                var was = FamilyGame.Fates(bridge.Content, before.Date, text);
                var changed = MapChanges(was, FamilyGame.Fates(bridge.Content, bridge.State.Date, text), bridge.State.Village)
                    .Select(c => (place: places.FirstOrDefault(p => p.Id == c.place), c.fate)).FirstOrDefault(c => c.place != null);
                if (changed.place == null) return;
                GlideTo(changed.place);
                toast.Append(text.Format("toast.map_changed", arabic && changed.place.NameAr.Length > 0 ? changed.place.NameAr : changed.place.Name, changed.fate));
            };
            bridge.TurnEnded += showChange;
            owned.Add(new Undo(() => bridge.TurnEnded -= showChange));
            Action onChanged = RenderScenery;
            bridge.Changed += onChanged;
            // One console line per turn, so a browser run (reload, Continue) can be checked against the hash.
            Action<IReadOnlyList<Rejection>> onTurn = _ =>
                Debug.Log(FormattableString.Invariant($"TURN {bridge.State.Turn} {bridge.State.Date:yyyy-MM-dd} hash {bridge.State.Hash()}"));
            bridge.TurnEnded += onTurn;
            logCamera = CameraLogRequested();
            loggedDistance = loggedYaw = float.NaN;
            owned.Add(new Undo(() =>
            {
                bridge.Changed -= onChanged;
                bridge.TurnEnded -= onTurn;
                pointer.HexClicked -= onHex;
                if (pointer.IsOverUi == overUi) pointer.IsOverUi = null;
                if (cameraController.IsOverUi == overUi) cameraController.IsOverUi = null;
                if (pointer.Map == map) pointer.Map = null;
                cameraController.Bounds = oldBounds;
                cameraController.Ground = oldGround; cameraController.GroundTop = oldTop;
            }));
            RenderScenery();
            SyncMinimap();
            // The start screen goes up in the same frame the loading state ends, so the HUD never flashes before it. While it
            // shows, the camera keys are off (keyboard pan would otherwise move the hidden map; plan amendment A4).
            if (showsStart)                                                // newGame is made above whenever smoke == 0
            {
                var cam = cameraController;
                start = Own(new StartScreenPresenter(root, newGame!, resumed, OpenPicker, shown => { if (cam != null) cam.enabled = !shown; }, text,
                    switchLanguage: language => StartLanguage(inputs, language), howToPlay: () => pause?.ShowHint()));
                start.Show();
            }
            if (arabic) Own(new UiDirection(root, bridge));
            root.Q("root")?.RemoveFromClassList("loading");                // ready: the bar, panels and screens may show (P2-1)
            DiagLog.BootPhase("screens ready");

            if (notes.Count > 0) toast.Show(notes);
            if (Application.isPlaying && music == null && !DiagLog.Automated)
            {
                music = gameObject.AddComponent<MusicPlayer>();
                music.Wanted = MusicWanted;
                music.Narrating = () => narrator != null && narrator.Speaking;
            }
            Debug.Log(FormattableString.Invariant($"GAME OK {bridge.State.Date:yyyy-MM-dd} turn {bridge.State.Turn} hash {bridge.State.Hash()}"));
            // Unlike the other browser instruments, ?firstcard keeps the normal start screen. It closes the screen through its
            // real button immediately, then gives tools/web_check.py a bounded result to wait for (#910).
            if (Application.isPlaying && FirstCardCheckRequested())
                StartCoroutine(showsStart || firstCardBegan == null
                    ? FirstCardCheck(root, card!, Debug.Log)
                    : FirstCardWait(root, card!, Debug.Log, firstCardBegan.Value, () => Time.realtimeSinceStartupAsDouble));
            if (smoke > 0)
            {
                if (Application.isPlaying) StartCoroutine(Smoke(root, bridge, card!, smoke, Debug.Log));
                else Debug.LogWarning("SMOKE FAIL the smoke run needs play mode or a player");
            }
            if (Application.isPlaying && PixelErrorProfile.Requested()) StartCoroutine(PixelErrorProfile.Run(map.Terrain));
            // The border renderer's synthetic, labelled proof (?borderproof / -borderproof): development builds only.
            if (Application.isPlaying && BorderProof.Requested()) BorderProof.Show(map);
            // The visual track's instruments (docs/visual-v1.md): both move the camera, so one per run.
            if (Application.isPlaying && FrameProfile.Requested())
                StartCoroutine(FrameProfile.Run(camRig, map.Frame, Debug.Log, Application.platform != RuntimePlatform.WebGLPlayer));
            else if (Application.isPlaying && VisualShots.Requested()) StartCoroutine(VisualShots.Run(camRig, map.Frame, Debug.Log));
            else if (Application.isPlaying && Application.platform != RuntimePlatform.WebGLPlayer && PopCheck.Requested() is string popDir)
                StartCoroutine(PopCheck.Run(camRig, map.Frame, popDir, Debug.Log));   // F6 round 4: a frame series for pop-in
        }

        /// <summary>Settings' language switch (#1036): every screen again in <paramref name="language"/> at once, no reload,
        /// over the same game with this turn's orders (Begin's keep), and Settings open again with the focus on the choice
        /// just made (for a mouse press too: the rebuilt UI has no focus to keep). Not in a smoke run (smokeRun).</summary>
        void SwitchLanguage(GameInputs inputs, string language)
        {
            if (Bridge == null || smokeRun) return;
            Language = language;
            bool fromPause = pause?.Returning == true;
            Begin(inputs, 0, keep: Bridge, keepSave: noSave);
            nav?.Open("settings");
            if (fromPause) pause?.ReturnAfterScreen("settings");   // closing Settings goes back to the pause menu, as before
            document.rootVisualElement.Q<Button>("settings-language-" + language)?.Focus();
        }

        /// <summary>Playtest 3, the start screen's language toggle: the language is kept as Settings keeps it, and the screens
        /// are built again in it with the start screen up, offering what it offered, the focus on the language pressed.</summary>
        void StartLanguage(GameInputs inputs, string language)
        {
            if (Bridge == null || smokeRun) return;
            try { SettingsFile()?.Write(SettingsPresenter.LanguageKey, language); }
            catch (Exception e) { Debug.LogWarning("Settings: could not keep the language: " + e.Message); }
            Language = language;
            Begin(inputs, 0, startScreen: true, keep: Bridge, keepSave: noSave, offerContinue: start?.Resumed ?? true);
            document.rootVisualElement.Q<Button>("start-language-" + language)?.Focus();
        }

        /// <summary>F6, the pause menu's Load: <paramref name="session"/> (a slot or the autosave, replayed) is played from where
        /// it was saved, the screens built again over it, and it becomes the autosave, so Continue finds it.</summary>
        internal void LoadGame(GameInputs inputs, GameSession session)
        {
            Begin(inputs, 0, keep: new SimBridge(session, Strings()), keepSave: noSave);
            if (noSave) return;
            try { (Store ?? SaveStore.ForPlatform()).Family.WriteAutosave(GameStart.SaveText(session)); }
            catch (Exception e) { Debug.LogWarning("Family autosave could not be written: " + e); }
        }

        /// <summary>Writes the family autosave now (the pause menu's Main menu and Quit), unless this game touches no save.</summary>
        void SaveNow()
        {
            if (Bridge == null || noSave) return;
            try { (Store ?? SaveStore.ForPlatform()).Family.WriteAutosave(GameStart.SaveText(Bridge.Session)); }
            catch (Exception e) { Debug.LogWarning("Family autosave could not be written: " + e); }
        }

        /// <summary>F6, the pause menu's Main menu: the game is saved and the start screen shows over it, Continue going back
        /// to it.</summary>
        internal void MainMenu(GameInputs inputs)
        {
            if (Bridge == null) return;
            SaveNow();
            Begin(inputs, 0, startScreen: true, keep: Bridge, keepSave: noSave);
        }

        /// <summary>F6, the pause menu's Quit to desktop (not offered on the web): the game is saved, then the player quits.</summary>
        void Quit()
        {
            SaveNow();
            Application.Quit();
        }

        /// <summary>F5: opens the chapter and village picker (the start screen's and the bar's New game), with no old toast
        /// left over it (playtest: "Game saved in slot 1." sat on its search).</summary>
        void OpenPicker() { clearToast?.Invoke(); picker?.Open(); }

        /// <summary>Playtest 2, "Skip to the next event" after a quiet month: ends turns, one every SkipStepSeconds so the month
        /// can be seen going by on the toast, until a card comes, the chapter ends, the player presses Stop (the same button) or
        /// SkipCap months have passed; then says how many passed. In edit mode (tests) the months pass at once.</summary>
        internal void SkipAhead()
        {
            if (Bridge == null || skipButton == null) return;
            if (skipping) { stopSkip = true; return; }
            if (Application.isPlaying) StartCoroutine(SkipRun());
            else { var run = SkipRun(); while (run.MoveNext()) { } }
        }

        public const string SkipName = "skip-ahead", SkipShownClass = "skip-shown";
        /// <summary>Playtest 3: Skip runs until the next card (the 1880-1946 chapter has years without one); this only guards
        /// against a chapter that never brings one. The first year passes at SkipStepSeconds a month, longer gaps at SkipFastSeconds.</summary>
        public const int SkipCap = 1200;
        const float SkipStepSeconds = 0.2f, SkipFastSeconds = 0.05f;
        const int SkipSlowMonths = 12;
        Button? skipButton; bool skipping, stopSkip;

        IEnumerator SkipRun()
        {
            var bridge = Bridge!; var button = skipButton!; var text = Strings(); bool arabic = FamilyGame.Arabic(text);
            skipping = true; stopSkip = false;
            int months = 0;
            button.text = text["skip.stop"];
            button.AddToClassList(SkipShownClass);
            try
            {
                while (months < SkipCap && !stopSkip && bridge.State.GameOver == null && bridge.State.PendingEvents.Count == 0)
                {
                    bridge.EndTurn();
                    months++;
                    say?.Invoke(text.Format("skip.running", PlayerText.Month(bridge.State.Date, arabic)));   // the running month
                    if (Application.isPlaying) yield return new WaitForSecondsRealtime(months < SkipSlowMonths ? SkipStepSeconds : SkipFastSeconds);
                    if (Bridge != bridge) yield break;   // a load or a new game meanwhile
                }
            }
            finally
            {
                skipping = false;
                if (skipButton == button) { button.text = text["skip.button"]; button.RemoveFromClassList(SkipShownClass); }
            }
            if (months > 0 && bridge.State.GameOver == null) say?.Invoke(text.Plural("skip.passed", months));
            if (bridge.State.GameOver == null && bridge.State.PendingEvents.Count == 0) button.AddToClassList(SkipShownClass);   // still quiet (the cap): again
        }

        /// <summary>settings.txt's key for the first-turn hint, "seen" once shown.</summary>
        public const string HintKey = "hint";


        /// <summary>Playtest (F6): the chapter's end on the pause panel: the chapter, its years and the family's village, then Next
        /// chapter (the same village in the next chapter; the picker if that fails), Choose chapter (the picker) and Main menu.</summary>
        internal void ShowChapterEnd(GameInputs inputs)
        {
            if (pause == null || Bridge == null) return;
            var text = Strings(); bool arabic = FamilyGame.Arabic(text);
            var c = Bridge.Content; var s = Bridge.State;
            var id = FamilyGame.ChapterOf(c);
            int i = FamilyGame.Chapters.Select(x => x.Id).ToList().IndexOf(id);
            var place = c.Places.FirstOrDefault(p => p.Id == s.Village);
            var years = i >= 0 ? FamilyGame.Years(text, FamilyGame.Chapters[i].From, FamilyGame.Chapters[i].To) : "";
            var summary = text.Format("chapter_end.summary", text.Has("chapter." + id) ? text["chapter." + id] : id, years,
                place != null ? FamilyGame.Name(place, arabic) : s.Village);
            var village = s.Village;
            Action? next = i >= 0 && i + 1 < FamilyGame.Chapters.Count ? () =>
            {
                try { StartFamily(inputs, FamilyGame.Chapters[i + 1].Id, village); }
                catch (Exception e) { Debug.LogWarning($"Next chapter for {village}: {e.Message}"); OpenPicker(); }
            } : (Action?)null;
            pause.ShowChapterEnd(summary, next);
        }

        /// <summary>F5 step 3: a new family game in <paramref name="chapter"/> for a family of <paramref name="village"/>: the
        /// family autosave is replaced (the old one deleted first), the screens are built
        /// again over the new session, and its turn 0 is saved at once, so Continue finds it even before End Turn.</summary>
        /// <param name="keepSave">The launch check (FamilyRequested): the player's family save is neither deleted nor written.</param>
        internal void StartFamily(GameInputs inputs, string chapter, string village, bool keepSave = false)
        {
            var store = (Store ?? SaveStore.ForPlatform()).Family;
            var session = GameSession.New(inputs.Chapter(chapter), GameStart.NewSeed(), village);   // an unknown chapter or village throws here
            if (!keepSave)
                try { store.DeleteAutosave(); } catch (Exception e) { Debug.LogWarning("Family autosave could not be deleted: " + e); }
            Begin(inputs, 0, keep: new SimBridge(session, Strings()), keepSave: keepSave);
            // F5 step 8: the chapter opens with its narrated intro in the UI's language (none in EditMode: no player loop).
            if (Application.isPlaying) (narrator ??= gameObject.AddComponent<Narrator>()).Play(chapter, UiLang());
            if (keepSave) return;
            // Playtest (F6): a first game shows the hint (the goal, the bar's icons, End Turn) once per profile, kept in settings.txt.
            var settingsFile = SettingsFile();
            if (settingsFile != null && settingsFile.Read(HintKey) != "seen")
            {
                pause?.ShowHint();
                try { settingsFile.Write(HintKey, "seen"); }
                catch (Exception e) { Debug.LogWarning("Settings: could not keep the hint as seen: " + e.Message); }
            }
            try { store.WriteAutosave(GameStart.SaveText(session)); }
            catch (Exception e) { Debug.LogWarning("Family autosave could not be written: " + e); }
        }

        /// <summary>The music wanted now: the main theme while the start screen shows, else the game's chapter and month's
        /// theme (MusicPlayer.Theme); none before the first game.</summary>
        string? MusicWanted() => Bridge == null ? null
            : start?.IsOpen == true ? MusicPlayer.Main : MusicPlayer.Theme(FamilyGame.ChapterOf(Bridge.Content), Bridge.State.Date);

        /// <summary>The distance the family game opens at over its village: close enough for the 1948 localities' names
        /// (MapLabelsPresenter.LocalityDistanceM), far enough for the villages around it.</summary>
        public const float VillageDistanceM = 30000f;

        /// <summary>How far one press of a pan button moves the view, as a share of the camera's distance.</summary>
        public const float PanShare = 0.5f;

        /// <summary>The places whose fate on the map (FamilyGame.Fates) is new in <paramref name="now"/>, with that fate:
        /// <paramref name="village"/> first, then the others by id.</summary>
        public static IEnumerable<(string place, string fate)> MapChanges(IReadOnlyDictionary<string, string> was,
            IReadOnlyDictionary<string, string> now, string village) =>
            now.Where(kv => !was.TryGetValue(kv.Key, out var f) || f != kv.Value)
                .OrderBy(kv => kv.Key == village ? 0 : 1).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => (kv.Key, kv.Value));

        /// <summary>Points the camera at the family's village (F5 step 6), looking north as the game starts.</summary>
        internal static void FrameVillage(MapScene map, CameraRig rig, GameSession session)
        {
            var place = session.Content.Places.FirstOrDefault(p => p.Id == session.State.Village);
            if (place == null) return;
            rig.LookAt = map.Frame.ToWorld(place.Lon, place.Lat, 0); rig.Distance = VillageDistanceM; rig.Yaw = 0f; rig.Apply();
        }

        T Own<T>(T d) where T : IDisposable { owned.Add(d); return d; }

        /// <summary>Disposes every presenter, the autosave and the camera/pointer hooks (newest first). Safe to call twice.</summary>
        void Shutdown()
        {
            StopAllCoroutines();
            // The card first: disposing the start screen uncovers the decision layer, and a card still waiting for it would
            // then render for a game that is going away (#592).
            if (card != null) { owned.Remove(card); card.Dispose(); }
            for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose();
            owned.Clear();
            picker = null; people = null; settings = null; pause = null; skipButton = null; skipping = false;
            minimap = null; labels = null; nav = null; start = null; card = null; rig = null; Regions = null; logCamera = false;
            // A ?profile run cut short (a stopped coroutine never reaches its restore line) must not leave the terrain at the
            // profile's pixel error (Task 24 review M1). Nothing else changes it, so restoring is always right.
            if (map != null && map.Terrain != null) map.Terrain.heightmapPixelError = MapBuilder.TerrainPixelError;
        }

        /// <summary>The 1948 Collection's craft (#669), for the labels to keep clear of.</summary>
        StoryOfTheLand.Game.Map.Collection.SeaAirCollection? seaAir;

        /// <summary>The map's period scenery at the game's date (every change raises Changed; each layer redraws only when what
        /// it shows changes): the dated routes, Tal Heuberger's 1948 Collection (towns far off, wildlife, civilian figures,
        /// farm and village scenery, the Tegart forts, barracks and factory, the ships and the C-54), streamed after the first
        /// frame. The family game gives no map orders, so nothing is placed by the player.</summary>
        void RenderScenery()
        {
            if (Bridge == null) return;
            var date = Bridge.State.Date;
            StoryOfTheLand.Game.Map.Routes.RouteLayer.ShowAt(map, date);   // dated routes (the Burma Road from June 1948)
            StoryOfTheLand.Game.Map.Collection.TownLandmarks.ShowAt(map);
            StoryOfTheLand.Game.Map.Collection.WildlifeLayer.Show(map);
            StoryOfTheLand.Game.Map.Collection.CivilianPeople.ShowAt(map);
            StoryOfTheLand.Game.Map.Collection.RuralLife.Show(map);
            StoryOfTheLand.Game.Map.Collection.SpecialBuildingModels.ShowAt(map, date);
            seaAir = StoryOfTheLand.Game.Map.Collection.SeaAirCollection.ShowAt(map, date);
        }

        void SyncMinimap()
        {
            if (minimap == null || rig == null) return;
            minimap.SetView(map.Frame, rig.LookAt, rig.Distance);
        }

        bool logCamera;
        float loggedDistance = float.NaN, loggedYaw = float.NaN;

        /// <summary>"camlog" in the WebGL page URL (a whole query parameter) or -camlog on desktop: log the camera distance and
        /// yaw whenever they change (checks the wheel-zoom step in a real browser).</summary>
        internal static bool CameraLogRequested(string? url, string[] args, bool webgl) =>
            webgl ? UrlFlags.Has(url, "camlog") : Array.IndexOf(args, "-camlog") >= 0;

        static bool CameraLogRequested() =>
            CameraLogRequested(Application.absoluteURL, Environment.GetCommandLineArgs(), Application.platform == RuntimePlatform.WebGLPlayer);

        internal static string CameraLine(float distance, float yaw) => FormattableString.Invariant($"CAMERA distance={distance:0} yaw={yaw:0.0}");

        /// <summary>"firstcard" asks a player build to exercise the first card through the normal media path. It deliberately
        /// is not one of StartScreenPresenter.Skipped or CardVideo.ForcedOff's automation flags.</summary>
        internal static bool FirstCardCheckRequested(string? url, string[] args, bool webgl) =>
            webgl ? UrlFlags.Has(url, "firstcard") : Array.IndexOf(args, "-firstcard") >= 0;

        internal static bool FirstCardCheckRequested() =>
            FirstCardCheckRequested(Application.absoluteURL, Environment.GetCommandLineArgs(), Application.platform == RuntimePlatform.WebGLPlayer);

        /// <summary>Starts a new game through the start screen's real buttons at once (New game, then the picker's
        /// <see cref="AutomatedChapter"/> and <see cref="AutomatedVillage"/>) and waits at most <see cref="FirstCardBudgetSeconds"/>
        /// seconds for the first card with an enabled option. The browser check treats FIRST CARD FAIL and console exceptions as
        /// failures. With a family save the start screen's New game needs a second press (NewGamePresenter): it gets one.</summary>
        internal static IEnumerator FirstCardCheck(VisualElement root, EventCardPresenter card, Action<string> log,
            Func<double>? now = null)
        {
            now ??= () => Time.realtimeSinceStartupAsDouble;
            var began = now();
            var hud = root.Q("root") ?? root;
            Action<string> fail = s => log(s.Replace("SMOKE FAIL", "FIRST CARD FAIL"));
            if (!hud.ClassListContains(StartScreenPresenter.OpenClass))
            {
                log("FIRST CARD FAIL start screen is not open");
                yield break;
            }
            if (!Press(root.Q<Button>("start-new-game"), fail)) yield break;
            // With a save, New game asks first (playtest 2: the pause menu's confirm) or, without that menu, takes a second press.
            if (!hud.ClassListContains(FamilyPickerPresenter.OpenClass)
                && !Press(root.Q<Button>(hud.ClassListContains(PausePresenter.OpenClass) ? "pause-confirm-yes" : "start-new-game"), fail)) yield break;
            yield return null;
            if (!Press(root.Q<Button>("chapter-" + AutomatedChapter), fail)) yield break;
            yield return null;
            // The village starts the family game: Begin stops this coroutine and goes on in FirstCardWait from the same start.
            firstCardBegan = began;
            if (!Press(root.Q<Button>("village-" + AutomatedVillage), fail)) yield break;
            var wait = FirstCardWait(root, card, log, began, now);
            while (wait.MoveNext()) yield return wait.Current;
        }

        static double? firstCardBegan;

        /// <summary>The second half of <see cref="FirstCardCheck"/>: waits, from <paramref name="began"/>, for the first card
        /// with an enabled option, and logs FIRST CARD OK or FAIL.</summary>
        static IEnumerator FirstCardWait(VisualElement root, EventCardPresenter card, Action<string> log, double began, Func<double> now)
        {
            var hud = root.Q("root") ?? root;
            while (now() - began <= FirstCardBudgetSeconds)
            {
                var option = root.Q<VisualElement>("event-options")?.Q<Button>();
                if (!hud.ClassListContains(StartScreenPresenter.OpenClass) && card.Shown != null && !card.Collapsed &&
                    option != null && option.enabledInHierarchy)
                {
                    var wait = (now() - began) * 1000;
                    firstCardBegan = null;
                    log(FormattableString.Invariant($"FIRST CARD OK event={card.Shown.Id} wait={wait:0} ms budget={FirstCardBudgetSeconds * 1000:0} ms"));
                    yield break;
                }
                yield return null;
            }
            firstCardBegan = null;
            log(FormattableString.Invariant($"FIRST CARD FAIL not interactive after {FirstCardBudgetSeconds * 1000:0} ms"));
        }

        /// <summary>Escape (Update): the start screen's New game request, the pause menu, the open screen, the decision card (only
        /// <paramref name="unfocused"/>: a focused card takes the UI's Cancel event itself), and with none of them open now or at
        /// the end of the last frame, the pause menu opens. True when something took it.</summary>
        internal bool Escape(bool unfocused)
        {
            bool done = pause?.Escape() == true;   // first: its confirm may be up over the start screen (New game, playtest 2)
            done = done || start?.Escape() == true || start?.IsOpen == true;
            done = done || nav?.Close() == true;
            if (!done && unfocused) done = card?.Escape() == true;
            if (done || coveredLastFrame || Covered() || pause == null) return done;
            pause.Open();
            return true;
        }

        /// <summary>F6, the end-turn key (Enter, Update): presses End Turn when nothing is open over the map (no start screen,
        /// picker, screen, pause menu or open card: Covered) and no element has the keyboard focus. With the focus on a control,
        /// Enter is the UI's Submit and presses that control instead; with it in a text field (the picker's search) Enter is
        /// typing. A finished game's End Turn is disabled: the key then says so on the toast. True when it acted.</summary>
        internal bool EndTurnKey()
        {
            if (Boot.DiagLog.Automated) return false;   // F6 round 5: never in a measuring run (an Enter ended a turn mid-profile)
            if (Covered() || document.rootVisualElement.focusController?.focusedElement != null) return false;
            var button = document.rootVisualElement.Q<Button>("end-turn");
            if (button == null) return false;
            if (!button.enabledInHierarchy) { say?.Invoke(gameOverLine); return true; }
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); }
            return true;
        }

        /// <summary>Whether anything that takes Escape before the pause menu is open: the start screen, the picker, a screen, the
        /// pause menu itself, or the decision card (not hidden).</summary>
        bool Covered() => start?.IsOpen == true || picker?.IsOpen == true || nav?.Current != null || pause?.IsOpen == true
            || card?.Shown != null && !card.Collapsed;

        /// <summary>After every Update (CameraController moves the camera in its Update), so the pills never trail the camera.</summary>
        void LateUpdate()
        {
            coveredLastFrame = Covered();
            if (labels == null) return;
            labels.Refresh();
            // Today's name under a 1948 locality's pill: while a mouse (or pen) is over it (owner ruling 2026-10-11), or after a
            // tap on it (#245). A touchscreen has no hover: its last touch position stays after the finger lifts.
            labels.Hover(MapLabelsPresenter.HoverPosition(UnityEngine.InputSystem.Pointer.current));
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) labels.Tap(touch.primaryTouch.position.ReadValue());
        }

        void Update()
        {
            // Escape closes the open screen even when no element has the keyboard focus (ScreenNav also takes the UI
            // Cancel event from a focused element; closing twice is harmless). On the start screen it withdraws an open New
            // Game request (StartScreenPresenter.Escape; idempotent too).
            // With nothing else to close, Escape on the open decision card is Hide (EventCardPresenter.Escape; the card itself
            // takes the Cancel event when it has the focus).
            // The family picker (F5) takes Escape as Back from its focused element (its Cancel event); with nothing focused, here.
            // F6: with nothing else to close (nothing that takes Escape was open at the end of the last frame either, so an
            // Escape a focused screen or card already took through the UI's Cancel event does not also open it), Escape opens the
            // pause menu; open, the menu takes Escape first (PausePresenter.Escape). A controller's Start opens and closes it.
            if (!Boot.DiagLog.Automated) Keys();   // F6 round 5: a measuring run takes no gameplay keys (an Enter ended a turn mid-profile)
            SyncMinimap();
            if (!logCamera || rig == null || (rig.Distance == loggedDistance && rig.Yaw == loggedYaw)) return;
            loggedDistance = rig.Distance; loggedYaw = rig.Yaw;
            Debug.Log(CameraLine(rig.Distance, rig.Yaw));
        }

        /// <summary>The keyboard and controller keys, every Update (not in a measuring run: DiagLog.Automated).</summary>
        void Keys()
        {
            if (picker?.IsOpen == true) { if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true && document.rootVisualElement.focusController?.focusedElement == null) picker.Back(); }
            else if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Escape(document.rootVisualElement.focusController?.focusedElement == null);
            else if (UnityEngine.InputSystem.Gamepad.current?.startButton.wasPressedThisFrame == true && start?.IsOpen != true)
            { if (pause?.IsOpen == true) pause.Close(); else if (!Covered()) pause?.Open(); }
            // PageUp, PageDown, Home and End, with no modifier, scroll the open screen or the open card's text, with or without
            // focus (KeyScroll; accessibility audit H3). The arrows stay with the camera (NavigationGuard, B6).
            var keys = UnityEngine.InputSystem.Keyboard.current;
            // N mutes the narration or turns it back on (F5 step 8), as Settings' switch does, but not while typing (the picker's search).
            if (keys?.nKey.wasPressedThisFrame == true && !(document.rootVisualElement.focusController?.focusedElement is VisualElement typing
                && (typing is TextField || typing.GetFirstAncestorOfType<TextField>() != null))) settings?.FlipNarration();
            // Audit m15: in the pause menu the up and down arrows move the focus as Shift+Tab and Tab do (the camera is off there).
            if (pause?.IsOpen == true && keys != null)
            {
                if (keys.upArrowKey.wasPressedThisFrame) pause.MoveFocus(-1);
                else if (keys.downArrowKey.wasPressedThisFrame) pause.MoveFocus(+1);
            }
            // F6: Enter (or the keypad's) ends the turn, the strategy games' key (EndTurnKey).
            if (keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)) EndTurnKey();
            if (keys != null && !(keys.ctrlKey.isPressed || keys.altKey.isPressed || keys.shiftKey.isPressed || keys.leftMetaKey.isPressed || keys.rightMetaKey.isPressed))
            {
                KeyScroll.Key? page = keys.pageUpKey.wasPressedThisFrame ? KeyScroll.Key.PageUp : keys.pageDownKey.wasPressedThisFrame ? KeyScroll.Key.PageDown
                    : keys.homeKey.wasPressedThisFrame ? KeyScroll.Key.Home : keys.endKey.wasPressedThisFrame ? KeyScroll.Key.End : (KeyScroll.Key?)null;
                if (page != null) KeyScroll.Press(document.rootVisualElement, nav?.Current, card?.Shown != null && !card.Collapsed, page.Value);
            }
        }

        /// <summary>Plays turns the way a player does: presses the card's first button while a card shows (a choice, then
        /// Continue under what followed), then End Turn. Logs "SMOKE turn &lt;n&gt; &lt;date&gt; refused &lt;k&gt;" per turn through
        /// <paramref name="log"/>, then "SMOKE OK &lt;n&gt; turns &lt;date&gt; &lt;hash&gt;", or "SMOKE FAIL &lt;reason&gt;" when a press
        /// does not take effect. Static, so an edit-mode test can drive it step by step.</summary>
        public static IEnumerator Smoke(VisualElement root, SimBridge bridge, EventCardPresenter card, int turns, Action<string> log)
        {
            for (int t = 0; t < turns; t++)
            {
                yield return null;
                for (int guard = 0; card.Shown != null; guard++)
                {
                    var shown = card.Shown;
                    var option = root.Q<VisualElement>("event-options")?.Q<Button>();
                    if (option == null || guard >= 100) { log(FormattableString.Invariant($"SMOKE FAIL event {shown.Id} has no option to press")); yield break; }
                    var queued = bridge.Queued.Count;
                    var label = option.text;
                    if (!Press(option, log)) yield break;
                    // A choice queues an order (the card may stay with what followed); Continue moves to the next card.
                    if (card.Shown == shown && bridge.Queued.Count == queued && root.Q<VisualElement>("event-options")?.Q<Button>()?.text == label)
                    { log(FormattableString.Invariant($"SMOKE FAIL the option '{label}' for event {shown.Id} was not taken")); yield break; }
                    yield return null;
                }
                var turn = bridge.State.Turn;
                int refused = 0;
                Action<IReadOnlyList<Rejection>> count = r => refused = r.Count;
                bridge.TurnEnded += count;
                bool pressed;
                try { pressed = Press(root.Q<Button>("end-turn"), log); }
                finally { bridge.TurnEnded -= count; }
                if (!pressed) yield break;
                if (bridge.State.Turn != turn + 1) { log(FormattableString.Invariant($"SMOKE FAIL End Turn did not end turn {turn}")); yield break; }
                var s = bridge.State;
                log(FormattableString.Invariant($"SMOKE turn {s.Turn} {s.Date:yyyy-MM-dd} refused {refused}"));
                if (s.GameOver != null) break;
            }
            log(FormattableString.Invariant($"SMOKE OK {turns} turns {bridge.State.Date:yyyy-MM-dd} {bridge.State.Hash()}"));
        }

        static bool Press(Button? b, Action<string> log)
        {
            if (b == null) { log("SMOKE FAIL button missing"); return false; }
            try
            {
                using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
                return true;
            }
            catch (Exception e) { log("SMOKE FAIL " + e.Message); return false; }
        }

        sealed class Undo : IDisposable
        {
            readonly Action undo;
            public Undo(Action undo) { this.undo = undo; }
            public void Dispose() => undo();
        }
    }
}
