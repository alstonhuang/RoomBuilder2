using UnityEngine;

public class DoorController : MonoBehaviour
{
    public bool isLocked = true;
    [Tooltip("Optional hinge pivot; if null will auto-find a child named 'DoorHinge' or fall back to self.")]
    public Transform hinge;
    [Tooltip("Optional local offset applied to the hinge after setup (useful when the pivot sits at the door center).")]
    public Vector3 hingeLocalOffset = Vector3.zero;
    [Tooltip("Auto-align the hinge to the left/right edge of the door leaf when no offset is provided.")]
    public bool autoAlignHinge = true;
    public bool hingeOnLeft = true; // Left = min local X, Right = max local X
    
    // 開/關旋轉角度
    private float openAngle = 90f; 
    private float closeAngle = 0f;
    
    private bool isOpen = false;
    private Transform _pivot;
    [SerializeField] private bool debugLog = false;

    void Awake()
    {
        // Prefer explicit hinge, otherwise search in self and children for a DoorHinge/Hinge transform.
        _pivot = hinge;
        if (_pivot == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "DoorHinge" || t.name == "Hinge")
                {
                    _pivot = t;
                    break;
                }
            }
        }
        if (_pivot == null) _pivot = transform;

        // Only move the actual door leaf under the hinge; keep frames static.
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t == _pivot) continue;
            if (t.IsChildOf(_pivot)) continue;

            string n = t.name.ToLower();
            bool looksLikeDoor = n.Contains("door"); // avoid reparenting Frame_* panels
            bool hasRenderable = t.GetComponent<Renderer>() != null || t.GetComponent<Collider>() != null;
            if (looksLikeDoor && hasRenderable)
            {
                t.SetParent(_pivot, true); // keep world pose
            }
        }

        AutoAlignPivotOffset();
        ApplyPivotOffset();
    }

    void OnEnable()
    {
        // Ensure hinge is parented to this door root so local rotation is relative to the frame.
        if (_pivot != null && _pivot.parent != transform)
        {
            _pivot.SetParent(transform, true); // keep world pose
        }
        ResetClosed();
        if (debugLog) Debug.Log($"[DoorController] OnEnable reset. isOpen={isOpen} pivot={_pivot?.name}");
    }

    void Update()
    {
        float targetAngleY = isOpen ? openAngle : closeAngle;
        Quaternion targetRotation = Quaternion.Euler(0, targetAngleY, 0);
        if (_pivot == null) _pivot = transform;
        _pivot.localRotation = Quaternion.Slerp(_pivot.localRotation, targetRotation, Time.deltaTime * 5f);
    }

    // 由 Interactable 呼叫
    public void TryOpen()
    {
        Debug.Log($"[DoorController] TryOpen on {name}, isLocked={isLocked}, isOpen={isOpen}");
        if (isLocked)
        {
            var player = FindAnyObjectByType<PlayerInteraction>();
            if (player != null && player.HasKey())
            {
                UnlockDoor();
                isOpen = true; 
                Debug.Log("[DoorController] 使用了鑰匙，門已解鎖並打開");
            }
            else
            {
                Debug.Log("[DoorController] 需要鑰匙才能打開這扇門");
            }
        }
        else
        {
            isOpen = !isOpen; 
        }
    }

    public void UnlockDoor()
    {
        isLocked = false;
        Debug.Log("[DoorController] 門已解鎖");
    }

    private void ResetClosed()
    {
        isOpen = false;
        if (_pivot != null) _pivot.localRotation = Quaternion.Euler(0, closeAngle, 0);
        if (_pivot != null)
        {
            // Door leafs under pivot should start at identity so pivot rotation fully controls them.
            foreach (var t in _pivot.GetComponentsInChildren<Transform>(true))
            {
                if (t == _pivot) continue;
                string n = t.name.ToLower();
                if (n.Contains("door")) t.localRotation = Quaternion.identity;
            }
        }
    }

    private void AutoAlignPivotOffset()
    {
        if (!autoAlignHinge) return;
        if (hingeLocalOffset != Vector3.zero) return;
        if (_pivot == null) return;

        var renderers = _pivot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        bool hasBounds = false;
        Vector3 worldMin = Vector3.zero;
        Vector3 worldMax = Vector3.zero;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!hasBounds)
            {
                worldMin = r.bounds.min;
                worldMax = r.bounds.max;
                hasBounds = true;
            }
            else
            {
                worldMin = Vector3.Min(worldMin, r.bounds.min);
                worldMax = Vector3.Max(worldMax, r.bounds.max);
            }
        }
        if (!hasBounds) return;

        Vector3 localMin = _pivot.InverseTransformPoint(worldMin);
        Vector3 localMax = _pivot.InverseTransformPoint(worldMax);
        float targetX = hingeOnLeft ? localMin.x : localMax.x;

        hingeLocalOffset = new Vector3(targetX, 0f, 0f);
        if (debugLog) Debug.Log($"[DoorController] Auto-aligned hinge offset to {hingeLocalOffset} (side={(hingeOnLeft ? "Left" : "Right")}) on {name}");
    }

    private void ApplyPivotOffset()
    {
        if (_pivot == null) return;
        if (hingeLocalOffset == Vector3.zero) return;

        // Move the pivot while preserving child world positions.
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in _pivot) children.Add(t);

        _pivot.localPosition += hingeLocalOffset;
        foreach (var child in children)
        {
            child.position -= _pivot.TransformVector(hingeLocalOffset);
        }
    }
}
