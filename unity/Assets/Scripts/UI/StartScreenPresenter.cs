#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using StoryOfTheLand.Game.Boot;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The start screen: the map picture, paragraph 11 of UN General Assembly resolution 194 in the United Nations'
    /// official Arabic with the English as adopted under it (the lines live in Game.uxml; StartScreenTests checks them
    /// against docs/sources/unga-194), and Continue / New Game. GameRoot
    /// shows it over the started game before the first turn; automated runs skip it (Skipped).
    /// Continue shows only when the session continues the family autosave (GameStart.ContinueFamily) and simply closes the
    /// screen: that session is the save, already replayed. New game opens the chapter and village picker over the screen
    /// (<c>pick</c>), at once with no save, and with a save after NewGamePresenter's two presses, the same flow as the top
    /// bar's New game, so one stray press never wipes the save (#175, B6); the screen stays up until the new game starts.
    /// Continue and Escape withdraw an open request.
    /// While it shows, the root has OpenClass (StoryOfTheLand.uss hides the HUD under it, so Tab reaches only these buttons)
    /// and <c>shownChanged</c> is told, so GameRoot can stop the camera keys. The panel's source line names the sources.
    /// Playtest 3: a corner toggle (EN | عربي, each in its own script) switches the UI's language on the screen itself, and
    /// How to play shows the first-turn hint.</summary>
    public sealed class StartScreenPresenter : IDisposable
    {
        /// <summary>On the "root" element while the start screen shows.</summary>
        public const string OpenClass = "start-open";
        /// <summary>On the start screen when the painting is a full-width band with the panel under it (Fit).</summary>
        public const string BandClass = "start-band";
        /// <summary>The painting's width over its height (Assets/UI/StartScreen/start-map.png, 1920x1080).</summary>
        public const float ArtAspect = 16f / 9f;
        /// <summary>The URL flag (WebGL) or command-line flag (desktop, "-nostart") that skips the screen by hand.</summary>
        public const string NoStartFlag = "nostart";

        /// <summary>Whether the screen is skipped: the automated runs (the golden check, the smoke run, the shot, frame and
        /// pixel-error profiles and the border proof, all of which drive or measure the map straight away),
        /// or "nostart" asked for by hand. Flags follow each instrument's own rule: a whole URL query parameter on WebGL
        /// (UrlFlags.Has), a command-line flag on desktop.</summary>
        public static bool Skipped(string? url, string[] args, bool webgl)
        {
            if (GameRoot.SmokeTurns(url, args, webgl) > 0) return true;
            var flags = new[] { NoStartFlag, "golden", "shot", "vprofile", "profile", StoryOfTheLand.Game.Map.BorderProof.Flag };
            return webgl ? flags.Any(f => UrlFlags.Has(url, f)) : flags.Any(f => args.Contains("-" + f));
        }

        public static bool Skipped() =>
            Skipped(Application.absoluteURL, Environment.GetCommandLineArgs(), Application.platform == RuntimePlatform.WebGLPlayer);

        readonly VisualElement hudRoot, screen, art, body, panel;
        readonly Button continueButton, newGameButton, howToButton;
        readonly VisualElement languages;
        readonly (Button button, string language)[] languageButtons;
        readonly Action<string>? switchLanguage;
        readonly Action? howToPlay;
        readonly List<(Button, Action)> hooks = new List<(Button, Action)>();
        readonly NewGamePresenter newGame;
        readonly Action<bool>? shownChanged;
        readonly EventCallback<NavigationCancelEvent> onCancel;
        readonly EventCallback<GeometryChangedEvent> onGeometry;
        readonly Action onContinue, onNewGame;
        /// <summary>New game: opens the picker over the screen.</summary>
        readonly Action pick;
        readonly PressSource presses;

        /// <summary>Continue is offered: the session continues the autosave.</summary>
        public bool Resumed { get; private set; }

        /// <param name="text">The UI's strings for the source line and the two buttons (#193); null keeps Game.uxml's English
        /// (tests). The title is the game's name and the English lines are the Declaration's official translation: neither
        /// is translated.</param>
        /// <param name="pick">New game: opens the chapter and village picker over the screen, which stays up until the new
        /// game starts (with a save to lose, after NewGamePresenter's second press).</param>
        /// <param name="switchLanguage">The corner toggle: puts the UI in the language pressed (GameRoot builds the screens
        /// again, the start screen up); null hides the toggle.</param>
        /// <param name="howToPlay">How to play: shows the first-turn hint; null hides the button.</param>
        public StartScreenPresenter(VisualElement root, NewGamePresenter newGame, bool resumed, Action pick, Action<bool>? shownChanged = null,
            UiText? text = null, Action<string>? switchLanguage = null, Action? howToPlay = null)
        {
            this.newGame = newGame; this.shownChanged = shownChanged; Resumed = resumed; this.pick = pick;
            this.switchLanguage = switchLanguage; this.howToPlay = howToPlay;
            hudRoot = root.Q("root") ?? root;
            screen = root.Q<VisualElement>("start-screen");
            art = screen.Q<VisualElement>("start-art");
            body = screen.Q<VisualElement>("start-body");
            panel = screen.Q<VisualElement>("start-panel");
            continueButton = screen.Q<Button>("start-continue");
            newGameButton = screen.Q<Button>("start-new-game");
            howToButton = screen.Q<Button>("start-how-to-play");
            languages = screen.Q<VisualElement>("start-languages");
            if (text != null)
            {
                screen.Q<Label>("start-source").text = text["start.source"];
                continueButton.text = text["start.continue"]; newGameButton.text = text["start.new_game"];
                howToButton.text = text["hud.nav.how_to_play"];
            }
            var current = text?["ui.lang"] ?? UiLanguage.English;
            languageButtons = new[] { UiLanguage.English, UiLanguage.Arabic }.Select(l => (screen.Q<Button>("start-language-" + l), l)).ToArray();
            languages.style.display = switchLanguage == null ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (var (button, language) in languageButtons)
            {
                button.EnableInClassList(SettingsPresenter.SelectedClass, language == current);
                Hook(button, () => { if (IsOpen && language != current) this.switchLanguage?.Invoke(language); });
            }
            howToButton.style.display = howToPlay == null ? DisplayStyle.None : DisplayStyle.Flex;
            Hook(howToButton, () => { if (IsOpen) this.howToPlay?.Invoke(); });
            onContinue = Continue; onNewGame = NewGame;
            continueButton.clicked += onContinue;
            newGameButton.clicked += onNewGame;
            // Escape (the UI's Cancel) from a focused start button withdraws an open New Game request. GameRoot also polls
            // the Escape key for when no element has the focus (Escape); doing it twice is harmless.
            onCancel = e => { if (Escape()) e.StopPropagation(); };
            screen.RegisterCallback(onCancel);
            onGeometry = _ => Fit();
            screen.RegisterCallback(onGeometry);
            panel.RegisterCallback(onGeometry);                // the panel's height decides where it goes
            presses = new PressSource(screen);
        }

        void Hook(Button b, Action a) { b.clicked += a; hooks.Add((b, a)); }

        public bool IsOpen => hudRoot.ClassListContains(OpenClass);

        /// <summary>Shows the screen with Continue only when there is a save to continue, and puts the keyboard focus on the
        /// first button.</summary>
        public void Show()
        {
            continueButton.style.display = Resumed ? DisplayStyle.Flex : DisplayStyle.None;
            hudRoot.AddToClassList(OpenClass);
            ScreenNav.DisableCovered(hudRoot);   // the HUD under it: hidden by the stylesheet, disabled so no key reaches it
            Fit();
            // Focus once the styles have resolved (a button still hidden by the stylesheet cannot take it).
            var first = Resumed ? continueButton : newGameButton;
            first.Focus();
            screen.schedule.Execute(() => { if (IsOpen && first.focusController?.focusedElement == null) first.Focus(); });
            DiagLog.BootPhase("start screen shown");
            shownChanged?.Invoke(true);
        }

        /// <summary>Hides the screen; the game is on the map. Safe to call when it is not open. Closed from the keyboard, the
        /// focus goes to End Turn (#41 M1), unless a decision card is open: the card takes it (EventCardPresenter).</summary>
        public void Close() => Hide(giveFocus: true);

        void Hide(bool giveFocus)
        {
            if (!IsOpen) return;
            hudRoot.RemoveFromClassList(OpenClass);
            ScreenNav.DisableCovered(hudRoot);
            var focused = screen.focusController?.focusedElement as VisualElement;
            if (focused != null && screen.Contains(focused))
            {
                focused.Blur();
                if (giveFocus && presses.ByKeyboard && !hudRoot.ClassListContains(EventCardPresenter.OpenClass)) FocusReturn.To(hudRoot.Q<Button>("end-turn"));
            }
            shownChanged?.Invoke(false);
        }

        /// <summary>Escape on the screen: withdraws an open New Game request. True when it did.</summary>
        public bool Escape()
        {
            if (!IsOpen || !newGame.Armed) return false;
            newGame.Cancel();
            return true;
        }

        void Continue()
        {
            if (!IsOpen || !Resumed) return;
            newGame.Cancel();
            Close();
        }

        void NewGame()
        {
            if (!IsOpen) return;
            if (!Resumed) { pick(); return; }     // nothing to wipe: the picker at once
            newGame.Press();                      // arms on the first press; the second opens the picker
        }

        /// <summary>Lays the painting out for the window. At 16:9 and wider it fills the window (cover, cropped at the
        /// bottom) with the panel at the foot over the audience. In a narrower window that is tall enough for the painting's
        /// full-width 16:9 band and the panel under it (a phone held upright, a square window), the band and the panel are
        /// stacked and centred together (BandClass), so nothing of the painting is cropped. Otherwise (16:10, 4:3) it fills
        /// the window anchored at its top centre, so both sides are cropped alike and the aisle stays in the middle.</summary>
        void Fit()
        {
            var size = screen.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0 || size.y <= 0) return;
            var bandHeight = size.x / ArtAspect;
            var panelHeight = panel.layout.height;
            var band = size.x / size.y < ArtAspect - 0.001f && !float.IsNaN(panelHeight) && panelHeight > 0
                && bandHeight + panelHeight + BodyPadding <= size.y;
            screen.EnableInClassList(BandClass, band);
            var top = band ? Mathf.Floor((size.y - bandHeight - panelHeight - BodyPadding) / 2f) : 0f;
            art.style.top = band ? top : new StyleLength(StyleKeyword.Null);
            art.style.height = band ? bandHeight : new StyleLength(StyleKeyword.Null);
            body.style.top = band ? top + bandHeight : new StyleLength(StyleKeyword.Null);
            body.style.justifyContent = band ? Justify.FlexStart : new StyleEnum<Justify>(StyleKeyword.Null);
        }

        /// <summary>The start body's top and bottom padding together in the band layout (StoryOfTheLand.uss .start-band .start-body).</summary>
        const float BodyPadding = 32f;

        public void Dispose()
        {
            Hide(giveFocus: false);
            presses.Dispose();
            continueButton.clicked -= onContinue;
            newGameButton.clicked -= onNewGame;
            foreach (var (b, a) in hooks) b.clicked -= a;
            screen.UnregisterCallback(onCancel);
            screen.UnregisterCallback(onGeometry);
            panel.UnregisterCallback(onGeometry);
        }
    }
}
