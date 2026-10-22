#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>Strips the GLES3 (WebGL 2) variants of three hidden URP utility shaders that need shader target 4.5 (WebGL 2
    /// is ES 3.0) and that this build never draws with; scoped by graphics API, not build target, so a WebGPU WebGL build
    /// would keep them (fix round 1, M1). Without them the browser console no longer reports them as "not
    /// supported on this GPU" (playtest 1, item 12). Why each is unused on WebGL (URP 17.3 / Core RP 17.3 sources):
    /// - Hidden/CoreSRP/CoreCopy: Blitter.Initialize creates its material unconditionally, but the render-graph copy
    ///   pass that draws with it needs framebuffer-fetch emulation, which RenderGraphUtils reports unsupported on WebGL
    ///   (IsFramebufferFetchEmulationSupportedOnCurrentPlatform), so URP blits instead.
    /// - Hidden/Universal Render Pipeline/StencilDitherMaskSeed: UniversalRenderer always creates the
    ///   StencilCrossFadeRenderPass's materials, but the pass only runs for LOD cross-fade dithering type Stencil; the
    ///   URP assets use Blue Noise, and the map has no LOD groups.
    /// - Hidden/Universal/HDRDebugView: the Rendering Debugger's HDR view (DebugHandler); release builds have no
    ///   Rendering Debugger and the project's HDR output is off.</summary>
    public sealed class WebShaderStripping : IPreprocessShaders
    {
        public static readonly string[] Unused =
        {
            "Hidden/CoreSRP/CoreCopy",
            "Hidden/Universal Render Pipeline/StencilDitherMaskSeed",
            "Hidden/Universal/HDRDebugView",
        };

        public int callbackOrder => 0;

        /// <summary>Removes the GLES3 (WebGL 2) variants of the <see cref="Unused"/> shaders from <paramref name="data"/>, in
        /// place, and returns how many it removed. Only the API whose limit this works around: other graphics APIs (WebGPU,
        /// Metal, D3D) keep every variant, and every other shader is untouched. Generic over the variant type so tests can
        /// run it over plain platforms.</summary>
        public static int Filter<T>(string shaderName, IList<T> data, Func<T, ShaderCompilerPlatform> platform)
        {
            if (Array.IndexOf(Unused, shaderName) < 0) return 0;
            int removed = 0;
            for (int i = data.Count - 1; i >= 0; i--)
                if (platform(data[i]) == ShaderCompilerPlatform.GLES3x) { data.RemoveAt(i); removed++; }
            return removed;
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) =>
            Filter(shader.name, data, d => d.shaderCompilerPlatform);
    }
}
