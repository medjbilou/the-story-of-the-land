#nullable enable
using System;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The in-game footage viewer (owner, 2026-10-22: watch a clip without leaving the game). After the card's content
    /// note and Open, a signed-off Archive Genocide file (Clip.MediaUrl, kind video or photo) plays in a panel over the card,
    /// under the pause menu: a VideoPlayer streams the address into a RenderTexture that the panel draws (a photo comes
    /// through UnityWebRequestTexture), with play/pause, a scrub bar, mute (it starts unmuted: its AudioSource is at full
    /// volume under the master volume, AudioListener.volume), close (Esc too), the caption in the UI's language and the credit
    /// "Archive Genocide · the archive · original post", each a link to the browser. The game's music holds while a video is
    /// open (MusicPlayer.Held). Nothing is written to disk: the file is streamed, never kept. A file that does not load says
    /// so and offers the browser instead.
    /// Desktop players only: media.archivegenocide.com sends no Access-Control-Allow-Origin header, so the web player cannot
    /// read its files (Plays is false there) and the card opens them in a new tab, as it does a publisher's page.</summary>
    public sealed class FootageViewer : IDisposable
    {
        public const string OverlayName = "footage-viewer";
        readonly VisualElement overlay, screen, controls, credit;
        readonly Label title, status, caption, archive;
        readonly Button play, mute, close, browser, post, source;
        readonly Slider progress;
        readonly UiText text;
        readonly Action<string> openUrl;
        readonly IVisualElementScheduledItem tick;
        Clip? clip;
        GameObject? host;
        AudioSource? sound;
        UnityWebRequest? photo;
        Texture2D? still;

        /// <summary>What Close does after it has taken the panel down (the card takes the focus back).</summary>
        public Action? Closed { get; set; }
        /// <summary>The clip on show, or null.</summary>
        public Clip? Showing => clip;
        public bool IsOpen => clip != null;
        /// <summary>The video's player and its target while a video is open (for the tests: a closed viewer holds neither).</summary>
        internal VideoPlayer? Video { get; private set; }
        internal RenderTexture? Target { get; private set; }
        /// <summary>True once the video or photo is on the screen; false while it loads or after an error.</summary>
        public bool Loaded { get; private set; }
        public bool Failed { get; private set; }

        /// <summary>True when <paramref name="clip"/> plays in the game: a direct video or photo file, in a desktop player.</summary>
        public static bool Plays(Clip clip, bool web) => !web && (clip.Kind == "video" || clip.Kind == "photo") && clip.MediaUrl.Length > 0;

        /// <summary>The address the browser opens for <paramref name="clip"/>: the file itself, else the publisher's page.</summary>
        public static string BrowserUrl(Clip clip) => clip.MediaUrl.Length > 0 ? clip.MediaUrl : clip.Url;

        /// <param name="after">The element the panel goes over (the card's layer): it is put just after it, under the pause menu.</param>
        public FootageViewer(VisualElement after, UiText text, Action<string> openUrl)
        {
            this.text = text; this.openUrl = openUrl;
            overlay = new VisualElement { name = OverlayName, focusable = false };
            overlay.AddToClassList(OverlayName);
            var panel = new VisualElement { name = "footage-panel" };
            panel.AddToClassList("panel"); panel.AddToClassList("footage-panel");
            overlay.Add(panel);
            panel.Add(title = new Label { name = "footage-title" }.WithClass("footage-title"));
            panel.Add(screen = new VisualElement { name = "footage-screen" }.WithClass("footage-screen"));
            screen.Add(status = new Label { name = "footage-status" }.WithClass("footage-status"));
            screen.Add(browser = new Button(() => { if (clip != null) openUrl(BrowserUrl(clip)); }) { name = "footage-browser", text = text["footage.open_browser"] });
            controls = new VisualElement { name = "footage-controls" }.WithClass("footage-controls");
            controls.Add(play = new Button(PlayPause) { name = "footage-play" });
            controls.Add(progress = new Slider(0f, 1f) { name = "footage-progress", tooltip = text["footage.progress"] }.WithClass("footage-progress"));
            controls.Add(mute = new Button(Mute) { name = "footage-mute" });
            panel.Add(controls);
            panel.Add(caption = new Label { name = "footage-caption" }.WithClass("footage-caption"));
            credit = new VisualElement { name = "footage-credit" }.WithClass("footage-credit");
            credit.Add(source = new Button(() => { if (clip != null) openUrl(clip.Url); }) { name = "footage-source", text = text["footage.archive"] }.WithClass(SourceLinks.LinkClass));
            credit.Add(archive = new Label { name = "footage-archive" }.WithClass("source-caption"));
            credit.Add(post = new Button(() => { if (clip != null) openUrl(clip.PostUrl); }) { name = "footage-post", text = text["footage.original_post"] }.WithClass(SourceLinks.LinkClass));
            panel.Add(credit);
            panel.Add(close = new Button(Close) { name = "footage-close", text = text["footage.close"] }.WithClass("footage-close"));
            progress.RegisterValueChangedCallback(e =>
            {
                if (Video != null && Video.length > 0) Video.time = e.newValue * Video.length;   // the player scrubs; Tick moves it without notice
            });
            overlay.RegisterCallback<NavigationCancelEvent>(e => { Close(); e.StopPropagation(); });   // Esc with the focus in the panel
            overlay.style.display = DisplayStyle.None;
            after.parent.Insert(after.parent.IndexOf(after) + 1, overlay);
            tick = overlay.schedule.Execute(Tick).Every(250);
            tick.Pause();
        }

        /// <summary>Opens the panel on <paramref name="c"/> and starts loading it (Plays must be true for it).</summary>
        public void Show(Clip c, bool arabic)
        {
            Close();
            clip = c; Loaded = false; Failed = false;
            title.text = c.TitleIn(arabic);
            caption.text = arabic ? c.DescriptionAr : c.Description;
            bool archived = c.AgEntryId.Length > 0;
            source.style.display = archived ? DisplayStyle.Flex : DisplayStyle.None;
            archive.text = archived ? "· " + c.Publisher + (c.PostUrl.Length > 0 ? " ·" : "") : c.Publisher;
            post.style.display = c.PostUrl.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            status.text = text["footage.loading"]; status.style.display = DisplayStyle.Flex;
            browser.style.display = DisplayStyle.None;
            bool video = c.Kind == "video";
            controls.style.display = video ? DisplayStyle.Flex : DisplayStyle.None;
            progress.SetValueWithoutNotify(0f);
            overlay.style.display = DisplayStyle.Flex;
            if (video) StartVideo(c.MediaUrl); else StartPhoto(c.MediaUrl);
            Labels();
            close.Focus();
        }

        void StartVideo(string url)
        {
            host = new GameObject("FootageViewer") { hideFlags = HideFlags.HideAndDontSave };
            sound = host.AddComponent<AudioSource>();
            sound.playOnAwake = false; sound.spatialBlend = 0f; sound.volume = 1f;   // under the master volume (AudioListener.volume)
            var v = Video = host.AddComponent<VideoPlayer>();
            v.playOnAwake = false; v.isLooping = false; v.skipOnDrop = true; v.waitForFirstFrame = true;
            v.source = VideoSource.Url; v.url = url;
            v.renderMode = VideoRenderMode.RenderTexture;
            v.audioOutputMode = VideoAudioOutputMode.AudioSource;
            v.controlledAudioTrackCount = 1; v.EnableAudioTrack(0, true); v.SetTargetAudioSource(0, sound);
            v.prepareCompleted += Prepared;
            v.errorReceived += (p, message) => { if (p == Video) Fail(message); };
            v.loopPointReached += p => { if (p == Video) Labels(); };
            MusicPlayer.Held = true;
            v.Prepare();
        }

        void Prepared(VideoPlayer v)
        {
            if (v != Video) return;
            // The video's own size, at most 1920 wide: the panel scales it to fit (USS background-size: contain).
            int w = (int)Math.Max(1, v.width), h = (int)Math.Max(1, v.height);
            if (w > 1920) { h = h * 1920 / w; w = 1920; }
            Target = new RenderTexture(w, h, 0) { name = "FootageViewer" };
            Target.Create();
            v.targetTexture = Target;
            screen.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(Target));
            status.style.display = DisplayStyle.None;
            Loaded = true;
            v.Play();
            tick.Resume();
            Labels();
        }

        void StartPhoto(string url)
        {
            var req = photo = UnityWebRequestTexture.GetTexture(url, nonReadable: true);
            req.SendWebRequest().completed += _ =>
            {
                if (req != photo) { req.Dispose(); return; }   // closed meanwhile
                photo = null;
                if (req.result != UnityWebRequest.Result.Success) { Fail(req.error); req.Dispose(); return; }
                still = DownloadHandlerTexture.GetContent(req);
                req.Dispose();
                screen.style.backgroundImage = new StyleBackground(still);
                status.style.display = DisplayStyle.None;
                Loaded = true;
            };
        }

        void Fail(string message)
        {
            Debug.LogWarning($"[Footage] {clip?.MediaUrl}: {message}");
            Failed = true; Loaded = false;
            status.text = text["footage.error"]; status.style.display = DisplayStyle.Flex;
            browser.style.display = DisplayStyle.Flex;
            controls.style.display = DisplayStyle.None;
            MusicPlayer.Held = false;
        }

        void PlayPause()
        {
            if (Video == null || !Loaded) return;
            if (Video.isPlaying) Video.Pause(); else Video.Play();
            Labels();
        }

        void Mute()
        {
            if (sound == null) return;
            sound.mute = !sound.mute;
            Labels();
        }

        void Labels()
        {
            play.text = text[Video != null && Video.isPlaying ? "footage.pause" : "footage.play"];
            mute.text = text[sound != null && sound.mute ? "footage.unmute" : "footage.mute"];
        }

        void Tick()
        {
            if (Video == null || !Loaded || Video.length <= 0) return;
            progress.SetValueWithoutNotify((float)(Video.time / Video.length));
            Labels();
        }

        /// <summary>Takes the panel down: the video stops, its RenderTexture is freed, a photo's texture too, nothing is kept,
        /// and the music plays on.</summary>
        public void Close()
        {
            if (clip == null) return;
            clip = null; Loaded = false; Failed = false;
            tick.Pause();
            screen.style.backgroundImage = StyleKeyword.Null;
            if (Video != null) { Video.Stop(); Video.targetTexture = null; Video = null; }
            if (Target != null) { Target.Release(); Free(Target); Target = null; }
            if (still != null) { Free(still); still = null; }
            if (photo != null) { photo.Abort(); photo = null; }   // its completed callback disposes it
            if (host != null) { Free(host); host = null; }
            sound = null;
            MusicPlayer.Held = false;
            overlay.style.display = DisplayStyle.None;
            Closed?.Invoke();
        }

        static void Free(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }

        public void Dispose()
        {
            Close();
            overlay.RemoveFromHierarchy();
        }
    }
}
