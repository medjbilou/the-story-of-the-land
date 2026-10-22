#nullable enable
namespace StoryOfTheLand.Game.Map
{
    /// <summary>The quality-tier decisions of visual v1 Task 10 (evidence: docs/visual-v1.md "Tier decisions"). Change a value
    /// only with new measurements recorded there; QualityTiers.Apply writes them to the URP assets.</summary>
    public static class QualityTierValues
    {
        /// <summary>1.0, not 0.8: at 1.0 every budget held (headless p95 5 / 11 ms, headed 5 / 11 ms, headed DPR 2 5 / 19 ms
        /// at gameplay03 / diorama02 against 16.7, 20 and 33 ms).</summary>
        public const float WebRenderScale = 1f;
        /// <summary>2048 kept although the diorama02 shadow cost (2.96 ms, spread 0.32) is over 2.5 ms plus the spread: a 1024
        /// build measured 2.84 ms (headless), 2.94 ms (headed) and 5.03 ms (DPR 2) against 2.96 / 2.90 / 4.99 at 2048, so the
        /// cost is the caster pass's geometry, not the map's resolution, and halving it would only blur the relief.</summary>
        public const int WebShadowMap = 2048, WebTiltShiftDownsample = 2;
        public const int DesktopShadowMap = 2048, DesktopTiltShiftDownsample = 2;
        /// <summary>The pipeline profile's bloom (the URP template's 0.25 above 1), off on both tiers. Measured with a
        /// temporary "bloom" toggle (commit 8d45a93, removed once bloom shipped off): rendered on both tiers at both promo
        /// poses it changed at most 2/255 of any pixel (mean 0.25/255 at diorama02, warmth +0.0003), for 0.37 ms (headless),
        /// 0.18 ms (headed) and 0.17 / 0.13 ms (macOS) at diorama02 (docs/visual-v1.md "Tier decisions").</summary>
        public const bool Bloom = false;
        /// <summary>F6: no MSAA on either tier. 4x MSAA on the desktop tier was priced and dropped: in the editor-render profile
        /// it made diorama02 32% slower (24.2 -> 31.9 ms p50; the overview's terrain is all small triangles, whose edges MSAA
        /// shades per sample) and the close-ups 20-23% slower (docs/visual-v1.md "F6"). The desktop tier smooths its edges
        /// with SMAA after the frame instead (<see cref="PostAntialiasing"/>), at a cost that does not grow with the geometry.</summary>
        public const int WebMsaa = 1, DesktopMsaa = 1;
        /// <summary>F6 (owner: "good texture and graphics ... like big games"): the camera's post-process anti-aliasing per tier:
        /// SMAA on the desktop tier (PC), so the kits' walls, the palms' fronds and the hex lines lose their stair steps; none on
        /// the web tier (WebGL's fill rate, the fallback's DPR-2 budget). CameraRig applies it at Init and on every Settings
        /// quality switch.</summary>
        public static UnityEngine.Rendering.Universal.AntialiasingMode PostAntialiasing(string tier) =>
            tier == GraphicsQuality.HighTier ? UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : UnityEngine.Rendering.Universal.AntialiasingMode.None;
        /// <summary>F6: the desktop tier's soft shadows filter at Medium, not High: the map's shadows are kilometres wide, so the
        /// wider High kernel changes no visible edge, while the four soft cascades cost the most of any feature on the desktop
        /// tier (the editor-render profile, docs/visual-v1.md "F6"). 2 = UnityEngine.Rendering.Universal.SoftShadowQuality.Medium.</summary>
        public const int DesktopSoftShadowQuality = 2;
        /// <summary>F6: the desktop tier's shadow cascades, 2 (was 4) with the near one over the nearest quarter of the shadow
        /// distance: the caster pass redraws the map's geometry once per cascade, which made the shadows the largest cost on
        /// the desktop tier (docs/visual-v1.md "F6"); at the map's kilometre scale two 2048 maps still keep the near relief
        /// sharp.</summary>
        public const int DesktopCascades = 2;
        public const float DesktopCascade2Split = 0.25f;
    }
}
