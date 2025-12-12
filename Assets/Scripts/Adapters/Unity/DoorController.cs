using UnityEngine;

public class DoorController : MonoBehaviour
{
    public bool isLocked = true;
    [Tooltip("Optional hinge pivot; if null will auto-find a child named 'DoorHinge' or fall back to self.")]
    public Transform hinge;
    
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
}
