using UnityEngine;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionDistance = 5f;
    public int keyCount = 0;
    public Image crosshairImage;
    public Color defaultColor = Color.white;
    public Color focusColor = Color.red;
    public Transform eye; // 視角來源（建議指定主攝影機）
    public bool debugRay = true;
    public bool debugInteract = true;

    private Interactable currentInteractable;

    void Update()
    {
        CheckHover();

        if (IsInteractPressed())
        {
            TryInteract();
        }
    }

    void CheckHover()
    {
        Transform origin = eye != null ? eye : (Camera.main != null ? Camera.main.transform : transform);
        if (origin == null) return;

        var hits = Physics.RaycastAll(origin.position, origin.forward, interactionDistance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            var newInteractable = h.transform.GetComponentInParent<Interactable>();
            if (newInteractable == null)
            {
                if (debugRay) Debug.Log($"[PlayerInteraction] Ray hit {h.collider.name} (no Interactable), continuing...");
                continue;
            }

            if (newInteractable != currentInteractable)
            {
                if (currentInteractable != null)
                    currentInteractable.OnLoseFocus();

                currentInteractable = newInteractable;
                currentInteractable.OnFocus();
            }

            SetCrosshairColor(focusColor);
            if (debugRay) Debug.Log($"[PlayerInteraction] Hit Interactable {newInteractable.name} at {h.point}");
            return;
        }

        //if (debugRay) Debug.Log("[PlayerInteraction] Ray hit nothing with Interactable");

        if (currentInteractable != null)
        {
            currentInteractable.OnLoseFocus();
            currentInteractable = null;
        }

        SetCrosshairColor(defaultColor);
    }

    void TryInteract()
    {
        if (currentInteractable != null)
        {
            if (debugInteract) Debug.Log($"[PlayerInteraction] Interact with {currentInteractable.name}");
            currentInteractable.OnInteract();
        }
        else if (debugInteract)
        {
            Debug.Log("[PlayerInteraction] Interact pressed but no target");
        }
    }

    bool IsInteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) return true;
#endif
        return Input.GetKeyDown(KeyCode.E);
    }

    private void SetCrosshairColor(Color color)
    {
        if (crosshairImage != null)
        {
            crosshairImage.color = color;
        }
    }

    public void AddKey()
    {
        keyCount++;
        Debug.Log("撿到鑰匙，持有數: " + keyCount);
    }

    public bool HasKey()
    {
        if (keyCount > 0)
        {
            keyCount--;
            return true;
        }
        return false;
    }
}
