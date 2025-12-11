using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    public UnityEvent onInteract;
    public Outline outlineScript; // 若未指定，會嘗試抓子物件的 Outline（不再自動新增，避免意外外觀）
    public bool debugInteract = false;

    void Start()
    {
        if (outlineScript == null) outlineScript = GetComponentInChildren<Outline>();
        if (outlineScript != null) outlineScript.enabled = false;
    }

    public void OnInteract()
    {
        if (debugInteract) Debug.Log($"[Interactable] OnInteract on {name}");

        // If event is wired in prefab, use it; otherwise try common fallbacks.
        if (onInteract != null && onInteract.GetPersistentEventCount() > 0)
        {
            onInteract.Invoke();
            if (debugInteract) Debug.Log($"[Interactable] Invoked UnityEvent on {name}");
            return;
        }

        // Fallback: try known interactables if no listeners were set up.
        var key = GetComponent<KeyController>();
        if (key != null)
        {
            if (debugInteract) Debug.Log($"[Interactable] Fallback KeyController.PickUp on {name}");
            key.PickUp();
            return;
        }

        var door = GetComponent<DoorController>();
        if (door != null)
        {
            if (debugInteract) Debug.Log($"[Interactable] Fallback DoorController.TryOpen on {name}");
            door.TryOpen();
            return;
        }

        // If nothing to do, at least invoke empty event to stay consistent.
        onInteract.Invoke();
    }

    public void OnFocus()
    {
        if (outlineScript != null) outlineScript.enabled = true;
    }

    public void OnLoseFocus()
    {
        if (outlineScript != null) outlineScript.enabled = false;
    }
}
