#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>The map's light (research notes §1 "Lighting", promo 03): a low, warm late-afternoon sun from the
    /// west-north-west, whose long shadows fall to the right of the default view, and a cool sky fill that keeps the shadow
    /// side readable. The sun is fixed in the world, so orbiting the camera moves the light across the screen.</summary>
    public static class MapLighting
    {
        public const float SunElevation = 30f;   // degrees above the horizon
        public const float SunAzimuth = 285f;    // degrees clockwise from north: west-north-west
        public const float SunIntensity = 1.6f;
        public static readonly Color SunColour = new Color(1.00f, 0.84f, 0.64f);
        public static readonly Color AmbientSky = new Color(0.52f, 0.60f, 0.72f);
        public static readonly Color AmbientEquator = new Color(0.48f, 0.46f, 0.42f);
        public static readonly Color AmbientGround = new Color(0.26f, 0.22f, 0.17f);

        /// <summary>Records the sun's transform and light settings and the active scene's ambient, and returns the action that
        /// puts them back (review M1: CameraRig.Init registers it as its first teardown action, so destroying the map leaves
        /// the scene's lighting as it was and saving an open Map.unity does not bake the runtime values). The ambient is put
        /// back only while the same scene is still active, so a map destroyed by loading another scene never writes into it.</summary>
        public static Action Capture(Light? sun)
        {
            var scene = SceneManager.GetActiveScene();
            var mode = RenderSettings.ambientMode; var sky = RenderSettings.ambientSkyColor;
            var equator = RenderSettings.ambientEquatorColor; var ground = RenderSettings.ambientGroundColor;
            var probe = RenderSettings.ambientProbe; var renderSun = RenderSettings.sun;
            bool hadSun = sun != null;
            Quaternion rot = default; Color colour = default; float intensity = 0f, strength = 0f; LightShadows shadows = default;
            if (sun != null) { rot = sun.transform.rotation; colour = sun.color; intensity = sun.intensity; strength = sun.shadowStrength; shadows = sun.shadows; }
            return () =>
            {
                if (hadSun && sun != null)
                {
                    sun.transform.rotation = rot; sun.color = colour; sun.intensity = intensity;
                    sun.shadowStrength = strength; sun.shadows = shadows;
                }
                if (SceneManager.GetActiveScene() != scene) return;
                RenderSettings.ambientMode = mode; RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientProbe = probe; RenderSettings.sun = renderSun;
            };
        }

        /// <summary>The scene's first directional light, or null.</summary>
        public static Light? FindSun()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.InstanceID))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        /// <summary>Points, colours and enables shadows on <paramref name="sun"/> (when there is one) and sets the trilight
        /// ambient. Unity converts the three (sRGB) colours to the linear SH probe the shaders read, at once, in the editor
        /// and in the WebGL player; a probe assigned in Trilight mode is replaced by that conversion (Task 4, review M4), so
        /// the colours are the only input.</summary>
        public static void Apply(Light? sun)
        {
            if (sun != null)
            {
                // A light shines along its forward axis: towards the azimuth opposite the sun, tilted down by the elevation.
                sun.transform.rotation = Quaternion.Euler(SunElevation, SunAzimuth - 180f, 0f);
                sun.color = SunColour; sun.intensity = SunIntensity;
                sun.shadows = GraphicsQuality.SunShadows; sun.shadowStrength = 0.85f;   // Soft unless Settings chose otherwise (F6)
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky; RenderSettings.ambientEquatorColor = AmbientEquator; RenderSettings.ambientGroundColor = AmbientGround;
        }
    }
}
