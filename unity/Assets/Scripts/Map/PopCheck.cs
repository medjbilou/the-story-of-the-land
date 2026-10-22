#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>F6 round 4 (the owner: "not show and hide and show and hide the buildings"): a desktop player run that moves the
    /// camera along a fixed path and writes every frame to a folder, so pop-in can be seen and measured.
    /// "-popcheck &lt;folder&gt;": after GAME OK it waits <see cref="SettleSeconds"/> (the Collection's stream), then, at a fixed
    /// 1/30 s step (Time.captureDeltaTime), zooms over Jaffa and Lydda from 60 km to 25 km and back (through the town swap at
    /// 40-45 km and the trees' level-of-detail distances), then pans 20 km east at 60 km, the HUD hidden. Each frame is written as
    /// frame-NNNN.jpg at half size. The player quits when done. The log has "POPCHECK frames=N dir=…".</summary>
    public static class PopCheck
    {
        public const float SettleSeconds = 3f, Step = 1f / 30f;
        public const int ZoomFrames = 120, PanFrames = 60;

        public static string? Requested() => Requested(Environment.GetCommandLineArgs());

        public static string? Requested(string[] args)
        {
            int i = Array.IndexOf(args, "-popcheck");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        /// <summary>The camera's distance (m) at frame <paramref name="f"/> of the zoom in and out (smooth at both ends).</summary>
        public static float ZoomAt(int f)
        {
            float t = f < ZoomFrames / 2 ? f / (ZoomFrames / 2f) : 1f - (f - ZoomFrames / 2) / (ZoomFrames / 2f);
            return Mathf.Lerp(60000f, 25000f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
        }

        public static IEnumerator Run(CameraRig rig, GeoFrame frame, string dir, Action<string> log)
        {
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "frame-*.jpg")) File.Delete(old);
            float until = Time.realtimeSinceStartup + SettleSeconds;
            while (Time.realtimeSinceStartup < until) yield return null;
            // The map only: the HUD and the opening card would cover a third of each frame.
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None))
                if (doc.rootVisualElement != null) doc.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None;
            var start = frame.ToWorld(34.82, 32.02, 0);
            rig.LookAt = start; rig.Yaw = 10f; rig.Distance = ZoomAt(0); rig.Apply();
            Time.captureDeltaTime = Step;
            int n = 0;
            try
            {
                for (int f = 0; f < ZoomFrames + PanFrames; f++)
                {
                    if (f < ZoomFrames) rig.Distance = ZoomAt(f);
                    else { rig.Distance = ZoomAt(ZoomFrames); rig.LookAt = start + Vector3.right * (20000f * (f - ZoomFrames) / PanFrames); }
                    rig.Apply();
                    yield return new WaitForEndOfFrame();
                    var shot = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        var half = new RenderTexture(shot.width / 2, shot.height / 2, 0);
                        var prev = RenderTexture.active;
                        Graphics.Blit(shot, half);   // leaves half active
                        var small = new Texture2D(half.width, half.height, TextureFormat.RGB24, false);
                        RenderTexture.active = half;
                        small.ReadPixels(new Rect(0, 0, half.width, half.height), 0, 0); small.Apply();
                        RenderTexture.active = prev;   // before the release: releasing the active texture warns
                        File.WriteAllBytes(Path.Combine(dir, string.Format(CultureInfo.InvariantCulture, "frame-{0:0000}.jpg", f)), small.EncodeToJPG(88));
                        UnityEngine.Object.Destroy(small); half.Release(); UnityEngine.Object.Destroy(half);
                    }
                    finally { UnityEngine.Object.Destroy(shot); }
                    n++;
                }
            }
            finally { Time.captureDeltaTime = 0f; }
            log($"POPCHECK frames={n} dir={dir}");
            Application.Quit(0);
        }
    }
}
