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
    public LayerMask interactionMask = ~0; // 可選：限制互動的 Layer
    public bool includeTriggers = true;    // 射線是否打到 Trigger colliders
    public bool debugRay = true;
    public bool debugInteract = true;

    private Interactable currentInteractable;
    private Transform m_Eye; // 實際用來發射射線的來源

    void Awake()
    {
        ResolveEye(false);
    }

    void Start()
    {
        // Allow other scripts/tests to assign `eye` before Start (AddComponent triggers Awake immediately).
        ResolveEye(true);
    }

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
        Transform origin = m_Eye != null ? m_Eye : transform;

        var hits = Physics.RaycastAll(
            origin.position,
            origin.forward,
            interactionDistance,
            interactionMask,
            includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore
        );
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
        return Input.GetKeyDown(KeyCode.E);
    }

    private void SetCrosshairColor(Color color)
    {
        if (crosshairImage != null)
        {
            crosshairImage.color = color;
        }
    }

    private void ResolveEye(bool warnIfFallback)
    {
        if (eye != null)
        {
            m_Eye = eye;
            return;
        }

        // 優先找子節點的 Camera
        var cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            m_Eye = cam.transform;
            return;
        }

        // 再退回到主攝影機
        if (Camera.main != null)
        {
            m_Eye = Camera.main.transform;
            return;
        }

        // 最後保底：使用自身 Transform，並提醒需要設定
        m_Eye = transform;
        m_Eye = transform;
        if (warnIfFallback)
        {
            Debug.LogWarning("[PlayerInteraction] No eye/camera assigned; using self transform for raycasts. Assign 'eye' to your camera for accurate interaction.");
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
