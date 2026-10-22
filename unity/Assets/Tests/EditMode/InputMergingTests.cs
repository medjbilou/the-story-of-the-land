#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using StoryOfTheLand.Game.Boot;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Playtest 2, P2-4, root cause (reproduced headed: same-frame CDP clicks under ?navlog, raw Input System events
/// logged): when a move, a click and a second click land in one update, the Input System's redundant-event merging
/// (FastMouse.MergeForward; the skip rule protects only the event right after a button change) merges the first click's
/// release into the move that follows it. The release then reaches the UI at the second click's position, the button
/// cancels its click, and the toggle is lost while the second click goes through. InputConfig turns merging off.</summary>
public class InputMergingTests
{
    [Test]
    public void AReleaseInABurstKeepsItsOwnPosition()
    {
        var old = InputSystem.settings.disableRedundantEventsMerging;
        // By layout name, as a real mouse is created: the precompiled FastMouse, the device that merges events.
        var mouse = (Mouse)InputSystem.AddDevice("Mouse", "P24Mouse");
        Assert.AreEqual("FastMouse", mouse.GetType().Name, "a merging mouse, as browsers and desktops get");
        var seen = new List<(float left, Vector2 at)>();
        System.Action<InputEventPtr, InputDevice> listen = (e, d) =>
        {
            if (d == mouse && e.IsA<StateEvent>()) seen.Add((mouse.leftButton.ReadValueFromEvent(e), mouse.position.ReadValueFromEvent(e)));
        };
        InputSystem.onEvent += listen;   // the events that survive merging, as the actions and the UI then see them
        try
        {
            InputConfig.Apply();
            Vector2 start = new Vector2(100, 100), why = new Vector2(1908, 1151), map = new Vector2(1630, 317);
            void State(Vector2 at, bool down)
            {
                var s = new MouseState { position = at };
                InputSystem.QueueStateEvent(mouse, down ? s.WithButton(MouseButton.Left) : s);
            }
            State(start, false); InputSystem.Update();
            seen.Clear();
            // The browser's burst: move to Why, click, move to the map, click, all in one frame.
            State(why, false); State(why, true); State(why, false); State(map, false); State(map, true); State(map, false);
            InputSystem.Update();
            Assert.AreEqual(6, seen.Count, "every event survives: " + string.Join(", ", seen));
            Assert.AreEqual((0f, why), seen[2], "the first release is where it happened, on Why");
        }
        finally
        {
            InputSystem.onEvent -= listen;
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.disableRedundantEventsMerging = old;
        }
    }

    [Test]
    public void TheGameTurnsMergingOff()
    {
        var old = InputSystem.settings.disableRedundantEventsMerging;
        try
        {
            InputSystem.settings.disableRedundantEventsMerging = false;
            InputConfig.Apply();
            Assert.IsTrue(InputSystem.settings.disableRedundantEventsMerging);
        }
        finally { InputSystem.settings.disableRedundantEventsMerging = old; }
    }
}
