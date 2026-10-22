#nullable enable
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StoryOfTheLand.Game
{
    /// <summary>Where the player's settings live (#40): presentation only, never the sim or the save, so the golden hashes do
    /// not move. On WebGL a fixed folder under the /idbfs IndexedDB mount, as the autosave's (SaveStore): persistentDataPath
    /// hashes the page's URL and itch.io serves every upload from a new URL, which would forget the settings. The template
    /// sets autoSyncPersistentDataPath, so closing the file persists it. One "key=value" line per setting; a line the game
    /// does not know is kept as it is.</summary>
    public class SettingsStore
    {
        public const string WebDirectory = "/idbfs/storyoftheland-settings";
        public const string FileName = "settings.txt";

        public string Directory { get; }
        public SettingsStore(string directory) { Directory = directory; }

        public static SettingsStore ForPlatform() => new SettingsStore(DirectoryFor(Application.platform, Application.persistentDataPath));

        /// <summary>The settings folder for a platform. On the web it is the fixed folder and ignores persistentDataPath.</summary>
        public static string DirectoryFor(RuntimePlatform platform, string persistentDataPath) =>
            platform == RuntimePlatform.WebGLPlayer ? WebDirectory : Path.Combine(persistentDataPath, "settings");

        public string FilePath => Path.Combine(Directory, FileName);

        /// <summary>The stored value of <paramref name="key"/>, or null: no file, no such line, or a file that cannot be read
        /// (a setting is never worth a failed start; the option keeps its default).</summary>
        public string? Read(string key)
        {
            foreach (var line in Lines())
                if (line.StartsWith(key + "=", System.StringComparison.Ordinal)) return line.Substring(key.Length + 1);
            return null;
        }

        /// <summary>Sets <paramref name="key"/>'s line, keeping every other line. Throws on a failed write (the caller logs it:
        /// the choice still holds for this session). Not written through a .tmp as the autosave is: a cut-off file only
        /// costs the options their defaults (Read fails soft), never a game.</summary>
        public void Write(string key, string value)
        {
            var lines = new List<string>();
            foreach (var line in Lines()) if (!line.StartsWith(key + "=", System.StringComparison.Ordinal)) lines.Add(line);
            lines.Add(key + "=" + value);
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllLines(FilePath, lines);
        }

        string[] Lines()
        {
            try { return File.Exists(FilePath) ? File.ReadAllLines(FilePath) : new string[0]; }
            catch (IOException) { return new string[0]; }
            catch (System.UnauthorizedAccessException) { return new string[0]; }
        }
    }
}
