using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PlayerInteractionSpecsTests
{
    private const float SnapEpsilon = 0.01f;

    private readonly System.Collections.Generic.List<GameObject> _cleanup = new System.Collections.Generic.List<GameObject>();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var go in _cleanup)
        {
            if (go != null) Object.Destroy(go);
        }
        _cleanup.Clear();
        yield return null;
    }

    private GameObject Track(GameObject go)
    {
        if (go != null) _cleanup.Add(go);
        return go;
    }

    [UnityTest]
    public IEnumerator INT1_FocusShowsHighlight_WhenRayHitsInteractable()
    {
        var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -0.1f, 0);
        ground.transform.localScale = new Vector3(20, 0.2f, 20);

        var target = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        target.name = "Target";
        target.transform.position = new Vector3(0, 1.6f, 3f);
        target.transform.localScale = Vector3.one * 0.5f;
        var interactable = target.AddComponent<Interactable>();
        interactable.useFallbackHighlight = true;
        interactable.debugFocus = false;

        var player = Track(new GameObject("Player"));
        player.transform.position = new Vector3(0, 1.6f, 0);

        var camGo = Track(new GameObject("TestCamera"));
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = Vector3.zero;
        camGo.transform.localRotation = Quaternion.identity;
        camGo.AddComponent<Camera>();

        var pi = player.AddComponent<PlayerInteraction>();
        pi.debugRay = false;
        pi.debugInteract = false;
        pi.interactionDistance = 10f;

        yield return null;
        yield return null;

        bool outlineOn = interactable.outlineScript != null && interactable.outlineScript.enabled;

        bool fallbackOn = false;
        var rend = target.GetComponent<Renderer>();
        if (rend != null)
        {
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            var c = mpb.GetColor("_Color");
            if (c.a > 0.001f || c.r > 0.001f || c.g > 0.001f || c.b > 0.001f)
            {
                fallbackOn = true;
            }
            else if (rend.material != null)
            {
                var mc = rend.material.color;
                var dv = (Vector4)mc - (Vector4)interactable.fallbackColor;
                if (dv.sqrMagnitude < 0.05f) fallbackOn = true;
            }
        }

        Assert.That(outlineOn || fallbackOn, "Focus should show outline or fallback highlight");
    }

    [UnityTest]
    public IEnumerator INT2_OutlineMaterialsDoNotAccumulate_WhenFocusToggles()
    {
        var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -0.1f, 0);
        ground.transform.localScale = new Vector3(20, 0.2f, 20);

        var target = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        target.name = "Target";
        target.transform.position = new Vector3(0, 1.6f, 3f);
        target.transform.localScale = Vector3.one * 0.5f;
        var interactable = target.AddComponent<Interactable>();
        interactable.useFallbackHighlight = false;
        interactable.debugFocus = false;

        var player = Track(new GameObject("Player"));
        player.transform.position = new Vector3(0, 1.6f, 0);

        var camGo = Track(new GameObject("TestCamera"));
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = Vector3.zero;
        camGo.transform.localRotation = Quaternion.identity;
        camGo.AddComponent<Camera>();

        var pi = player.AddComponent<PlayerInteraction>();
        pi.debugRay = false;
        pi.debugInteract = false;
        pi.interactionDistance = 0f; // keep unfocused for baseline capture

        yield return null;
        yield return null;

        var rend = target.GetComponent<Renderer>();
        Assert.That(rend, Is.Not.Null);

        int baselineCount = rend.materials.Length;

        for (int i = 0; i < 3; i++)
        {
            // Focus on target
            pi.interactionDistance = 10f;
            camGo.transform.localRotation = Quaternion.identity;
            yield return null;
            yield return null;

            int focusedCount = rend.materials.Length;
            Assert.That(focusedCount, Is.EqualTo(baselineCount + 2), $"Focus should add exactly 2 outline materials (cycle {i})");
            Assert.That(interactable.outlineScript, Is.Not.Null, "Outline script should exist after focus");
            Assert.That(interactable.outlineScript.enabled, Is.True, "Outline script should be enabled while focused");

            // Lose focus (look away)
            camGo.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            yield return null;
            yield return null;

            int unfocusedCount = rend.materials.Length;
            Assert.That(unfocusedCount, Is.EqualTo(baselineCount), $"LoseFocus should remove outline materials (cycle {i})");
            Assert.That(interactable.outlineScript.enabled, Is.False, "Outline script should be disabled when not focused");
        }
    }

    [UnityTest]
    public IEnumerator PL1_PlayerFallsToGround_WithCharacterControllerGravity()
    {
        var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -0.1f, 0);
        ground.transform.localScale = new Vector3(50, 0.2f, 50);

        var player = Track(new GameObject("Player"));
        player.transform.position = new Vector3(0, 5f, 0);
        player.AddComponent<CharacterController>();
        player.AddComponent<PlayerMovement>();

        float startY = player.transform.position.y;
        yield return new WaitForSeconds(0.25f);
        float midY = player.transform.position.y;

        Assert.That(midY, Is.LessThan(startY - SnapEpsilon), "Player should fall due to gravity");
    }
}
