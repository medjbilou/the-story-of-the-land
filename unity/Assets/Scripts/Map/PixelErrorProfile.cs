#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>Measures frame time with the terrain's heightmapPixelError at 1 (M2's value, which kept the old hex line
    /// overlay draped visibly; it now keeps the selection band clear of the ground) and at Unity's default 5, from the
    /// diorama camera over central Israel, then restores the M2 value.
    /// Logs "PROFILE pixelError=&lt;e&gt; frames=&lt;n&gt; avg_ms=&lt;a&gt; p95_ms=&lt;p&gt;" per setting and "PROFILE done".
    /// Asked for by "profile" in the WebGL page URL (a whole query parameter, UrlFlags.Has) or -profile on desktop.</summary>
    public static class PixelErrorProfile
    {
        public const int WarmupFrames = 60, MeasuredFrames = 300;

        public static bool Requested() =>
            Requested(Application.absoluteURL, Environment.GetCommandLineArgs(), Application.platform == RuntimePlatform.WebGLPlayer);

        public static bool Requested(string? url, string[] args, bool webgl) =>
            webgl ? Boot.UrlFlags.Has(url, "profile") : args.Contains("-profile");

        public static string Line(float pixelError, List<float> frameMs)
        {
            var (avg, p95) = FrameStats.Summary(frameMs);
            return string.Format(CultureInfo.InvariantCulture, "PROFILE pixelError={0:0} frames={1} avg_ms={2:0.00} p95_ms={3:0.00}",
                pixelError, frameMs.Count, avg, p95);
        }

        public static IEnumerator Run(Terrain terrain)
        {
            foreach (var pixelError in new[] { 1f, 5f })
            {
                terrain.heightmapPixelError = pixelError;
                for (int i = 0; i < WarmupFrames; i++) yield return null;
                var times = new List<float>(MeasuredFrames);
                for (int i = 0; i < MeasuredFrames; i++) { yield return null; times.Add(Time.unscaledDeltaTime * 1000f); }
                Debug.Log(Line(pixelError, times));
            }
            terrain.heightmapPixelError = MapBuilder.TerrainPixelError;
            Debug.Log("PROFILE done");
        }
    }
}
