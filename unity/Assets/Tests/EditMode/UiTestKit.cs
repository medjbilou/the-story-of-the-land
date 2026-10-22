using System;
using NUnit.Framework;
using StoryOfTheLand.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>Edit-mode UI harness (tech notes 1.6): a UIDocument on a fresh GameObject gives the tree a real runtime
/// panel, so events dispatch. Buttons are pressed with NavigationSubmitEvent, which fires Button.clicked; a synthetic
/// ClickEvent does not.</summary>
public sealed class UiTestKit : IDisposable
{
    public readonly GameObject Go;
    public readonly UIDocument Doc;
    public VisualElement Root => Doc.rootVisualElement;

    public UiTestKit(string uxmlPath = UiPaths.GameUxml)
    {
        var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel);
        var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        Assert.IsNotNull(ps, UiPaths.Panel + " (run UiAssets.Create)");
        Assert.IsNotNull(vta, uxmlPath);
        Go = new GameObject("UITest");
        Go.SetActive(false);
        Doc = Go.AddComponent<UIDocument>();
        Doc.panelSettings = ps;
        Doc.visualTreeAsset = vta;
        Go.SetActive(true);                                    // OnEnable -> RecreateUI
        Assert.IsNotNull(Root.panel, "the UIDocument tree has no panel");
    }

    public T Q<T>(string name) where T : VisualElement
    {
        var e = Root.Q<T>(name);
        Assert.IsNotNull(e, $"no {typeof(T).Name} named '{name}'");
        return e;
    }

    /// <summary>Presses a button the way keyboard/gamepad Submit does: Button.clicked fires synchronously.</summary>
    public static void Press(Button b)
    {
        Assert.IsNotNull(b.panel, $"button '{b.name}' is not on a panel");
        using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
    }

    public void Press(string buttonName) => Press(Q<Button>(buttonName));

    public void Dispose() => Object.DestroyImmediate(Go);
}
