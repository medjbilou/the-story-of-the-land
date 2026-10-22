#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace StoryOfTheLand.Game.Map.Collection
{
    /// <summary>F6 round 5 (the owner: "not show and hide and show and hide the buildings"): shows and hides a layer's Collection
    /// models over <see cref="Seconds"/> (the kits' dissolve, TownModelLayer.FadeSeconds) instead of in one frame. While a
    /// model fades, its materials are copies on CollectionFade.shader (URP Lit behind a screen-door clip) with the visible share
    /// in a property block; whole or hidden, it has its own materials back and Renderer.forceRenderingOff as before, so the
    /// map's toggles (Renderer.enabled) still hide it. A renderer seen first, and every change in edit mode (captures, tests),
    /// takes its state at once. The layer calls <see cref="Set"/> for its renderers, then <see cref="Step"/>, from its
    /// beginCameraRendering hook; Step advances once per frame, by game time (PopCheck's fixed step).</summary>
    public sealed class ModelFade
    {
        public const float Seconds = Kits.TownModelLayer.FadeSeconds;
        static readonly int FadeId = Shader.PropertyToID("_ScFade");
        static Shader? shader;
        static bool looked;
        // ponytail: the fade copies live as long as the player (a few dozen materials, one per Collection material faded).
        static readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();

        sealed class Entry { public float shown; public bool target; public Material[]? own; }

        readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
        readonly List<Renderer> moving = new List<Renderer>();
        MaterialPropertyBlock? block;
        int stepped = -1;

        /// <summary>Renderers fading now (a test aid).</summary>
        public int Fading => moving.Count;

        /// <summary>Forgets every renderer (the layer rebuilt its models).</summary>
        public void Clear() { entries.Clear(); moving.Clear(); }

        /// <summary>Asks <paramref name="r"/> to be drawn (<paramref name="show"/>) or hidden: over <see cref="Seconds"/> in
        /// play once it is known, at once otherwise.</summary>
        public void Set(Renderer r, bool show)
        {
            bool known = entries.TryGetValue(r, out var e);
            if (!known || !Application.isPlaying)
            {
                if (e == null) entries[r] = e = new Entry();
                e.target = show; e.shown = show ? 1f : 0f;
                Apply(r, e);
                return;
            }
            if (e!.target == show) return;
            e.target = show;
            if (!moving.Contains(r)) moving.Add(r);
        }

        /// <summary>Moves every fading renderer one frame on (once per frame, whichever camera calls).</summary>
        public void Step()
        {
            if (moving.Count == 0 || stepped == Time.frameCount) return;
            stepped = Time.frameCount;
            float step = Time.deltaTime / Seconds;
            for (int i = moving.Count - 1; i >= 0; i--)
            {
                var r = moving[i];
                if (r == null || !entries.TryGetValue(r, out var e)) { moving.RemoveAt(i); continue; }
                float to = e.target ? 1f : 0f;
                e.shown = Mathf.MoveTowards(e.shown, to, step);
                Apply(r, e);
                if (e.shown == to) moving.RemoveAt(i);
            }
        }

        void Apply(Renderer r, Entry e)
        {
            bool partial = e.shown > 0f && e.shown < 1f;
            r.forceRenderingOff = e.shown <= 0f;
            if (partial && e.own == null && FadeShader() != null)
            {
                e.own = r.sharedMaterials;
                r.sharedMaterials = Array.ConvertAll(e.own, Copy);
            }
            else if (!partial && e.own != null)
            {
                r.sharedMaterials = e.own; e.own = null;
                r.SetPropertyBlock(null);
            }
            if (e.own == null) return;
            block ??= new MaterialPropertyBlock();
            block.SetFloat(FadeId, e.shown);
            r.SetPropertyBlock(block);
        }

        static Material Copy(Material m)
        {
            if (m == null) return m!;
            if (!copies.TryGetValue(m, out var c) || c == null)
                copies[m] = c = new Material(m) { shader = shader!, name = m.name + " (fade)", hideFlags = HideFlags.DontSave };
            return c;
        }

        /// <summary>CollectionFade.shader (null if it is missing: the models then switch at the fade's end).</summary>
        static Shader? FadeShader()
        {
            if (!looked) { looked = true; shader = Resources.Load<Shader>("Map/CollectionFade"); if (shader != null && !shader.isSupported) shader = null; }
            return shader;
        }
    }
}
