#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StoryOfTheLand.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>F6: the pause menu, as the big strategy games have it. GameRoot routes Escape (and a controller's Start): it
    /// opens the menu when nothing else is open (a screen, the decision card, the start screen and the picker each take it
    /// first) and closes it again; so does Resume. The game is turn-based, so pausing only stops input: a dim backdrop over the
    /// map, the HUD under it disabled (ScreenNav.DisableCovered, OpenClass) and the camera keys off (<c>shownChanged</c>).
    /// The items: Resume; Save game and Load game, over the family autosave and SaveStore.SlotCount named slots in the same
    /// save format (FamilyGame.SaveText), each slot named by its chapter, village, date and the time it was saved; Settings,
    /// How to play and Credits, which open their screen and bring the menu back when it closes (ScreenNav.Changed); Main menu
    /// and Quit to desktop, each after a confirm, the game saved first (GameRoot); Quit is not offered on the web
    /// (<c>quit</c> null). Escape steps back once: from a confirm or the slot list to the menu, from the menu to the game.
    /// The focus goes to the first item when the menu opens and back to what had it when it closes (FocusReturn).</summary>
    public sealed class PausePresenter : IDisposable
    {
        /// <summary>On the "root" element while the menu shows (StoryOfTheLand.uss shows it and dims the map).</summary>
        public const string OpenClass = "pause-open";
        /// <summary>The menu's items, top to bottom (buttons pause-&lt;item&gt; in Game.uxml).</summary>
        public static readonly string[] Items = { "resume", "save", "load", "chapters", "settings", "how-to-play", "credits", "main-menu", "quit" };

        readonly VisualElement root, hud, menu, items, slots, confirm, slotList, hint, end;
        readonly Label title, endSummary;
        readonly Button endNext;
        /// <summary>The chapter-end page's Next chapter (null: the last chapter) and Choose chapter.</summary>
        Action? onNext; readonly Action? chapters;
        readonly Label confirmText, slotsTitle;
        readonly Button yes, no, back;
        readonly UiText text; readonly ScreenNav nav; readonly SaveStore saves; readonly Func<string, Content> chapter;
        readonly Func<GameSession> current; readonly Action<GameSession> load; readonly Action mainMenu; readonly Action? quit;
        readonly Action<string> say; readonly Action<bool>? shownChanged;
        readonly List<(Button, Action)> hooks = new List<(Button, Action)>();
        readonly EventCallback<NavigationCancelEvent> onCancel;
        readonly Action onNav;
        /// <summary>What had the focus when the menu opened; the item whose screen is open (the menu comes back when it closes).</summary>
        VisualElement? before; string? returnTo;
        /// <summary>The confirm's action, the page and button it was asked from.</summary>
        Action? onYes; VisualElement? askedFrom; Button? askedBy;
        bool saving;
        int openedFrame = -1;

        /// <param name="saves">The family save store (SaveStore.Family): its autosave and its slots.</param>
        /// <param name="chapter">Loads a chapter's content (GameInputs.Chapter), to replay a slot.</param>
        /// <param name="current">The game being played, to save.</param>
        /// <param name="load">Plays a loaded game (GameRoot builds the screens again over it).</param>
        /// <param name="mainMenu">Saves and goes back to the start screen.</param>
        /// <param name="quit">Saves and quits; null hides Quit (the web).</param>
        /// <param name="say">Shows a line on the toast (a save made, a save that failed).</param>
        public PausePresenter(VisualElement root, UiText text, ScreenNav nav, SaveStore saves, Func<string, Content> chapter,
            Func<GameSession> current, Action<GameSession> load, Action mainMenu, Action? quit, Action<string> say,
            Action<bool>? shownChanged = null, Action? chapters = null)
        {
            this.chapters = chapters;
            this.root = root; this.text = text; this.nav = nav; this.saves = saves; this.chapter = chapter; this.current = current;
            this.load = load; this.mainMenu = mainMenu; this.quit = quit; this.say = say; this.shownChanged = shownChanged;
            hud = root.Q("root") ?? root;
            menu = root.Q<VisualElement>("pause-menu");
            items = menu.Q<VisualElement>("pause-items"); slots = menu.Q<VisualElement>("pause-slots"); confirm = menu.Q<VisualElement>("pause-confirm");
            slotList = menu.Q<VisualElement>("pause-slot-list");
            slotsTitle = menu.Q<Label>("pause-slots-title"); confirmText = menu.Q<Label>("pause-confirm-text");
            yes = menu.Q<Button>("pause-confirm-yes"); no = menu.Q<Button>("pause-confirm-no"); back = menu.Q<Button>("pause-slots-back");
            title = menu.Q<Label>("pause-title");
            title.text = text["pause.title"];
            // Playtest (F6): the first-turn hint and the chapter's end are pages of this panel, over the same dim backdrop.
            hint = menu.Q<VisualElement>("pause-hint"); end = menu.Q<VisualElement>("pause-end");
            foreach (var line in new[] { "goal", "icons", "delta", "turn" }) menu.Q<Label>("pause-hint-" + line).text = text["hint." + line];
            Hook(Item("hint-ok", "hint.ok"), Close);
            endSummary = menu.Q<Label>("pause-end-summary");
            endNext = Item("end-next", "chapter_end.next");
            Hook(endNext, () => { var next = onNext; Hide(giveFocus: false); next?.Invoke(); });
            Hook(Item("end-choose", "chapter_end.choose"), () => { Hide(giveFocus: false); this.chapters?.Invoke(); });
            Hook(Item("end-menu", "pause.main_menu"), () => { Hide(giveFocus: false); mainMenu(); });
            var chaptersButton = Item("chapters", "pause.chapters");
            chaptersButton.style.display = chapters == null ? DisplayStyle.None : DisplayStyle.Flex;
            // Playtest 2: the picker leads to a new game, which replaces this one: ask first.
            Hook(chaptersButton, () => Ask(text["restart.confirm"], text["restart.yes"], () => { Hide(giveFocus: false); this.chapters?.Invoke(); }));
            back.text = text["picker.back"]; no.text = text["pause.cancel"];
            Hook(Item("resume", "pause.resume"), Close);
            Hook(Item("save", "pause.save"), () => ShowSlots(saving: true));
            Hook(Item("load", "pause.load"), () => ShowSlots(saving: false));
            Hook(Item("settings", "hud.nav.settings"), () => OpenScreen("settings"));
            Hook(Item("how-to-play", "hud.nav.how_to_play"), () => OpenScreen("how-to-play"));
            Hook(Item("credits", "settings.credits"), () => OpenScreen(CreditsPresenter.Screen));
            Hook(Item("main-menu", "pause.main_menu"), () => Ask(text["pause.confirm_main_menu"], text["pause.main_menu"], mainMenu));
            var quitButton = Item("quit", "pause.quit");
            quitButton.style.display = quit == null ? DisplayStyle.None : DisplayStyle.Flex;
            Hook(quitButton, () => Ask(text["pause.confirm_quit"], text["pause.quit"], () => this.quit?.Invoke()));
            Hook(back, () => ShowItems(saving ? "save" : "load"));
            Hook(yes, () =>
            {
                var action = onYes; onYes = null;
                if (askedOutside) Hide(giveFocus: false); else ShowItems(null);
                askedOutside = false;
                action?.Invoke();
            });
            Hook(no, Back);
            // A controller's Cancel (and Escape from a focused item) steps back as GameRoot's Escape does. Not in the frame the
            // menu opened: the Escape that closed a screen and brought the menu back must not close it too.
            onCancel = e => { if (IsOpen && Escape()) e.StopPropagation(); };
            menu.RegisterCallback(onCancel);
            onNav = () =>
            {
                if (returnTo == null || nav.Current != null) return;
                var item = returnTo; returnTo = null;
                Open(item);
            };
            nav.Changed += onNav;
        }

        Button Item(string item, string key)
        {
            var b = menu.Q<Button>("pause-" + item);
            b.text = text[key];
            return b;
        }

        void Hook(Button b, Action a) { b.clicked += a; hooks.Add((b, a)); }

        public bool IsOpen => hud.ClassListContains(OpenClass);

        /// <summary>Whether a screen this menu opened is still open: the menu comes back when it closes.</summary>
        public bool Returning => returnTo != null;

        /// <summary>Brings the menu back when the open screen closes, focused on <paramref name="item"/> (the language switch
        /// builds the screens again with Settings open).</summary>
        public void ReturnAfterScreen(string item) => returnTo = item;

        /// <summary>Shows the menu with the focus on <paramref name="item"/> (the first item by default).</summary>
        public void Open(string? item = null)
        {
            if (IsOpen) return;
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            if (before == null && focused != null && !menu.Contains(focused)) before = focused;   // kept while a screen of ours was open
            focused?.Blur();   // nothing under the menu keeps the focus (a closed screen's X would, and FocusReturn waits for none)
            hud.AddToClassList(OpenClass);
            ScreenNav.DisableCovered(root);
            ShowItems(item ?? Items[0]);
            openedFrame = Time.frameCount;
            shownChanged?.Invoke(true);
        }

        /// <summary>Hides the menu; the game goes on. The focus goes back to what had it, unless the decision card takes it.</summary>
        public void Close() => Hide(giveFocus: true);

        void Hide(bool giveFocus)
        {
            if (!IsOpen) return;
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            hud.RemoveFromClassList(OpenClass);
            ScreenNav.DisableCovered(root);
            if (focused != null && menu.Contains(focused)) focused.Blur();
            if (giveFocus)
            {
                if (before?.panel != null && !hud.ClassListContains(EventCardPresenter.ModalClass)) FocusReturn.To(before);
                before = null;
            }
            onYes = null; focusing = null;
            shownChanged?.Invoke(false);
        }

        /// <summary>Escape: one step back (a confirm or the slot list to the menu, the menu to the game). True when the menu
        /// took it; false when the menu is shut.</summary>
        public bool Escape()
        {
            if (!IsOpen) return false;
            if (Application.isPlaying && Time.frameCount == openedFrame) return true;   // this Escape already brought the menu back
            if (Shown(confirm)) Back();
            else if (Shown(hint) || Shown(end)) Close();   // a hint or the chapter's end: back to the map
            else if (Shown(slots)) ShowItems(saving ? "save" : "load");
            else Close();
            return true;
        }

        /// <summary>The control the menu's focus goes to: now, and once more when the styles of a page just shown have resolved
        /// (FocusReturn's rule), the latest one winning, so two quick steps in one frame never leave the focus on the first.</summary>
        Button? focusing;
        void Focus(Button? b)
        {
            focusing = b;
            if (b == null) return;
            b.Focus();
            menu.schedule.Execute(() =>
            {
                if (focusing == b && IsOpen && b.panel != null && b.focusController?.focusedElement != b) b.Focus();
            });
        }

        static bool Shown(VisualElement e) => e.style.display.keyword == StyleKeyword.Undefined && e.style.display.value == DisplayStyle.Flex;

        void Page(VisualElement page)
        {
            // The focus leaves the page going away, so FocusReturn can put it on the new one once its styles have resolved.
            if (root.panel?.focusController?.focusedElement is VisualElement focused && menu.Contains(focused)) focused.Blur();
            foreach (var p in new[] { items, slots, confirm, hint, end }) p.style.display = p == page ? DisplayStyle.Flex : DisplayStyle.None;
            title.text = text[page == hint ? "hint.title" : page == end ? "chapter_end.title" : "pause.title"];
        }

        void ShowItems(string? focus)
        {
            Page(items);
            if (focus != null) Focus(menu.Q<Button>("pause-" + focus));
        }

        /// <summary>Audit m15: the arrow keys (GameRoot: up -1, down +1) move the focus through the open page's buttons as Tab
        /// and Shift+Tab do, round at the ends. False when the menu is shut.</summary>
        public bool MoveFocus(int by)
        {
            if (!IsOpen) return false;
            var page = new[] { items, slots, confirm, hint, end }.FirstOrDefault(Shown);
            if (page == null) return false;
            var buttons = page.Query<Button>().ToList().Where(b => !(b.style.display.keyword == StyleKeyword.Undefined && b.style.display.value == DisplayStyle.None)
                && b.enabledInHierarchy).ToList();   // an item hidden inline (Quit on the web, Chapters without a game) is skipped
            if (buttons.Count == 0) return false;
            var now = buttons.IndexOf(root.panel?.focusController?.focusedElement as Button);
            var next = now < 0 ? (by > 0 ? 0 : buttons.Count - 1) : (now + by + buttons.Count) % buttons.Count;
            Focus(buttons[next]);
            return true;
        }

        /// <summary>Playtest (F6): the first-turn hint (the goal, the bar's icons, End Turn), shown once per profile by GameRoot.</summary>
        public void ShowHint() => OpenOn(hint, menu.Q<Button>("pause-hint-ok"));

        /// <summary>Playtest (F6): the chapter's end: "Chapter complete", <paramref name="summary"/>, and Next chapter
        /// (<paramref name="next"/>; hidden after the last chapter), Choose chapter and Main menu.</summary>
        public void ShowChapterEnd(string summary, Action? next)
        {
            endSummary.text = summary; onNext = next;
            endNext.style.display = next == null ? DisplayStyle.None : DisplayStyle.Flex;
            OpenOn(end, next != null ? endNext : menu.Q<Button>("pause-end-choose"));
        }

        void OpenOn(VisualElement page, Button focus)
        {
            if (!IsOpen) Open();
            Page(page);
            Focus(focus);
        }

        /// <summary>Settings, How to play or Credits: the menu steps aside for the screen and comes back when it closes.</summary>
        void OpenScreen(string screen)
        {
            var item = screen == CreditsPresenter.Screen ? "credits" : screen;
            Hide(giveFocus: false);
            returnTo = item;
            nav.Open(screen);
            FocusReturn.To(root.Q("screen-" + screen)?.Q<Button>("screen-close-" + screen));   // a key press goes on inside the screen
        }

        void Ask(string question, string action, Action then)
        {
            askedFrom = Shown(slots) ? slots : items;
            askedBy = root.panel?.focusController?.focusedElement as Button;
            confirmText.text = question; yes.text = action; onYes = then;
            Page(confirm);
            title.text = action;   // audit m9: the confirm names its action ("Main menu"), not "Paused"
            Focus(no);   // the safe answer first
        }

        /// <summary>Playtest 2: asks <paramref name="question"/> on the menu's confirm page from outside the menu (New game over
        /// a game in progress); Cancel or Escape closes the menu again, the answer runs <paramref name="then"/>.</summary>
        public void Confirm(string question, string action, Action then)
        {
            bool open = IsOpen;
            if (!open) Open();
            Ask(question, action, then);
            askedOutside = !open;
        }

        /// <summary>The confirm was asked from outside the menu (Confirm): its answer closes the menu.</summary>
        bool askedOutside;

        void Back()
        {
            onYes = null;
            if (askedOutside) { askedOutside = false; Close(); return; }
            Page(askedFrom ?? items);
            if (askedBy?.panel != null && askedBy.enabledInHierarchy) Focus(askedBy);
        }

        // ---- Save and load ------------------------------------------------------------------------------------------

        /// <summary>The slot list: to save, the named slots; to load, the autosave first, then the slots, an empty or unreadable
        /// one disabled.</summary>
        void ShowSlots(bool saving, int focusSlot = 0)
        {
            this.saving = saving;
            slotsTitle.text = text[saving ? "pause.save" : "pause.load"];
            slotList.Clear();
            Button? first = null;
            if (!saving) first = AddSlot("autosave", text["slots.autosave"], saves, 0);
            for (int n = 1; n <= SaveStore.SlotCount; n++)
            {
                var b = AddSlot("slot-" + n, text.Format("slots.slot", n), saves.Slot(n), n);
                if (first == null && b.enabledSelf || n == focusSlot) first = b;
            }
            Page(slots);
            Focus(first != null && first.enabledSelf ? first : back);
        }

        Button AddSlot(string id, string name, SaveStore store, int n)
        {
            var session = GameStart.Peek(chapter, store, out var readable);
            var at = store.SavedAt;
            var b = new Button { name = "pause-" + id };
            b.AddToClassList("pause-slot");
            b.Add(new Label(name).WithClass("pause-slot-name"));
            b.Add(new Label(session != null ? Describe(session) : !readable ? text["slots.unreadable"] : text["slots.empty"]).WithClass("pause-slot-detail"));
            if (session != null && at != null) b.Add(new Label(text.Format("slots.saved_at", When(at.Value))).WithClass("pause-slot-time"));
            b.SetEnabled(saving || session != null);
            b.clicked += () => Pick(store, n, session, used: session != null || !readable);
            slotList.Add(b);
            return b;
        }

        /// <summary>A save's chapter, village and date in the UI's language.</summary>
        string Describe(GameSession s)
        {
            bool arabic = FamilyGame.Arabic(text);
            var id = FamilyGame.ChapterOf(s.Content);
            var place = s.Content.Places.FirstOrDefault(p => p.Id == s.State.Village);
            return text.Format("slots.detail", text.Has("chapter." + id) ? text["chapter." + id] : id,
                place != null ? FamilyGame.Name(place, arabic) : s.State.Village, PlayerText.Month(s.State.Date, arabic));   // the turn's month, as the HUD
        }

        /// <summary>The time a save was written: its date as the HUD says dates (Levantine months in Arabic) and the time on
        /// the 24-hour clock, in Western digits.</summary>
        public string When(DateTime t) =>
            text.Format("slots.time", PlayerText.Date(t, FamilyGame.Arabic(text)), t.ToString("HH:mm", CultureInfo.InvariantCulture));

        void Pick(SaveStore store, int n, GameSession? session, bool used)
        {
            if (saving)
            {
                void Save()
                {
                    try { store.WriteAutosave(FamilyGame.SaveText(current())); say(text.Format("slots.saved", n)); }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Slot {n} could not be written: {e}");
                        say(text["save.write_failed"]);
                    }
                    ShowSlots(saving: true, focusSlot: n);
                }
                if (used) Ask(text.Format("slots.overwrite", n), text["slots.replace"], Save);
                else Save();
                return;
            }
            if (session != null) Ask(text["slots.load_confirm"], text["slots.load"], () => { Hide(giveFocus: false); load(session); });
        }

        public void Dispose()
        {
            Hide(giveFocus: false);
            foreach (var (b, a) in hooks) b.clicked -= a;
            hooks.Clear();
            menu.UnregisterCallback(onCancel);
            nav.Changed -= onNav;
            slotList.Clear();
        }
    }
}
