#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using StoryOfTheLand.Game.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The Settings screen (#40; tabbed in F6), opened from the left column's gear or the pause menu like the other
    /// screens (ScreenNav). Six tabs, as the big strategy games have them: Display (resolution, window mode, vertical sync,
    /// frame-rate limit), Graphics (the preset, and the rendering side's knobs in GraphicsQuality: shadows, render scale, edge
    /// smoothing, terrain detail, haze; and the map's blur, TiltShift), Audio (master volume,
    /// the narration and its volume, the music's volume), Language, Accessibility (text size, reduced motion, high-contrast meters) and Controls (the keys, read
    /// from the real bindings). Every choice applies at once and is kept in the SettingsStore (settings.txt), so it holds after
    /// a reload; a value the game does not know leaves the default. A preset sets the knobs; a knob changed after it marks no
    /// preset (CurrentPreset: "custom"). Each row is made in code from a <see cref="Row"/>: its
    /// name and either a set of buttons (the set one marked), an On/Off switch that says what is set, or a stepper (‹ value ›)
    /// for a long list. Elements are named settings-&lt;row&gt;, settings-&lt;row&gt;-label, settings-&lt;row&gt;-&lt;value&gt;,
    /// settings-&lt;row&gt;-prev/-next/-value; the tabs settings-tab-&lt;tab&gt; and their pages settings-page-&lt;tab&gt;.
    /// A stored "off" blur only ever turns the blur off, so ?notiltshift still boots without it; a stored quality yields to an
    /// instrument flag (GraphicsQuality.AtStart), and a stored frame pacing to the frame-time instruments
    /// (GraphicsQuality.VSyncAtStart). The Narration switch mutes the chapter intros (Narrator.Muted); N flips it too (GameRoot).</summary>
    public sealed class SettingsPresenter : IDisposable
    {
        public const string TiltShiftKey = "tiltshift", NarrationKey = "narration", QualityKey = "quality", LanguageKey = "lang", SelectedClass = "settings-choice-selected";
        public const string ResolutionKey = "resolution", WindowKey = "window", VSyncKey = "vsync", FrameCapKey = "fps", VolumeKey = "volume",
            NarrationVolumeKey = "narration_volume", MusicVolumeKey = "music_volume", TextSizeKey = "text_size", ContrastKey = "high_contrast",
            ShadowsKey = "shadows", RenderScaleKey = "render_scale", AntialiasingKey = "antialiasing", DetailKey = "detail", HazeKey = "haze",
            MotionKey = "reduced_motion";
        /// <summary>The graphics knobs (GraphicsQuality, the rendering side's): a preset sets them, and they yield to the
        /// instruments as the preset does.</summary>
        static readonly string[] GraphicsKeys = { ShadowsKey, RenderScaleKey, AntialiasingKey, DetailKey, HazeKey };
        /// <summary>The presets: Automatic, Low and High are GraphicsQuality's tiers at their own look; Medium is High with hard
        /// shadows, a 0.9 render scale and the fast edge smoothing (the rendering side's suggestion). There is no Ultra:
        /// High already draws everything the map has (no MSAA or bloom: both priced and dropped, docs/visual-v1.md).</summary>
        public static readonly string[] Presets = { "auto", "low", "medium", "high" };
        /// <summary>The tabs in their order; each one's title is settings.tab.&lt;tab&gt;.</summary>
        public static readonly string[] Tabs = { "display", "graphics", "audio", "language", "accessibility", "controls", "about" };
        public const string TabSelectedClass = "settings-tab-selected";
        /// <summary>On the "root" element while the meters are drawn in high contrast (StoryOfTheLand.uss).</summary>
        public const string ContrastClass = "high-contrast";
        /// <summary>The text sizes offered, in percent of the stylesheet's.</summary>
        public static readonly int[] TextSizes = { 90, 100, 115, 130 };
        /// <summary>The volumes offered, in percent.</summary>
        public static readonly int[] Volumes = Enumerable.Range(0, 11).Select(i => i * 10).ToArray();

        enum Kind { Choices, Switch, Stepper }

        /// <summary>One option: what it shows, what is set, and what choosing a value does.</summary>
        sealed class Row
        {
            public string Name = "", Key = "", Label = "", Tab = "";
            public Kind Kind;
            public string[] Values = Array.Empty<string>();
            public Func<string, string> Text = v => v;
            public Func<string> Current = () => "";
            public Action<string> Apply = _ => { };
            public Func<bool> Enabled = () => true;
            public readonly List<(Button button, string value)> Buttons = new List<(Button, string)>();
            public Label? Value;
            public Button? Switch, Prev, Next;
        }

        // What is set and kept for the session (statics, as TiltShift.Enabled: one choice for every game).
        static string tab = Tabs[0];
        static string window = "", resolution = "";
        static int textSize = 100;
        static bool contrast;
        static Vector2Int? baseReference;

        readonly SettingsStore? store; readonly UiText text; readonly VisualElement root, screen; readonly ScrollView body;
        readonly PanelSettings? panel;
        readonly List<Row> rows = new List<Row>();
        readonly List<(Button, Action)> hooks = new List<(Button, Action)>();
        readonly EventCallback<GeometryChangedEvent> onGeometry;
        readonly Dictionary<string, (Button button, VisualElement page)> tabs = new Dictionary<string, (Button, VisualElement)>();
        readonly Action<string>? switchLanguage;
        readonly Action onQuality;
        readonly bool web;

        /// <param name="store">Where the choices are kept; null keeps nothing (the options still work until the page closes).</param>
        /// <param name="switchLanguage">Puts the UI in the language pressed (GameRoot builds the screens again); null changes
        /// nothing but the stored choice, read at the next start.</param>
        /// <param name="panel">The game's panel settings, which the text size scales; null leaves the size as it is.</param>
        public SettingsPresenter(VisualElement root, UiText text, SettingsStore? store, Action<string>? switchLanguage = null, PanelSettings? panel = null)
        {
            this.root = root; this.store = store; this.text = text; this.switchLanguage = switchLanguage; this.panel = panel;
            web = Application.platform == RuntimePlatform.WebGLPlayer;
            screen = root.Q<VisualElement>("screen-settings");
            body = screen.Q<ScrollView>("settings-body");
            screen.Q<Label>("settings-screen-title").text = text["settings.title"];
            var tabBar = screen.Q<VisualElement>("settings-tabs");
            // Audit m10: the credits row (Game.uxml) lives in the About tab, once, not under every tab. Taken out first: clearing
            // the pages below must not drop it (the language switch builds this screen again).
            var creditsRow = screen.Q(className: "settings-credits-row");
            creditsRow?.RemoveFromHierarchy();
            tabBar.Clear(); body.Clear();
            foreach (var t in Tabs)
            {
                var b = new Button { name = "settings-tab-" + t, text = text["settings.tab." + t] }.WithClass("settings-tab");
                var page = new VisualElement { name = "settings-page-" + t }.WithClass("settings-page");
                tabBar.Add(b); body.Add(page);
                tabs[t] = (b, page);
                var id = t;
                Hook(b, () => ShowTab(id));
            }
            foreach (var r in Build()) { rows.Add(r); Add(r); }
            AddControls(tabs["controls"].page);
            if (creditsRow != null) tabs["about"].page.Add(creditsRow);
            Restore();
            onQuality = Show;   // a tier switch may come from the fallback or a dev key while the screen is open
            GraphicsQuality.Changed += onQuality;
            ScreenNav.OnOpened(screen, Show);   // a dev toggle or N may have changed a value while the screen was shut
            onGeometry = _ => Fit();
            screen.RegisterCallback(onGeometry);
            body.contentContainer.RegisterCallback(onGeometry);
            ShowTab(tab);
            Show();
        }

        // ---- The rows -----------------------------------------------------------------------------------------------

        IEnumerable<Row> Build()
        {
            string Percent(string v) => text.Format("settings.percent", int.Parse(v, CultureInfo.InvariantCulture));
            string OnOff(bool on) => on ? "on" : "off";
            // Display: the window on the desktop; the frame-rate limit everywhere (the browser paces the web player's frames).
            if (!web)
            {
                yield return new Row
                {
                    Name = ResolutionKey, Key = ResolutionKey, Label = "settings.resolution", Tab = "display", Kind = Kind.Stepper,
                    Values = Resolutions(Screen.resolutions.Select(r => (r.width, r.height)), CurrentResolution()),
                    Text = v => text.Format("settings.resolution_value", v.Split('x')[0], v.Split('x')[1]),
                    Current = CurrentResolution, Apply = SetResolution,
                };
                yield return new Row
                {
                    Name = WindowKey, Key = WindowKey, Label = "settings.window", Tab = "display", Kind = Kind.Choices,
                    Values = new[] { "fullscreen", "borderless", "windowed" }, Text = v => text["settings.window." + v],
                    Current = CurrentWindow, Apply = SetWindow,
                };
                yield return new Row
                {
                    Name = VSyncKey, Key = VSyncKey, Label = "settings.vsync", Tab = "display", Kind = Kind.Switch,
                    Values = new[] { "on", "off" }, Current = () => OnOff(GraphicsQuality.VSync),
                    Apply = v => GraphicsQuality.SetFramePacing(v == "on", GraphicsQuality.FrameCap),
                };
            }
            yield return new Row
            {
                Name = FrameCapKey, Key = FrameCapKey, Label = "settings.fps", Tab = "display", Kind = Kind.Choices,
                Values = GraphicsQuality.FrameCaps.Where(c => c > 0).Concat(new[] { 0 }).Select(c => c.ToString(CultureInfo.InvariantCulture)).ToArray(),
                Text = v => v == "0" ? text["settings.fps.unlimited"] : v,
                Current = () => GraphicsQuality.FrameCap.ToString(CultureInfo.InvariantCulture),
                Apply = v => GraphicsQuality.SetFramePacing(GraphicsQuality.VSync, int.Parse(v, CultureInfo.InvariantCulture)),
                Enabled = () => web || !GraphicsQuality.VSync,   // vertical sync sets the rate (one frame per refresh)
            };
            // Graphics.
            yield return new Row
            {
                Name = QualityKey, Key = QualityKey, Label = "settings.quality", Tab = "graphics", Kind = Kind.Choices,
                Values = Presets, Text = v => text["settings.quality." + v], Current = CurrentPreset, Apply = ApplyPreset,
            };
            yield return new Row
            {
                Name = ShadowsKey, Key = ShadowsKey, Label = "settings.shadows", Tab = "graphics", Kind = Kind.Choices,
                Values = new[] { "off", "hard", "soft" }, Text = v => text["settings.shadows." + v],
                Current = () => GraphicsQuality.Shadows.ToString().ToLowerInvariant(),
                Apply = v => GraphicsQuality.Shadows = (GraphicsQuality.ShadowChoice)Enum.Parse(typeof(GraphicsQuality.ShadowChoice), v, ignoreCase: true),
            };
            yield return new Row
            {
                Name = RenderScaleKey, Key = RenderScaleKey, Label = "settings.render_scale", Tab = "graphics", Kind = Kind.Choices,
                Values = new[] { "preset", "70", "80", "90", "100" }, Text = v => v == "preset" ? text["settings.render_scale.preset"] : Percent(v),
                Current = () => GraphicsQuality.RenderScale is float f ? Mathf.RoundToInt(f * 10f).ToString(CultureInfo.InvariantCulture) + "0" : "preset",
                Apply = v => GraphicsQuality.RenderScale = v == "preset" ? (float?)null : int.Parse(v, CultureInfo.InvariantCulture) / 100f,
            };
            yield return new Row
            {
                Name = AntialiasingKey, Key = AntialiasingKey, Label = "settings.antialiasing", Tab = "graphics", Kind = Kind.Choices,
                Values = new[] { "auto", "off", "fxaa", "smaa" }, Text = v => text["settings.antialiasing." + v],
                Current = () => GraphicsQuality.Antialiasing.ToString().ToLowerInvariant(),
                Apply = v => GraphicsQuality.Antialiasing = (GraphicsQuality.AntialiasingChoice)Enum.Parse(typeof(GraphicsQuality.AntialiasingChoice), v, ignoreCase: true),
            };
            yield return new Row
            {
                Name = DetailKey, Key = DetailKey, Label = "settings.detail", Tab = "graphics", Kind = Kind.Switch,
                Values = new[] { "on", "off" }, Current = () => OnOff(GraphicsQuality.Detail), Apply = v => GraphicsQuality.Detail = v == "on",
            };
            yield return new Row
            {
                Name = HazeKey, Key = HazeKey, Label = "settings.haze", Tab = "graphics", Kind = Kind.Switch,
                Values = new[] { "on", "off" }, Current = () => OnOff(GraphicsQuality.Haze), Apply = v => GraphicsQuality.Haze = v == "on",
            };
            yield return new Row
            {
                Name = TiltShiftKey, Key = TiltShiftKey, Label = "settings.tiltshift", Tab = "graphics", Kind = Kind.Switch,
                Values = new[] { "on", "off" }, Current = () => OnOff(TiltShift.Enabled), Apply = v => TiltShift.Enabled = v == "on",
            };
            // Audio.
            yield return new Row
            {
                Name = VolumeKey, Key = VolumeKey, Label = "settings.volume", Tab = "audio", Kind = Kind.Stepper,
                Values = Volumes.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray(), Text = Percent,
                Current = () => Step(AudioListener.volume), Apply = v => AudioListener.volume = int.Parse(v, CultureInfo.InvariantCulture) / 100f,
            };
            yield return new Row
            {
                Name = NarrationKey, Key = NarrationKey, Label = "settings.narration", Tab = "audio", Kind = Kind.Switch,
                Values = new[] { "on", "off" }, Current = () => OnOff(!Narrator.Muted), Apply = v => Narrator.Muted = v == "off",
            };
            yield return new Row
            {
                Name = NarrationVolumeKey, Key = NarrationVolumeKey, Label = "settings.narration_volume", Tab = "audio", Kind = Kind.Stepper,
                Values = Volumes.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray(), Text = Percent,
                Current = () => Step(Narrator.Volume), Apply = v => Narrator.Volume = int.Parse(v, CultureInfo.InvariantCulture) / 100f,
            };
            yield return new Row
            {
                Name = MusicVolumeKey, Key = MusicVolumeKey, Label = "settings.music_volume", Tab = "audio", Kind = Kind.Stepper,
                Values = Volumes.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray(), Text = Percent,
                Current = () => Step(MusicPlayer.Volume), Apply = v => MusicPlayer.Volume = int.Parse(v, CultureInfo.InvariantCulture) / 100f,
            };
            // Language (#1036): each button names its language in that language, so a player who cannot read the UI's one
            // still finds their own. The choice is kept first: the switch disposes this presenter with the rest of the UI.
            yield return new Row
            {
                Name = "language", Key = LanguageKey, Label = "settings.language", Tab = "language", Kind = Kind.Choices,
                Values = new[] { UiLanguage.English, UiLanguage.Arabic }, Text = v => v == UiLanguage.Arabic ? "العربية" : "English",
                Current = () => text["ui.lang"], Apply = v => switchLanguage?.Invoke(v),
            };
            // Accessibility.
            yield return new Row
            {
                Name = TextSizeKey, Key = TextSizeKey, Label = "settings.text_size", Tab = "accessibility", Kind = Kind.Choices,
                Values = TextSizes.Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray(), Text = Percent,
                Current = () => textSize.ToString(CultureInfo.InvariantCulture), Apply = v => SetTextSize(int.Parse(v, CultureInfo.InvariantCulture)),
            };
            yield return new Row
            {
                Name = MotionKey, Key = MotionKey, Label = "settings.reduced_motion", Tab = "accessibility", Kind = Kind.Switch,
                Values = new[] { "on", "off" }, Current = () => OnOff(GraphicsQuality.ReducedMotion), Apply = v => { GraphicsQuality.ReducedMotion = v == "on"; ShowMotion(); },
            };
            yield return new Row
            {
                Name = ContrastKey, Key = ContrastKey, Label = "settings.high_contrast", Tab = "accessibility", Kind = Kind.Switch,
                Values = new[] { "on", "off" }, Current = () => OnOff(contrast), Apply = v => { contrast = v == "on"; ShowContrast(); },
            };
        }

        /// <summary>The preset the knobs match: a tier with every knob at its own look (Automatic, Low, High), or High with
        /// Medium's; else "custom", which marks no preset button.</summary>
        static string CurrentPreset()
        {
            bool own = GraphicsQuality.Shadows == GraphicsQuality.ShadowChoice.Soft && GraphicsQuality.RenderScale == null
                && GraphicsQuality.Antialiasing == GraphicsQuality.AntialiasingChoice.Auto && GraphicsQuality.Detail && GraphicsQuality.Haze;
            bool medium = GraphicsQuality.Shadows == GraphicsQuality.ShadowChoice.Hard && GraphicsQuality.RenderScale is float f && Mathf.Abs(f - 0.9f) < 0.01f
                && GraphicsQuality.Antialiasing == GraphicsQuality.AntialiasingChoice.Fxaa && GraphicsQuality.Detail && GraphicsQuality.Haze;
            return GraphicsQuality.Current == GraphicsQuality.Choice.High && medium ? "medium" : own ? GraphicsQuality.Format(GraphicsQuality.Current) : "custom";
        }

        /// <summary>Sets a preset: its tier (Medium is High's), then every knob to the preset's values, each kept as if chosen.
        /// The same tier is not applied again, so a step the fallback took for a slow device stays.</summary>
        void ApplyPreset(string v)
        {
            var c = v == "medium" ? GraphicsQuality.Choice.High : GraphicsQuality.Parse(v);
            if (c != GraphicsQuality.Current) GraphicsQuality.Apply(c);
            bool medium = v == "medium";
            foreach (var r in rows.Where(r => GraphicsKeys.Contains(r.Key)))
            {
                var value = r.Key == ShadowsKey ? (medium ? "hard" : "soft") : r.Key == RenderScaleKey ? (medium ? "90" : "preset")
                    : r.Key == AntialiasingKey ? (medium ? "fxaa" : "auto") : "on";
                if (value == r.Current()) continue;
                Keep(r.Key, value);
                r.Apply(value);
            }
        }

        /// <summary>A volume as the nearest step of ten.</summary>
        static string Step(float volume) => (Mathf.RoundToInt(Mathf.Clamp01(volume) * 10f) * 10).ToString(CultureInfo.InvariantCulture);

        /// <summary>The resolutions to offer: each width and height once (a display lists one per refresh rate), smallest first,
        /// with <paramref name="current"/> among them (a window dragged to a size of its own).</summary>
        public static string[] Resolutions(IEnumerable<(int w, int h)> listed, string current) =>
            listed.Select(r => Format(r.w, r.h)).Append(current).Distinct()
                .Select(v => (v, wh: v.Split('x').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray()))
                .OrderBy(x => x.wh[0]).ThenBy(x => x.wh[1]).Select(x => x.v).ToArray();

        static string Format(int w, int h) => w.ToString(CultureInfo.InvariantCulture) + "x" + h.ToString(CultureInfo.InvariantCulture);

        static string CurrentResolution() => resolution.Length > 0 ? resolution : Format(Screen.width, Screen.height);

        static void SetResolution(string v)
        {
            resolution = v;
            var wh = v.Split('x');
            Screen.SetResolution(int.Parse(wh[0], CultureInfo.InvariantCulture), int.Parse(wh[1], CultureInfo.InvariantCulture), Mode(CurrentWindow()));
        }

        static string CurrentWindow() => window.Length > 0 ? window : Screen.fullScreenMode switch
        {
            FullScreenMode.Windowed => "windowed", FullScreenMode.FullScreenWindow => "borderless", _ => "fullscreen",
        };

        static void SetWindow(string v) { window = v; Screen.fullScreenMode = Mode(v); }

        /// <summary>The window mode a choice names: fullscreen is exclusive on Windows and the native full screen (its own
        /// Space) on macOS; borderless is a full-screen window; windowed a window.</summary>
        public static FullScreenMode Mode(string v) => v switch
        {
            "windowed" => FullScreenMode.Windowed,
            "borderless" => FullScreenMode.FullScreenWindow,
            _ => Application.platform == RuntimePlatform.WindowsPlayer ? FullScreenMode.ExclusiveFullScreen
                : Application.platform == RuntimePlatform.OSXPlayer ? FullScreenMode.MaximizedWindow : FullScreenMode.FullScreenWindow,
        };

        /// <summary>The text size: the panel's reference resolution divided by the size, so every text and control grows
        /// together and the HUD picks its tier from the smaller logical width (HudPresenter).</summary>
        void SetTextSize(int percent)
        {
            textSize = percent;
            if (panel == null) return;
            baseReference ??= panel.referenceResolution;
            var b = baseReference.Value;
            panel.referenceResolution = new Vector2Int(Mathf.RoundToInt(b.x * 100f / percent), Mathf.RoundToInt(b.y * 100f / percent));
        }

#if UNITY_EDITOR
        // The panel settings are a project file in the editor: leaving Play mode puts its reference resolution back, so a
        // text size chosen while playing is never saved into Assets/UI.
        [UnityEditor.InitializeOnLoadMethod]
        static void PutTheTextSizeBackOnExit() => UnityEditor.EditorApplication.playModeStateChanged += state =>
        {
            if (state != UnityEditor.PlayModeStateChange.ExitingPlayMode || baseReference == null) return;
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel);
            if (asset != null) asset.referenceResolution = baseReference.Value;
            baseReference = null; textSize = 100;
        };
#endif

        /// <summary>F7: on the "root" element while the UI moves (hover and press transitions, the card sliding in, chips popping,
        /// the bar counting, End Turn glowing): in a player or Play mode with reduce motion off. Off in edit mode, so tests see
        /// the end states at once.</summary>
        public const string MotionClass = "ui-motion";

        void ShowMotion() => (root.Q("root") ?? root).EnableInClassList(MotionClass, Application.isPlaying && !GraphicsQuality.ReducedMotion);

        void ShowContrast() => (root.Q("root") ?? root).EnableInClassList(ContrastClass, contrast);

        /// <summary>The stored choices, applied at start where they differ from what is set: the blur only ever turned off; the
        /// quality and the frame pacing unless an instrument must see the tier's own look or measure the frame's own cost; the
        /// language by GameRoot (UiLanguage.AtStart).</summary>
        void Restore()
        {
            var args = Environment.GetCommandLineArgs();
            bool instrument = GraphicsQuality.AtStart("high", Application.absoluteURL, args, web) != GraphicsQuality.Choice.High;
            foreach (var r in rows)
            {
                if (r.Key == LanguageKey || r.Key == QualityKey || instrument && GraphicsKeys.Contains(r.Key)) continue;
                if ((r.Key == VSyncKey || r.Key == FrameCapKey) && !GraphicsQuality.VSyncAtStart(args)) continue;
                var stored = store?.Read(r.Key);
                if (stored == null || !r.Values.Contains(stored) || stored == r.Current()) continue;
                if (r.Key == TiltShiftKey && stored != "off") continue;
                r.Apply(stored);
            }
            var preset = store?.Read(QualityKey);
            var start = GraphicsQuality.AtStart(preset == "medium" ? "high" : preset, Application.absoluteURL, args, web);   // Medium is High's tier
            if (start != GraphicsQuality.Current) GraphicsQuality.Apply(start);
            ShowContrast();
            ShowMotion();
        }

        // ---- Building and showing -----------------------------------------------------------------------------------

        void Hook(Button b, Action a) { b.clicked += a; hooks.Add((b, a)); }

        void Add(Row r)
        {
            var line = new VisualElement { name = "settings-" + r.Name + "-row" }.WithClass("row").WithClass("settings-row");
            line.Add(new Label(text[r.Label]) { name = "settings-" + r.Name + "-label" }.WithClass("settings-name"));
            switch (r.Kind)
            {
                case Kind.Switch:
                    r.Switch = new Button { name = "settings-" + r.Name }.WithClass("settings-switch");
                    Hook(r.Switch, () => Pick(r, r.Current() == "on" ? "off" : "on"));
                    line.Add(r.Switch);
                    break;
                case Kind.Choices:
                    var choices = new VisualElement().WithClass("row").WithClass("settings-choices");
                    foreach (var v in r.Values)
                    {
                        var b = new Button { name = "settings-" + r.Name + "-" + v, text = r.Text(v) }.WithClass("settings-switch");
                        var value = v;
                        Hook(b, () => Pick(r, value));
                        r.Buttons.Add((b, v));
                        choices.Add(b);
                    }
                    line.Add(choices);
                    break;
                case Kind.Stepper:
                    var stepper = new VisualElement().WithClass("row").WithClass("settings-choices");
                    // The chevrons point outward from the value, so in the mirrored Arabic row "less" is on the right, pointing right.
                    bool arabic = FamilyGame.Arabic(text);
                    r.Prev = new Button { name = "settings-" + r.Name + "-prev", tooltip = text["settings.less"] }.WithClass("settings-step");
                    r.Next = new Button { name = "settings-" + r.Name + "-next", tooltip = text["settings.more"] }.WithClass("settings-step");
                    r.Prev.Add(new UiIcon { Glyph = arabic ? "chevron" : "chevron-back" }.WithClass("settings-step-icon"));
                    r.Next.Add(new UiIcon { Glyph = arabic ? "chevron-back" : "chevron" }.WithClass("settings-step-icon"));
                    r.Value = new Label { name = "settings-" + r.Name + "-value" }.WithClass("settings-value");
                    Hook(r.Prev, () => Move(r, -1));
                    Hook(r.Next, () => Move(r, +1));
                    stepper.Add(r.Prev); stepper.Add(r.Value); stepper.Add(r.Next);
                    line.Add(stepper);
                    break;
            }
            tabs[r.Tab].page.Add(line);
        }

        void Move(Row r, int by)
        {
            int i = Array.IndexOf(r.Values, r.Current());
            int to = Mathf.Clamp(i < 0 ? 0 : i + by, 0, r.Values.Length - 1);
            if (to != i) Pick(r, r.Values[to]);
        }

        void Pick(Row r, string value)
        {
            if (!r.Enabled() || value == r.Current()) return;
            Keep(r.Key, value);   // first: the language switch disposes this presenter
            r.Apply(value);
            if (r.Key != LanguageKey) Show();
        }

        void Keep(string key, string value)
        {
            try { store?.Write(key, value); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Settings: could not keep the {key} choice ({e.Message}); it holds until the page is closed");
            }
        }

        /// <summary>Mutes the narration or turns it back on (the switch, and N in GameRoot), and keeps the choice.</summary>
        public void FlipNarration() => Pick(rows.First(r => r.Key == NarrationKey), Narrator.Muted ? "on" : "off");

        /// <summary>Shows <paramref name="t"/>'s page and marks its tab; kept for the session, so Settings opens where it was left.</summary>
        public void ShowTab(string t)
        {
            tab = t;
            foreach (var (name, (button, page)) in tabs)
            {
                page.style.display = name == t ? DisplayStyle.Flex : DisplayStyle.None;
                button.EnableInClassList(TabSelectedClass, name == t);
            }
            body.scrollOffset = Vector2.zero;
            unheldTab = "";   // the new tab is measured afresh: a hold sized for the last tab must not clip it
            screen.style.maxHeight = StyleKeyword.Null;
        }

        /// <summary>The open tab.</summary>
        public static string Tab => tab;

        /// <summary>A short tab leaves no blank panel under it (playtest of the web build: Display there has only the
        /// frame-rate limit, Language one row): the screen is held to its content's height. Only when that is shorter
        /// than the room the stylesheet's own top and bottom give it; a tab as tall as the room or taller keeps those
        /// limits, with the body scrolling, so the desktop's long tabs look as before. Should the held body ever need to
        /// scroll (a row grew after the measure), the hold is dropped for that tab.</summary>
        void Fit()
        {
            if (screen.resolvedStyle.display == DisplayStyle.None) return;   // measured when it opens
            bool held = screen.style.maxHeight.keyword == StyleKeyword.Undefined;
            if (held && body.contentContainer.layout.height > body.contentViewport.layout.height + 1f)
            {
                unheldTab = tab;
                screen.style.maxHeight = StyleKeyword.Null;
                return;
            }
            float need = ContentHeight(screen, body), room = Room(screen);
            if (unheldTab == tab || !(need > 0f) || !(room > 0f) || need >= room - 1f)
            {
                if (held) screen.style.maxHeight = StyleKeyword.Null;
                return;
            }
            var want = new StyleLength(need);
            if (screen.style.maxHeight != want) screen.style.maxHeight = want;   // only on a change: setting it lays out again
        }

        string unheldTab = "";

        /// <summary>The height the stylesheet gives <paramref name="screen"/>: its parent's, less its own top and bottom.</summary>
        internal static float Room(VisualElement screen)
        {
            var p = screen.parent;
            if (p == null) return 0f;
            var ps = p.resolvedStyle;
            return p.layout.height - ps.borderTopWidth - ps.borderBottomWidth - ps.paddingTop - ps.paddingBottom
                - screen.resolvedStyle.top - screen.resolvedStyle.bottom;
        }

        /// <summary>The height <paramref name="screen"/> needs to show all of its laid-out children, with <paramref
        /// name="body"/> at its content's own height rather than the height it was stretched to.</summary>
        internal static float ContentHeight(VisualElement screen, ScrollView body)
        {
            var s = screen.resolvedStyle;
            float h = s.paddingTop + s.paddingBottom + s.borderTopWidth + s.borderBottomWidth;
            foreach (var c in screen.Children())
            {
                var cs = c.resolvedStyle;
                if (cs.display == DisplayStyle.None || cs.position == Position.Absolute) continue;   // the frame overlay
                float own = c == body
                    ? BodyContent(body) + cs.paddingTop + cs.paddingBottom + cs.borderTopWidth + cs.borderBottomWidth
                    : c.layout.height;
                if (float.IsNaN(own)) return 0f;
                h += own + cs.marginTop + cs.marginBottom;
            }
            return Mathf.Ceil(h) + 1f;
        }

        /// <summary>The body's content height: its container's, or its shown pages' own, whichever is more.</summary>
        static float BodyContent(ScrollView body)
        {
            var cc = body.contentContainer;
            var cs = cc.resolvedStyle;
            float pages = cs.paddingTop + cs.paddingBottom;
            foreach (var p in cc.Children())
            {
                var s = p.resolvedStyle;
                if (s.display != DisplayStyle.None) pages += p.layout.height + s.marginTop + s.marginBottom;
            }
            return Mathf.Max(cc.layout.height, pages);
        }

        /// <summary>Tests: the session's display and accessibility choices and the open tab back to their defaults (a new session).</summary>
        internal static void Reset() { tab = Tabs[0]; window = resolution = ""; textSize = 100; contrast = false; baseReference = null; }

        void Show()
        {
            foreach (var r in rows)
            {
                var now = r.Current(); bool enabled = r.Enabled();
                if (r.Switch != null)
                {
                    r.Switch.text = text[now == "on" ? "settings.on" : "settings.off"]; r.Switch.SetEnabled(enabled);
                    r.Switch.EnableInClassList(SelectedClass, now == "on");   // audit m11: On reads as set, the gold fill the choices use
                }
                foreach (var (b, v) in r.Buttons) { b.EnableInClassList(SelectedClass, v == now); b.SetEnabled(enabled); }
                if (r.Value != null) r.Value.text = r.Text(now);
                if (r.Prev != null) r.Prev.SetEnabled(enabled && Array.IndexOf(r.Values, now) != 0);
                if (r.Next != null) r.Next.SetEnabled(enabled && Array.IndexOf(r.Values, now) != r.Values.Length - 1);
            }
        }

        // ---- Controls -----------------------------------------------------------------------------------------------

        /// <summary>The Controls tab: the game's keys, read-only. The camera's come from its action map (InputSystem.actions'
        /// Camera map, which CameraController reads), the others are the keys GameRoot polls; key names in the UI's language
        /// (controls.key.*), letters and the arrows as they are printed on the keys.</summary>
        void AddControls(VisualElement page)
        {
            page.Add(new Label(text["settings.controls_intro"]) { name = "settings-controls-intro" }.WithClass("settings-note"));
            foreach (var (id, keys) in Controls(text))
            {
                var line = new VisualElement { name = "settings-control-" + id }.WithClass("row").WithClass("settings-row");
                line.Add(new Label(text["controls." + id]).WithClass("settings-name"));
                var keyLabel = new Label(keys) { name = "settings-control-" + id + "-keys" }.WithClass("settings-keys");
                // A key's Latin name first ("Esc · زر Start ...") would make UiDirection read the line as English: in Arabic a
                // line with Arabic words in it is right to left, so its order reads from the right.
                if (FamilyGame.Arabic(text) && UiDirection.HasRightToLeft(keys))
                {
                    keyLabel.AddToClassList(UiDirection.OwnDirectionClass);
                    keyLabel.languageDirection = LanguageDirection.RTL;
                }
                line.Add(keyLabel);
                page.Add(line);
            }
        }

        /// <summary>Each control's id (its words: controls.&lt;id&gt;) and its keys.</summary>
        public static IReadOnlyList<(string id, string keys)> Controls(UiText text)
        {
            var list = new List<(string, string)> { ("pause", "Esc · " + text["controls.key.start"]), ("end_turn", "Enter · " + text["controls.key.keypad_enter"]) };
            var camera = UnityEngine.InputSystem.InputSystem.actions?.FindActionMap("Camera");
            foreach (var (action, id) in new[] { ("Pan", "pan"), ("DragPan", "drag"), ("Zoom", "zoom"), ("Orbit", "orbit") })
            {
                var a = camera?.FindAction(action);
                if (a != null) list.Add((id, Keys(a, text)));
            }
            list.Add(("narration", "N"));
            list.Add(("scroll", "Page Up · Page Down · Home · End"));
            list.Add(("focus", "Tab · Shift + Tab"));
            list.Add(("press", "Enter · " + text["controls.key.space"]));
            return list;
        }

        /// <summary>An action's bindings in words: a composite's parts together (W A S D, in that order; the right button +
        /// mouse movement), the bindings apart (·).</summary>
        static string Keys(UnityEngine.InputSystem.InputAction a, UiText text)
        {
            var order = new[] { "up", "left", "down", "right" };
            var groups = new List<string>(); List<(string name, string key)>? parts = null; string joiner = " ";
            void Close()
            {
                if (parts != null && parts.Count > 0)
                    groups.Add(string.Join(joiner, parts.OrderBy(p => Array.IndexOf(order, p.name)).Select(p => p.key)));
                parts = null;
            }
            foreach (var b in a.bindings)
            {
                if (b.isComposite) { Close(); parts = new List<(string, string)>(); joiner = b.path == "OneModifier" ? " + " : " "; continue; }
                var key = Key(b.effectivePath, text);
                if (b.isPartOfComposite && parts != null) { parts.Add((b.name, key)); continue; }
                Close();
                groups.Add(key);
            }
            Close();
            return string.Join(" · ", groups);
        }

        /// <summary>A control path's name: a key's letter, an arrow (in words in Arabic: its fonts have no arrow glyphs), or the mouse's buttons and wheel in the UI's language.</summary>
        public static string Key(string path, UiText text)
        {
            var control = path.Substring(path.IndexOf('/') + 1);
            switch (control)
            {
                case "upArrow": return text["controls.key.up"];
                case "downArrow": return text["controls.key.down"];
                case "leftArrow": return text["controls.key.left"];
                case "rightArrow": return text["controls.key.right"];
                case "scroll/y": case "scroll": return text["controls.key.wheel"];
                case "rightButton": return text["controls.key.right_button"];
                case "delta": return text["controls.key.mouse_move"];
            }
            return control.Length == 1 ? control.ToUpperInvariant() : UnityEngine.InputSystem.InputControlPath.ToHumanReadableString(path,
                UnityEngine.InputSystem.InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        public void Dispose()
        {
            foreach (var (b, a) in hooks) b.clicked -= a;
            hooks.Clear();
            GraphicsQuality.Changed -= onQuality;
            screen.UnregisterCallback(onGeometry);
            body.contentContainer.UnregisterCallback(onGeometry);
            screen.style.maxHeight = StyleKeyword.Null;
            if (screen.userData is Action) screen.userData = null;
        }
    }
}
