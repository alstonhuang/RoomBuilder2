using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MouseLookSpecsTests
{
    private const float AngleEpsilonDeg = 0.25f;

    [UnityTest]
    public IEnumerator ML1_SmallSensitivity_AppliesDegreesPerFrame()
    {
        var player = new GameObject("Player_ML1");
        var camera = new GameObject("Camera_ML1");
        camera.transform.SetParent(player.transform, worldPositionStays: false);

        var look = camera.AddComponent<MouseLook>();
        look.playerBody = player.transform;
        look.mouseSensitivity = 5f; // <= 10 => per-frame path (no deltaTime scaling)
        look.useOverrideInput = true;
        look.overrideLookInput = new Vector2(1f, 0f);

        float beforeYaw = player.transform.eulerAngles.y;
        yield return null; // allow Update() to run once
        float afterYaw = player.transform.eulerAngles.y;

        float deltaYaw = Mathf.Abs(Mathf.DeltaAngle(beforeYaw, afterYaw));
        Assert.That(deltaYaw, Is.EqualTo(5f).Within(AngleEpsilonDeg));

        Object.Destroy(player);
        Object.Destroy(camera);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ML2_LargeSensitivity_AppliesDegreesPerSecondScaledByDeltaTime()
    {
        var player = new GameObject("Player_ML2");
        var camera = new GameObject("Camera_ML2");
        camera.transform.SetParent(player.transform, worldPositionStays: false);

        var look = camera.AddComponent<MouseLook>();
        look.playerBody = player.transform;
        look.mouseSensitivity = 900f; // > 10 => per-second path (scaled by deltaTime)
        look.useOverrideInput = true;
        look.overrideLookInput = new Vector2(1f, 0f);

        float beforeYaw = player.transform.eulerAngles.y;
        yield return null; // allow Update() to run once
        float afterYaw = player.transform.eulerAngles.y;

        float expected = look.mouseSensitivity * Time.deltaTime;
        float deltaYaw = Mathf.Abs(Mathf.DeltaAngle(beforeYaw, afterYaw));
        Assert.That(deltaYaw, Is.EqualTo(expected).Within(1.5f));

        Object.Destroy(player);
        Object.Destroy(camera);
        yield return null;
    }
}
