#nullable enable
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>F5 step 8: a family chapter opens with its narrated intro, pre-rendered offline (narration/README.md) into
    /// StreamingAssets/narration as Ogg Vorbis, so no TTS runs in the game. Arabic prefers the Habibi-TTS MSA render
    /// (<c>.ar.habibi.ogg</c>, 0% word errors against Chatterbox's 13% in the README), then the Chatterbox one; English is
    /// Chatterbox. A chapter with no file plays nothing: narration is optional. GameRoot adds it on the first family game in
    /// Play mode and players, never in EditMode (no player loop runs a load there). Presentation only.</summary>
    public sealed class Narrator : MonoBehaviour
    {
        /// <summary>Silenced by N or Settings' Narration switch (SettingsPresenter.FlipNarration, which keeps the choice).
        /// Static, as TiltShift.Enabled: one choice for every game in the session. A muted intro still plays, so unmuting
        /// mid-way picks it up where it is.</summary>
        public static bool Muted { get; set; }
        /// <summary>The narration's own volume, 0 to 1 (Settings, F6), under the master volume (AudioListener.volume).</summary>
        public static float Volume { get; set; } = 1f;

        AudioSource? source;

        /// <summary>Whether an intro is being heard now (the music ducks under it: MusicPlayer).</summary>
        public bool Speaking => source != null && source.isPlaying && !Muted;

        /// <summary>The narration script's id for <paramref name="chapter"/>: narration/scripts/intro_1947 is the 1947_1949
        /// chapter's (its text is about 1947); the others are named by their chapter.</summary>
        public static string ScriptId(string chapter) => chapter == "1947_1949" ? "1947" : chapter;

        /// <summary>The files to try for <paramref name="chapter"/> in <paramref name="language"/>, best first. The web
        /// player takes the MP3 copy of each (the same render, made with ffmpeg): Unity's web player loads no Ogg Vorbis
        /// ("Streaming of 'ogg' on this platform is not supported" even with streaming off; UI audit of the web build).</summary>
        public static string[] Files(string chapter, string language, bool web = false)
        {
            var id = "intro_" + ScriptId(chapter);
            var ext = web ? ".mp3" : ".ogg";
            return language == UiLanguage.Arabic ? new[] { id + ".ar.habibi" + ext, id + ".ar" + ext } : new[] { id + ".en" + ext };
        }

        /// <summary>Plays <paramref name="chapter"/>'s intro in <paramref name="language"/> from the start, stopping the one
        /// playing.</summary>
        public void Play(string chapter, string language)
        {
            StopAllCoroutines();
            if (source != null) source.Stop();
            StartCoroutine(Load(chapter, language));
        }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        void Update() { if (source != null) { source.mute = Muted; source.volume = Volume; } }

        /// <summary>The request for one narration file: an MP3 on the web, downloaded whole for the browser to decode (the
        /// web player streams nothing), an Ogg Vorbis file on the desktop with the handler's own setting.</summary>
        public static UnityWebRequest Request(string url, bool web)
        {
            var req = UnityWebRequestMultimedia.GetAudioClip(url, web ? AudioType.MPEG : AudioType.OGGVORBIS);
            if (web) ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
            return req;
        }

        /// <summary>How long a downloaded clip may take to decode before the intro is given up (seconds).</summary>
        public const float DecodeTimeout = 15f;

        /// <summary>True while a clip's data is still on its way (the web player decodes after the download).</summary>
        public static bool Decoding(AudioDataLoadState state) => state == AudioDataLoadState.Loading || state == AudioDataLoadState.Unloaded;

        /// <summary>The finished request's clip, or null after one clear warning: a failed download, or audio this platform
        /// could not decode. Narration is optional, so nothing throws.</summary>
        public static AudioClip? Clip(UnityWebRequest req, string file)
        {
            if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning($"[Narrator] {file}: {req.error}"); return null; }
            AudioClip? clip = null;
            try { clip = DownloadHandlerAudioClip.GetContent(req); }
            catch (Exception e) { Debug.LogWarning($"[Narrator] {file}: no audio on this platform ({e.GetType().Name}: {e.Message})"); return null; }
            if (clip == null) Debug.LogWarning($"[Narrator] {file}: no audio on this platform (the clip could not be decoded)");
            return clip;
        }

        IEnumerator Load(string chapter, string language)
        {
            // The web player cannot look for a file: it asks for each in turn and takes the first that comes.
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            foreach (var file in Files(chapter, language, web))
            {
                var path = Application.streamingAssetsPath + "/narration/" + file;
                if (!web && !File.Exists(path)) continue;
                var url = web ? Boot.GameInputs.WithBuildId(path, Application.buildGUID) : new Uri(path).AbsoluteUri;
                using var req = Request(url, web);
                yield return req.SendWebRequest();
                var clip = Clip(req, file);
                if (clip == null) continue;
                // The web player decodes in the browser, after the download: the clip is 0 s long until its data has loaded.
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                float until = Time.realtimeSinceStartup + DecodeTimeout;
                while (Decoding(clip.loadState) && Time.realtimeSinceStartup < until) yield return null;
                if (clip.loadState != AudioDataLoadState.Loaded || !(clip.length > 0f))
                {
                    Debug.LogWarning(FormattableString.Invariant($"[Narrator] {file}: no audio on this platform (load state {clip.loadState}, {clip.length:F1} s)"));
                    continue;
                }
                if (source == null) yield break;
                source.clip = clip;
                source.mute = Muted; source.volume = Volume;
                source.Play();
                Debug.Log(FormattableString.Invariant($"[Narrator] playing intro_{chapter} {language}: {file}, {clip.length:F1} s{(Muted ? " (muted)" : "")}"));
                yield break;
            }
            Debug.Log($"[Narrator] no intro for {chapter} in {language}");
        }
    }
}
