#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

/// <summary>Playtest 1, item 12 (fix round 1, M1): the three hidden URP utility shaders that GLES3 (WebGL 2) cannot run,
/// and that this build never draws with, lose their GLES3 variants only; other graphics APIs (WebGPU, Metal) and every
/// other shader keep theirs.</summary>
public class WebShaderStrippingTests
{
    // Editor code lives in Assembly-CSharp-Editor, which a test assembly cannot reference (as in PortraitImportTests).
    static Type Stripper
    {
        get
        {
            var t = Type.GetType("StoryOfTheLand.Game.EditorTools.WebShaderStripping, Assembly-CSharp-Editor");
            Assert.IsNotNull(t, "WebShaderStripping exists");
            return t!;
        }
    }

    /// <summary>Runs the stripper's own filter (the method OnProcessShader calls) over variants given by platform.</summary>
    static List<ShaderCompilerPlatform> Filter(string shader, params ShaderCompilerPlatform[] variants)
    {
        var data = new List<ShaderCompilerPlatform>(variants);
        var filter = Stripper.GetMethod("Filter", BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeof(ShaderCompilerPlatform));
        Func<ShaderCompilerPlatform, ShaderCompilerPlatform> platform = p => p;
        filter.Invoke(null, new object[] { shader, data, platform });
        return data;
    }

    [Test]
    public void ItIsAShaderPreprocessor() => Assert.IsTrue(typeof(IPreprocessShaders).IsAssignableFrom(Stripper));

    [TestCase("Hidden/CoreSRP/CoreCopy")]
    [TestCase("Hidden/Universal Render Pipeline/StencilDitherMaskSeed")]
    [TestCase("Hidden/Universal/HDRDebugView")]
    public void OnlyTheGles3VariantsOfTheUnsupportedShadersAreStripped(string name)
    {
        Assert.IsNotNull(Shader.Find(name), name + " exists (URP renamed it?)");
        var left = Filter(name, ShaderCompilerPlatform.GLES3x, ShaderCompilerPlatform.WebGPU, ShaderCompilerPlatform.GLES3x, ShaderCompilerPlatform.Metal);
        CollectionAssert.AreEqual(new[] { ShaderCompilerPlatform.WebGPU, ShaderCompilerPlatform.Metal }, left);
    }

    [TestCase("Hidden/Universal Render Pipeline/Blit")]
    [TestCase("Hidden/Universal/CoreBlit")]
    [TestCase("Universal Render Pipeline/Unlit")]
    [TestCase("Universal Render Pipeline/Terrain/Lit")]
    public void EveryOtherShaderIsKept(string name)
    {
        var all = new[] { ShaderCompilerPlatform.GLES3x, ShaderCompilerPlatform.WebGPU, ShaderCompilerPlatform.Metal };
        CollectionAssert.AreEqual(all, Filter(name, all));
    }
}
