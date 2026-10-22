#nullable enable
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>The player's graphics quality (#40, owner decision of 18 Oct 2026), the same three choices on the web and the
    /// desktop. Auto is today's behaviour: the tier the device picked (the per-platform default quality level) at its own
    /// render scale, with the WebGL fallback (QualityFallback) stepping down when frames are slow. High is the PC tier at
    /// render scale 1.0, Low the Mobile tier at 0.8 (the fallback's lowest scale), both fixed: the fallback holds while one is
    /// set (QualityFallbackDriver.Feed). Presentation only: never the sim or the save. The tilt-shift stays its own option.</summary>
    public static class GraphicsQuality
    {
        public enum Choice { Auto, High, Low }

        public const string HighTier = "PC", LowTier = "Mobile";
        public const float HighRenderScale = 1f, LowRenderScale = 0.8f;

        public static Choice Current { get; private set; } = Choice.Auto;

        /// <summary>After a tier switch (CameraRig refits the new tier's shadow distance, which ShadowFit keeps per asset).</summary>
        public static event Action? Changed;

        static int bootLevel = -1;   // the level the device picked, which Auto goes back to

#if UNITY_EDITOR
        // In the editor the URP assets are project files: leaving Play mode puts every tier's own scale and the editor's
        // level back, so a choice made while playing is never saved into Assets/Settings.
        [UnityEditor.InitializeOnLoadMethod]
        static void PutTheTiersBackOnExit() => UnityEditor.EditorApplication.playModeStateChanged += state =>
        {
            if (state != UnityEditor.PlayModeStateChange.ExitingPlayMode || Current == Choice.Auto && RenderScale == null) return;
            RenderScale = null;
            Apply(Choice.Auto);
            int low = Array.IndexOf(QualitySettings.names, LowTier);
            if (low >= 0 && QualitySettings.GetRenderPipelineAssetAt(low) is UniversalRenderPipelineAsset mobile)
                mobile.renderScale = QualityTierValues.WebRenderScale;
        };
#endif

        /// <summary>The choice a stored value names; anything else (no file, a value the game does not know) is Auto.</summary>
        public static Choice Parse(string? stored) => stored == "high" ? Choice.High : stored == "low" ? Choice.Low : Choice.Auto;

        public static string Format(Choice c) => c == Choice.High ? "high" : c == Choice.Low ? "low" : "auto";

        /// <summary>The choice to apply at start: the stored one, except under an instrument that must see the tier's own
        /// look, which wins over the file as it does over the fallback: on the web ?vprofile, ?profile, ?shot or
        /// ?nofallback; on the desktop -vprofile, -profile or -shot (FrameProfile, PixelErrorProfile, VisualShots).</summary>
        public static Choice AtStart(string? stored, string? url, string[] args, bool webgl) =>
            (webgl ? !QualityFallback.Wanted(url, webgl: true) : args.Any(a => a == "-vprofile" || a == "-profile" || a == "-shot"))
                ? Choice.Auto : Parse(stored);

        /// <summary>Switches to <paramref name="c"/> at once: the quality level (and with it the URP asset) and the active
        /// asset's render scale. A tier missing from this build keeps the current one (a warning, never a failed start).</summary>
        public static void Apply(Choice c)
        {
            if (bootLevel < 0) bootLevel = QualitySettings.GetQualityLevel();
            int level = c == Choice.Auto ? bootLevel : Array.IndexOf(QualitySettings.names, c == Choice.High ? HighTier : LowTier);
            if (level < 0) { Debug.LogWarning($"Settings: no {c} tier in this build; the quality stays as it is"); return; }
            Current = c;
            if (QualitySettings.GetQualityLevel() != level) { QualitySettings.SetQualityLevel(level, true); ApplyFramePacing(); }   // a level brings its own vSyncCount
            var asset = UniversalRenderPipeline.asset;
            // Auto puts the tier's own scale back (QualityTiers.Apply: Mobile at WebRenderScale, PC at 1.0).
            if (asset != null)
                asset.renderScale = RenderScale ?? (c == Choice.High ? HighRenderScale : c == Choice.Low ? LowRenderScale
                    : QualitySettings.names[level] == LowTier ? QualityTierValues.WebRenderScale : 1f);
            TiltShift.FallbackOff = false;   // a fixed choice ends the fallback's last step; Auto starts it again from the top
            Changed?.Invoke();
        }

        // ---- F6: Settings > Graphics, one knob each. Presentation only (never the sim or the save), applied at once, kept
        // across map rebuilds (the map reads them when it builds), never written into a project asset: in the editor, leaving
        // Play mode puts the render scale back (PutTheTiersBackOnExit); the rest live in the scene or in shader globals.

        public enum ShadowChoice { Off, Hard, Soft }

        /// <summary>The sun's shadows: Soft (the default, as MapLighting sets them), Hard (no soft filtering, cheaper) or Off
        /// (no shadow pass: the largest saving, about 3-6 ms in the editor-render profile on the desktop tier). The "shadows"
        /// visual toggle, on, means this choice.</summary>
        public static ShadowChoice Shadows
        {
            get => shadows;
            set { shadows = value; var sun = MapLighting.FindSun(); if (sun != null) sun.shadows = SunShadows; Changed?.Invoke(); }
        }
        static ShadowChoice shadows = ShadowChoice.Soft;
        public static LightShadows SunShadows => shadows == ShadowChoice.Soft ? LightShadows.Soft : shadows == ShadowChoice.Hard ? LightShadows.Hard : LightShadows.None;

        /// <summary>The ground's rock and soil detail and the hill terraces (StoryOfTheLandDetail.hlsl): drawn on the desktop tier
        /// only (High; MapShaderGlobals.DetailAt), so off saves about 1-2 ms there and changes nothing on Low.</summary>
        public static bool Detail { get; set; } = true;

        /// <summary>The far haze over land and water (MapShaderGlobals); off shows the far map clear.</summary>
        public static bool Haze { get => haze; set { haze = value; MapShaderGlobals.ApplyHaze(); } }
        static bool haze = true;

        /// <summary>The render scale the player chose, 0.7-1.0 (clamped), or null for the quality choice's own (High 1.0, Low
        /// 0.8, Auto the tier's). Set, it holds through every Apply and stops the WebGL fallback stepping it.</summary>
        public static float? RenderScale
        {
            get => renderScale;
            set { renderScale = value.HasValue ? Mathf.Clamp(value.Value, MinRenderScale, 1f) : (float?)null; Apply(Current); }
        }
        static float? renderScale;
        public const float MinRenderScale = 0.7f;

        public enum AntialiasingChoice { Auto, Off, Fxaa, Smaa }

        /// <summary>The camera's anti-aliasing: Auto is the tier's (SMAA on the desktop tier, none on the web tier:
        /// QualityTierValues.PostAntialiasing); Off, FXAA (cheapest) or SMAA fix it. No MSAA: priced and dropped (F6).</summary>
        public static AntialiasingChoice Antialiasing { get => antialiasing; set { antialiasing = value; AntialiasingChanged?.Invoke(); } }

        /// <summary>After an anti-aliasing choice only (CameraRig re-applies it): not <see cref="Changed"/>, whose listeners refit
        /// the shadows and restart the fallback, which an anti-aliasing switch must not do.</summary>
        public static event Action? AntialiasingChanged;
        static AntialiasingChoice antialiasing = AntialiasingChoice.Auto;

        /// <summary>The anti-aliasing the camera draws with on the quality level named <paramref name="tier"/>.</summary>
        public static AntialiasingMode AntialiasingFor(string tier) => antialiasing switch
        {
            AntialiasingChoice.Off => AntialiasingMode.None,
            AntialiasingChoice.Fxaa => AntialiasingMode.FastApproximateAntialiasing,
            AntialiasingChoice.Smaa => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
            _ => QualityTierValues.PostAntialiasing(tier),
        };

        /// <summary>Reduced motion (accessibility): the water stands still (its clock pinned) and the trains stop where they
        /// are. The camera has no easing to calm: it already moves only as the player moves it.</summary>
        public static bool ReducedMotion
        {
            get => reducedMotion;
            set { reducedMotion = value; MapShaderGlobals.PinWaterTime(value ? ReducedMotionWaterTime : (float?)null); }
        }
        static bool reducedMotion;
        public const float ReducedMotionWaterTime = 100f;

        /// <summary>F6 frame pacing (Settings: "vsync" and "frame cap"). VSync on, the default, draws one frame per display
        /// refresh (60, 120 Hz...) with no tearing; off, the frame rate runs free up to <see cref="FrameCap"/> (0 = no cap: the
        /// highest the machine allows). WebGL ignores vsync (the browser paces every frame) but honours a cap.</summary>
        public static bool VSync { get; private set; } = true;
        public static int FrameCap { get; private set; }

        /// <summary>The caps Settings offers (0 = none).</summary>
        public static readonly int[] FrameCaps = { 0, 30, 60, 120, 144, 240 };

        /// <summary>QualitySettings.vSyncCount and Application.targetFrameRate for a choice: with vsync on the cap is moot
        /// (Unity ignores targetFrameRate under vsync on desktop), so it is left at -1, the platform default.</summary>
        public static (int vSyncCount, int targetFrameRate) FramePacing(bool vsync, int cap) =>
            vsync ? (1, -1) : (0, cap > 0 ? cap : -1);

        /// <summary>Sets and applies the frame pacing at once. Presentation only. In the editor the values are kept but not
        /// applied, so QualitySettings (a project file) is never changed by play mode or a test.</summary>
        public static void SetFramePacing(bool vsync, int cap)
        {
            VSync = vsync; FrameCap = Mathf.Max(0, cap);
            ApplyFramePacing();
        }

        static void ApplyFramePacing()
        {
            if (Application.isEditor) return;
            var (sync, target) = FramePacing(VSync, FrameCap);
            QualitySettings.vSyncCount = sync;
            Application.targetFrameRate = target;
        }

        /// <summary>The pacing a player starts with, before Settings applies the stored one: vsync, except under the frame-time
        /// instruments (-vprofile, -profile), which measure the frame's own cost.</summary>
        public static bool VSyncAtStart(string[] args) => !args.Any(a => a == "-vprofile" || a == "-profile");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void FramePacingAtBoot() => SetFramePacing(VSyncAtStart(Environment.GetCommandLineArgs()), 0);
    }
}
