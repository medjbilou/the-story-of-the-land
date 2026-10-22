using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>Diorama camera: fixed 50-degree pitch, 35-degree FOV, a two-band tilt-shift (TiltShiftFeature) driven from Apply.
    /// Yaw turns the view around the look-at point (0 = looking north); CameraController drives LookAt, Distance and Yaw.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public const float Pitch = 50f, Fov = 35f;
        public Vector3 LookAt; public float Distance = 120000f; public float Yaw;
        /// <summary>Share of the screen height (0..1) the UI covers at the top. The view is shifted so LookAt sits in the
        /// middle of the map area below it (an off-centre frustum: picking rays and WorldToScreenPoint follow it).</summary>
        public float TopInset;
        /// <summary>The largest inset the projection applies (TopInset is clamped to it); at this pitch and FOV the top of
        /// the view still looks down, so the sky never shows (MapSceneTests.TheCameraClearsToTheSeaColour).</summary>
        public const float MaxTopInset = 0.5f;

        /// <summary>Undo actions for state Init changes outside the rig (the URP asset's shadow distance...). MapScene.Build hands
        /// them to the map's MapRuntimeAssets right after Init, so destroying the map undoes them.</summary>
        public readonly List<Action> TeardownActions = new List<Action>();
        bool initialised;   // only a rig the map set up fits the shadow distance (bare rigs in tests must not touch the URP asset)

        /// <summary>The global post-processing volume Init creates. Its runtime VolumeProfile must be owned by the caller
        /// (MapScene registers it with MapRuntimeAssets).</summary>
        public Volume Volume { get; private set; }

        /// <summary>Whether Init attaches the runtime quality fallback (QualityFallback.Wanted: the WebGL tier, no instrument
        /// running). A seam: the integration test forces the web tier's answer in the editor.</summary>
        internal static Func<bool> FallbackWanted = QualityFallback.Wanted;

        public void Init()
        {
            // Undo actions an earlier Init left behind (Init called outside MapScene.Build) run now instead of piling up.
            for (int i = TeardownActions.Count - 1; i >= 0; i--) TeardownActions[i]();
            TeardownActions.Clear();
            var cam = GetComponent<Camera>(); cam.fieldOfView = Fov; cam.farClipPlane = 1_000_000f; cam.nearClipPlane = 50f;
            // Beyond the terrain the view shows open sea, not a grey void (playtest 1, item 9). At this pitch the top of the
            // view still looks down, so the sky never shows and a solid clear in the sea biome colour is the whole fix.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MapBuilder.BiomeColours["sea"];
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var vol = new GameObject("PostFX").AddComponent<Volume>(); vol.isGlobal = true;
            vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Volume = vol;
            MapGrade.Apply(vol.profile);
            var sun = MapLighting.FindSun();
            TeardownActions.Add(MapLighting.Capture(sun));   // first, so it runs last (review M1)
            MapLighting.Apply(sun);
            TeardownActions.Add(VisualToggles.Register("shadows", on => { var s = MapLighting.FindSun(); if (s != null) s.shadows = on ? GraphicsQuality.SunShadows : LightShadows.None; }));
            TeardownActions.Add(ShadowFit.Restore);
            TeardownActions.Add(TiltShift.Reset);
            TeardownActions.Add(VisualToggles.Register("tiltshift", on => TiltShift.Enabled = on));
            TeardownActions.Add(VisualToggles.Register("grade", on => MapGrade.SetActive(vol.profile, on)));
            if (TiltShift.OffRequested()) TiltShift.Enabled = false;
            // The runtime quality fallback (WebGL only); its undo removes the driver and restores the tier's render scale.
            TeardownActions.Add(QualityFallbackDriver.Attach(gameObject, FallbackWanted()));
            // A quality switch in Settings (#40) changes the URP asset: refit its shadow distance now, not at the next move.
            GraphicsQuality.Changed += Apply;
            TeardownActions.Add(() => GraphicsQuality.Changed -= Apply);
            // F6: the camera's anti-aliasing (GraphicsQuality.AntialiasingFor: Settings' choice, or the tier's), now and on
            // every quality or Settings switch.
            ApplyAntialiasing();
            GraphicsQuality.Changed += ApplyAntialiasing;
            TeardownActions.Add(() => GraphicsQuality.Changed -= ApplyAntialiasing);
            GraphicsQuality.AntialiasingChanged += ApplyAntialiasing;
            TeardownActions.Add(() => GraphicsQuality.AntialiasingChanged -= ApplyAntialiasing);
            // ... and whenever the quality level changes under the map by another way (the editor's captures and RenderProfile
            // switch QualitySettings directly): F6 round 3 found the desktop tier's SMAA left on the camera after such a switch,
            // which made every Low-tier measure about 0.8 ms slower than the tier really is.
            RenderPipelineManager.beginCameraRendering += FollowLevel;
            TeardownActions.Add(() => RenderPipelineManager.beginCameraRendering -= FollowLevel);
            initialised = true;
            Apply();
        }

        int antialiasingLevel = -1;

        void FollowLevel(ScriptableRenderContext _, Camera cam)
        {
            if (cam == null || cam.gameObject != gameObject || QualitySettings.GetQualityLevel() == antialiasingLevel) return;
            ApplyAntialiasing();
        }

        void ApplyAntialiasing()
        {
            antialiasingLevel = QualitySettings.GetQualityLevel();
            var data = GetComponent<Camera>().GetUniversalAdditionalCameraData();
            data.antialiasing = GraphicsQuality.AntialiasingFor(QualitySettings.names[QualitySettings.GetQualityLevel()]);
            data.antialiasingQuality = AntialiasingQuality.Medium;
        }

        /// <summary>F7 (the owner: "keep the game interactive ... not freezed or static"): how long the camera takes to glide
        /// to a place (EaseTo).</summary>
        public const float EaseSeconds = 0.6f;
        Vector3 easeFrom, easeTo;
        float easeT = 1f;   // 1 = no glide

        /// <summary>Whether the camera is gliding to a place (EaseTo).</summary>
        public bool Easing => easeT < 1f;

        /// <summary>F7: glides the look-at point to <paramref name="target"/> over <see cref="EaseSeconds"/> (smoothstep; the
        /// distance and yaw stay the player's), driven by CameraController.Step, which also stops it at the player's first
        /// camera input. Reduced motion jumps there at once; a measuring run (DiagLog.Automated) ignores it.</summary>
        public void EaseTo(Vector3 target)
        {
            if (Boot.DiagLog.Automated) return;
            if (GraphicsQuality.ReducedMotion) { LookAt = target; easeT = 1f; Apply(); return; }
            easeFrom = LookAt; easeTo = target; easeT = 0f;
        }

        /// <summary>F7: <see cref="EaseTo"/> only when <paramref name="target"/> is outside the middle of the view (inside the
        /// <paramref name="margin"/> share of the screen from each edge, below the top inset), so a card about the place the
        /// player is already looking at moves nothing. True when it glides.</summary>
        public bool EaseIntoView(Vector3 target, float margin = 0.2f)
        {
            var cam = GetComponent<Camera>();
            var v = cam.WorldToViewportPoint(target);
            bool inView = v.z > 0f && v.x > margin && v.x < 1f - margin && v.y > margin && v.y < 1f - Mathf.Max(margin, TopInset);
            if (inView) return false;
            EaseTo(target);
            return true;
        }

        /// <summary>One frame of the glide (CameraController.Step); <paramref name="moved"/> (the player's own input) ends it
        /// where it is.</summary>
        internal void StepEase(float dt, bool moved)
        {
            if (easeT >= 1f) return;
            if (moved) { easeT = 1f; return; }
            easeT = Mathf.Min(1f, easeT + dt / EaseSeconds);
            LookAt = Vector3.Lerp(easeFrom, easeTo, Mathf.SmoothStep(0f, 1f, easeT));
        }

        public void Apply()
        {
            transform.rotation = Quaternion.Euler(Pitch, Yaw, 0);
            transform.position = LookAt - transform.forward * Distance;
            TiltShift.Apply(TopInset, Distance);
            ApplyProjection();
            if (initialised) ShadowFit.Apply(GetComponent<Camera>());   // after the projection: the fit reads the corner rays
        }

        /// <summary>Sets the camera's projection for <see cref="TopInset"/> only (the transform is left alone). Apply calls it
        /// every frame, so a new window aspect is picked up too.</summary>
        public void ApplyProjection()
        {
            var cam = GetComponent<Camera>();
            if (cam == null) return;
            cam.ResetProjectionMatrix();   // follows the aspect and FOV again
            if (TopInset <= 0f) return;
            // NDC y = (m11 y + m12 z) / -z, so m12 = s moves every point down by s: the centre of the area below the bar
            // (NDC y from -1 to 1 - 2 TopInset) is at -TopInset.
            var p = cam.projectionMatrix; p[1, 2] = Mathf.Clamp(TopInset, 0f, MaxTopInset); cam.projectionMatrix = p;
        }
    }
}
