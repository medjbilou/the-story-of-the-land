#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>One on/off switch per visual feature (shadows, tilt-shift, grade, haze, grid...), registered by the feature
    /// itself, so the visual profile can price each one and tests can isolate it. Names run in ordinal order; registering
    /// a name again replaces its switch (a rebuilt map re-registers).
    /// The one pattern (review I2): the map owns its switches through its teardown,
    /// <c>assets.OnTeardown(VisualToggles.Register("grid", on => ...))</c>. Register returns a removal token that removes
    /// the switch only while it is still the one registered, so an old map's teardown never removes a rebuilt map's switch.</summary>
    public static class VisualToggles
    {
        sealed class Entry { public readonly Action<bool> Set; public Entry(Action<bool> set) { Set = set; } }

        static readonly SortedDictionary<string, Entry> Switches = new SortedDictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>Registers (or replaces) the switch; returns its removal token (safe to call more than once).</summary>
        public static Action Register(string name, Action<bool> set)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("a toggle needs a name", nameof(name));
            var entry = new Entry(set ?? throw new ArgumentNullException(nameof(set)));
            Switches[name] = entry;
            return () => { if (Switches.TryGetValue(name, out var current) && ReferenceEquals(current, entry)) Switches.Remove(name); };
        }

        public static void Unregister(string name) => Switches.Remove(name);
        public static IReadOnlyList<string> Names => Switches.Keys.ToList();

        public static void Set(string name, bool on)
        {
            if (!Switches.TryGetValue(name, out var entry)) throw new KeyNotFoundException($"no visual toggle named '{name}' (known: {string.Join(", ", Switches.Keys)})");
            entry.Set(on);
        }

        public static void SetAll(bool on) { foreach (var entry in Switches.Values.ToList()) entry.Set(on); }
        public static void Clear() => Switches.Clear();
    }
}
