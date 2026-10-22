#nullable enable
using System;
using System.Linq;
using UnityEngine;

namespace StoryOfTheLand.Game.Boot
{
    /// <summary>Diagnostic console lines, off unless asked for: "?bootlog" (WebGL page URL) or -bootlog (desktop) logs each
    /// boot phase with its time since the player started and since the previous phase ("BOOT phase=… at=… ms dt=… ms");
    /// "?navlog" / -navlog logs every UI pointer and submit event, every screen toggle and every map click with the frame
    /// it happened in ("NAV f=… …"). Playtest 2, P2-1 and P2-4.</summary>
    public static class DiagLog
    {
        public static bool Boot { get; set; }
        public static bool Nav { get; set; }
        /// <summary>F6 round 5: a desktop measuring run (-vprofile, -popcheck or -bootlog): the player keeps running when its
        /// window loses focus (runInBackground; it is off for play), and the gameplay keys and the camera input are ignored,
        /// so someone using the machine meanwhile cannot pause, steer or end a turn in it (an Enter ended a turn mid-profile).</summary>
        public static bool Automated { get; set; }

        public static bool AutomatedRequested(string[] args) => args.Contains("-vprofile") || args.Contains("-popcheck") || args.Contains("-bootlog");

        /// <summary>Whether <paramref name="flag"/> is asked for: a whole query parameter on WebGL (UrlFlags.Has), else a
        /// command-line flag "-<paramref name="flag"/>".</summary>
        public static bool Requested(string flag, string? url, string[] args, bool webgl) =>
            webgl ? UrlFlags.Has(url, flag) : args.Contains("-" + flag);

        /// <summary>Reads both flags from this player's URL or command line.</summary>
        public static void Init()
        {
            var webgl = Application.platform == RuntimePlatform.WebGLPlayer;
            var url = Application.absoluteURL; var args = Environment.GetCommandLineArgs();
            Boot = Requested("bootlog", url, args, webgl);
            Nav = Requested("navlog", url, args, webgl);
            Automated = !webgl && AutomatedRequested(args);
            if (Automated) Application.runInBackground = true;
        }

        static double last = -1;

        public static void BootPhase(string phase)
        {
            if (!Boot) return;
            var now = Time.realtimeSinceStartupAsDouble;
            Debug.Log(FormattableString.Invariant($"BOOT phase={phase} at={now * 1000:0} ms dt={(last < 0 ? now : now - last) * 1000:0} ms"));
            last = now;
        }

        public static void NavLine(string what)
        {
            if (!Nav) return;
            Debug.Log(FormattableString.Invariant($"NAV f={Time.frameCount} t={Time.realtimeSinceStartupAsDouble * 1000:0} {what}"));
        }
    }
}
