#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>The tilt-shift renderer feature (on both URP renderers, PostFxAssets.Create). Before URP's own post-processing
    /// (so the grade and vignette apply on top): blur the camera colour horizontally into a 1/Downsample target, vertically
    /// into a second one, then composite back with the band weight as alpha. Game cameras only.</summary>
    public sealed class TiltShiftFeature : ScriptableRendererFeature
    {
        [SerializeField] Material? material;
        [SerializeField, Range(1, 4)] int downsample = 2;
        TiltShiftPass? pass;

        public Material? Material { get => material; set => material = value; }
        public int Downsample { get => downsample; set => downsample = Mathf.Clamp(value, 1, 4); }

        public override void Create() =>
            pass = new TiltShiftPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing, requiresIntermediateTexture = true };

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || pass == null || !TiltShift.Draws || !TiltShift.HasBand) return;
            // Scene-view and preview cameras get no pass, so they are not forced onto an intermediate texture (review m2).
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            pass.Setup(material, downsample);
            renderer.EnqueuePass(pass);
        }
    }

    sealed class TiltShiftPass : ScriptableRenderPass
    {
        Material material = null!;
        int downsample = 2;

        sealed class PassData { public TextureHandle source; public Material material = null!; public int pass; }

        public void Setup(Material m, int d) { material = m; downsample = d; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>();
            var resources = frameData.Get<UniversalResourceData>();
            if (camera.cameraType != CameraType.Game || resources.isActiveTargetBackBuffer) return;
            var colour = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(colour);
            desc.sizeMode = TextureSizeMode.Explicit;
            desc.width = Mathf.Max(1, desc.width / downsample); desc.height = Mathf.Max(1, desc.height / downsample);
            desc.msaaSamples = MSAASamples.None; desc.clearBuffer = false;
            desc.filterMode = FilterMode.Bilinear; desc.wrapMode = TextureWrapMode.Clamp;
            desc.name = "TiltShiftA"; var a = renderGraph.CreateTexture(desc);
            desc.name = "TiltShiftB"; var b = renderGraph.CreateTexture(desc);
            Blit(renderGraph, "TiltShift Horizontal", colour, a, 0, AccessFlags.Write);
            Blit(renderGraph, "TiltShift Vertical", a, b, 1, AccessFlags.Write);
            Blit(renderGraph, "TiltShift Composite", b, colour, 2, AccessFlags.ReadWrite);   // blends over the sharp frame
        }

        void Blit(RenderGraph graph, string name, TextureHandle source, TextureHandle target, int shaderPass, AccessFlags targetAccess)
        {
            using var builder = graph.AddRasterRenderPass<PassData>(name, out var data);
            data.source = source; data.material = material; data.pass = shaderPass;
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(target, 0, targetAccess);
            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, d.pass));
        }
    }
}
