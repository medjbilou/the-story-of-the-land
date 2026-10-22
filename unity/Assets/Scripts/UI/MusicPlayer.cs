#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The game's music (StreamingAssets/music, AI-generated with Google Lyria: ASSETS.yaml, docs/credits.md): the main theme on the
    /// start screen, the chapter's era theme in play, the war theme in a war month (<see cref="Wars"/>). Loaded as the narration
    /// is (Ogg Vorbis on the desktop, the MP3 copy on the web: Narrator.Files), looped, crossfaded over <see cref="FadeSeconds"/>
    /// and ducked under the narration. GameRoot adds it in Play mode and players and says which theme it wants (Wanted).
    /// Presentation only.</summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        /// <summary>The music's own volume, 0 to 1 (Settings), under the master volume (AudioListener.volume).</summary>
        public static float Volume { get; set; } = DefaultVolume;
        public const float DefaultVolume = 0.6f;
        public const float FadeSeconds = 2f;
        /// <summary>The music's share of its volume while the narration speaks, reached over <see cref="DuckSeconds"/>.</summary>
        public const float Duck = 0.35f, DuckSeconds = 0.5f;
        /// <summary>True while the footage viewer plays a clip (FootageViewer): the music pauses, and plays on from where it
        /// stopped when the viewer closes.</summary>
        public static bool Held { get; set; }

        public const string Main = "main_theme", War = "war_theme";
        /// <summary>Each chapter's era theme (FamilyGame.Chapters).</summary>
        public static readonly IReadOnlyDictionary<string, string> Eras = new Dictionary<string, string>
        {
            ["1880_1946"] = "era_1880_1946", ["1947_1949"] = "era_1947_1966", ["1950_1966"] = "era_1947_1966",
            ["1967_1987"] = "era_1967_2000", ["1987_2000"] = "era_1967_2000",
            ["2000_2023"] = "era_2000_2026", ["2023_2026"] = "era_2000_2026",
        };

        /// <summary>The war months, first and last (inclusive): the 1947-1949 war (30 November 1947 to the last armistice, 20 July
        /// 1949), June 1967, the 1982 invasion of Lebanon (6 June, to Sabra and Shatila, 16-18 September), and the wars on Gaza
        /// (27 December 2008 - 18 January 2009, 14-21 November 2012, 8 July - 26 August 2014, 10-21 May 2021, and from
        /// 7 October 2023 on).</summary>
        public static readonly (DateTime From, DateTime To)[] Wars =
        {
            (new DateTime(1947, 12, 1), new DateTime(1949, 7, 1)), (new DateTime(1967, 6, 1), new DateTime(1967, 6, 1)),
            (new DateTime(1982, 6, 1), new DateTime(1982, 9, 1)), (new DateTime(2008, 12, 1), new DateTime(2009, 1, 1)),
            (new DateTime(2012, 11, 1), new DateTime(2012, 11, 1)), (new DateTime(2014, 7, 1), new DateTime(2014, 8, 1)),
            (new DateTime(2021, 5, 1), new DateTime(2021, 5, 1)), (new DateTime(2023, 10, 1), DateTime.MaxValue),
        };

        public static bool AtWar(DateTime date)
        {
            var month = new DateTime(date.Year, date.Month, 1);
            return Wars.Any(w => month >= w.From && month <= w.To);
        }

        /// <summary>The theme for a game of <paramref name="chapter"/> at <paramref name="date"/>.</summary>
        public static string Theme(string chapter, DateTime date) =>
            AtWar(date) ? War : Eras.TryGetValue(chapter, out var era) ? era : Eras[FamilyGame.Chapters[0].Id];

        /// <summary>Every track the player can ask for (file names without the extension).</summary>
        public static IEnumerable<string> Tracks => new[] { Main, War }.Concat(Eras.Values).Distinct();

        /// <summary>The file of <paramref name="track"/> on this platform: the MP3 copy on the web, as for the narration.</summary>
        public static string File(string track, bool web) => track + (web ? ".mp3" : ".ogg");

        /// <summary>The request for one track: downloaded whole on the web (the web player streams nothing), kept compressed in
        /// memory on the desktop (a decoded 3-minute track would take 60 MB).</summary>
        public static UnityWebRequest Request(string url, bool web)
        {
            var req = UnityWebRequestMultimedia.GetAudioClip(url, web ? AudioType.MPEG : AudioType.OGGVORBIS);
            var handler = (DownloadHandlerAudioClip)req.downloadHandler;
            handler.streamAudio = false;
            if (!web) handler.compressed = true;
            return req;
        }

        /// <summary>The track wanted now (null: none), asked every frame; and whether the narration is speaking.</summary>
        public Func<string?>? Wanted { get; set; }
        public Func<bool>? Narrating { get; set; }

        AudioSource? current, fading;
        string? playing, loading;
        float fade = 1f, duck = 1f;

        void Update()
        {
            var want = Wanted?.Invoke();
            if (want != playing && want != loading)
            {
                StopAllCoroutines(); loading = null;
                if (want == null) Switch(null, null);
                else StartCoroutine(Load(want));
            }
            duck = Mathf.MoveTowards(duck, Narrating?.Invoke() == true ? Duck : 1f, Time.unscaledDeltaTime * (1f - Duck) / DuckSeconds);
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / FadeSeconds);
            Hold(current); Hold(fading);
            float volume = Volume * duck;
            if (current != null) current.volume = volume * fade;
            if (fading != null)
            {
                fading.volume = volume * (1f - fade);
                if (fade >= 1f) { Release(fading); fading = null; }
            }
        }

        /// <summary>Pauses <paramref name="source"/> while <see cref="Held"/>, and lets it play on after.</summary>
        static void Hold(AudioSource? source)
        {
            if (source == null || source.isPlaying != Held) return;
            if (Held) source.Pause(); else source.UnPause();
        }

        /// <summary>Fades <paramref name="clip"/> in (null: silence) over what plays, which fades out.</summary>
        void Switch(AudioClip? clip, string? track)
        {
            if (fading != null) Release(fading);   // a third track: the oldest goes at once
            fading = current; current = null; fade = 0f; playing = track;
            if (clip == null) return;
            current = gameObject.AddComponent<AudioSource>();
            current.playOnAwake = false; current.loop = true; current.clip = clip; current.volume = 0f;
            current.Play();
            Debug.Log(FormattableString.Invariant($"[Music] playing {track}, {clip.length:F1} s"));
        }

        static void Release(AudioSource source)
        {
            var clip = source.clip;
            source.Stop();
            Destroy(source);
            if (clip != null) Destroy(clip);
        }

        /// <summary>Loads <paramref name="track"/>, then switches to it; a track that does not load is silence, with one warning
        /// (Narrator.Clip), and is not asked for again until another track has been wanted.</summary>
        IEnumerator Load(string track)
        {
            loading = track;
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            var file = File(track, web);
            var path = Application.streamingAssetsPath + "/music/" + file;
            AudioClip? clip = null;
            if (web || System.IO.File.Exists(path))
            {
                var url = web ? Boot.GameInputs.WithBuildId(path, Application.buildGUID) : new Uri(path).AbsoluteUri;
                using var req = Request(url, web);
                yield return req.SendWebRequest();
                clip = Narrator.Clip(req, file);
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                float until = Time.realtimeSinceStartup + Narrator.DecodeTimeout;
                while (clip != null && Narrator.Decoding(clip.loadState) && Time.realtimeSinceStartup < until) yield return null;
                if (clip != null && (clip.loadState != AudioDataLoadState.Loaded || !(clip.length > 0f)))
                {
                    Debug.LogWarning(FormattableString.Invariant($"[Music] {file}: no audio on this platform (load state {clip.loadState})"));
                    Destroy(clip); clip = null;
                }
            }
            else Debug.LogWarning($"[Music] {file}: no such file");
            loading = null;
            Switch(clip, track);
        }

        void OnDestroy()
        {
            if (current != null) Release(current);
            if (fading != null) Release(fading);
        }
    }
}
