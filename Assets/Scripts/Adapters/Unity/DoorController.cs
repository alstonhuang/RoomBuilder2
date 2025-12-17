using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    public bool isLocked = true;

    [Tooltip("Optional hinge pivot; if null will auto-find a child named 'DoorHinge' or 'Hinge' or fall back to self.")]
    public Transform hinge;

    [Tooltip("Optional local offset applied to the hinge after setup (useful when the pivot sits at the door center).")]
    public Vector3 hingeLocalOffset = Vector3.zero;

    [Tooltip("Auto-align the hinge to the left/right edge of the door leaf when no offset is provided.")]
    public bool autoAlignHinge = true;

    public bool hingeOnLeft = true;

    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float closeAngle = 0f;
    [SerializeField] private float rotateSpeedDegPerSec = 240f;
    [SerializeField] private bool debugLog = false;

    private bool _isOpen;
    private int _openSign = 1;
    private bool _openSignChosen;
    private float _currentYaw;
    private Transform _pivot;
    private bool _pivotBaseCaptured;
    private Vector3 _pivotBaseLocalPosition;

    void Awake()
    {
        _pivot = ResolvePivot();
        EnsureOnlyDoorRotates();
        CapturePivotBaseLocalPosition();

        AutoAlignPivotOffset();
        ApplyPivotOffset();
    }

    /// <summary>
    /// Call this after runtime art rebuilds (e.g., DoorArtLoader.RebuildArt) so the controller can re-resolve the pivot
    /// and ensure the door leaf is the only thing that rotates.
    /// </summary>
    public void RefreshAfterArt(bool resetClosed = true)
    {
        _pivot = ResolvePivot();
        EnsureOnlyDoorRotates();
        CapturePivotBaseLocalPosition();

        if (autoAlignHinge)
        {
            AutoAlignPivotOffset();
            ApplyPivotOffset();
        }

        if (resetClosed) ResetClosed();
    }

    void OnEnable()
    {
        if (_pivot == null) _pivot = ResolvePivot();

        // Ensure hinge is parented to this door root so local rotation is relative to the frame.
        if (_pivot != null && _pivot.parent != transform)
        {
            _pivot.SetParent(transform, true);
        }

        CapturePivotBaseLocalPosition();
        ResetClosed();
        if (debugLog) Debug.Log($"[DoorController] OnEnable reset. isOpen={_isOpen} pivot={_pivot?.name}");
    }

    void Update()
    {
        if (_pivot == null) _pivot = transform;

        float targetAngleY = _isOpen ? (closeAngle + _openSign * openAngle) : closeAngle;
        _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, targetAngleY, rotateSpeedDegPerSec * Time.deltaTime);
        _pivot.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);
    }

    public void TryOpen()
    {
        Debug.Log($"[DoorController] TryOpen on {name}, isLocked={isLocked}, isOpen={_isOpen}");

        if (isLocked)
        {
            var player = FindAnyObjectByType<PlayerInteraction>();
            if (player != null && player.HasKey())
            {
                UnlockDoor();
                if (!_openSignChosen)
                {
                    _openSign = ChooseOpenSign();
                    _openSignChosen = true;
                }
                _isOpen = true;
                Debug.Log("[DoorController] Used a key. Door unlocked and opened.");
            }
            else
            {
                Debug.Log("[DoorController] Door is locked. A key is required.");
            }
            return;
        }

        // Choose direction once so repeated open/close does not flip or drift.
        if (!_isOpen && !_openSignChosen)
        {
            _openSign = ChooseOpenSign();
            _openSignChosen = true;
        }

        _isOpen = !_isOpen;
    }

    private int ChooseOpenSign()
    {
        // Default heuristic: hinge side implies a preferred open direction (prevents opening into the frame for many prefabs).
        int fallback = hingeOnLeft ? 1 : -1;

        var pivot = _pivot != null ? _pivot : ResolvePivot();
        if (pivot == null) return fallback;

        var player = FindAnyObjectByType<PlayerInteraction>();
        if (player == null) return fallback;

        // Find a representative door leaf renderer under the pivot.
        var leafRenderer = pivot.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(r => r != null && LooksLikeDoorLeaf(r.transform));
        if (leafRenderer == null) return fallback;

        // Evaluate both directions without mutating transforms (avoids quaternion drift/spin over repeated toggles).
        Vector3 pivotLocalCenter = pivot.InverseTransformPoint(leafRenderer.bounds.center);
        Vector3 playerPos = player.transform.position;

        Vector3 centerPos = pivot.TransformPoint(Quaternion.Euler(0f, openAngle, 0f) * pivotLocalCenter);
        Vector3 centerNeg = pivot.TransformPoint(Quaternion.Euler(0f, -openAngle, 0f) * pivotLocalCenter);

        float distPos = (centerPos - playerPos).sqrMagnitude;
        float distNeg = (centerNeg - playerPos).sqrMagnitude;

        if (Mathf.Abs(distPos - distNeg) < 0.0001f) return fallback;
        return distPos > distNeg ? 1 : -1;
    }

    public void UnlockDoor()
    {
        isLocked = false;
        Debug.Log("[DoorController] Door unlocked.");
    }

    private Transform ResolvePivot()
    {
        if (hinge != null) return hinge;

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "DoorHinge" || t.name == "Hinge" || t.name == "HingeSlot")
                return t;
        }

        return transform;
    }

    private void EnsureOnlyDoorRotates()
    {
        if (_pivot == null || _pivot == transform) return;

        // If the prefab authored the frame under the hinge, move it out so only the door leaf rotates.
        var pivotChildren = new List<Transform>();
        foreach (Transform c in _pivot) pivotChildren.Add(c);

        foreach (var c in pivotChildren)
        {
            if (c == null) continue;
            string n = c.name.ToLowerInvariant();

            bool isDoor = LooksLikeDoorLeaf(c);
            bool isDoorSlot = n.Contains("doorslot") || n == "door";
            bool isFrameLike = n.Contains("frame") || (n.Contains("slot") && !n.Contains("door")) || n.Contains("trim");

            if (isFrameLike && !isDoor && !isDoorSlot)
            {
                // Preserve world pose; we only want to move it out of the hinge hierarchy so it doesn't rotate.
                c.SetParent(transform, true);
            }
        }

        // Ensure the actual door leaf is under the pivot (keep world pose).
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t == _pivot) continue;
            if (t.IsChildOf(_pivot)) continue;
            if (!LooksLikeDoorLeaf(t)) continue;

            bool hasRenderable = t.GetComponentInChildren<Renderer>(true) != null || t.GetComponentInChildren<Collider>(true) != null;
            if (!hasRenderable) continue;

            t.SetParent(_pivot, true);
        }
    }

    private bool LooksLikeDoorLeaf(Transform t)
    {
        if (t == null) return false;
        string n = t.name.ToLowerInvariant();
        if (!n.Contains("door")) return false;
        if (n.Contains("frame")) return false;
        if (n.Contains("slot")) return false;
        return true;
    }

    private void ResetClosed()
    {
        _isOpen = false;
        _currentYaw = closeAngle;
        if (_pivot != null) _pivot.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);

        if (_pivot == null) return;
        foreach (var t in _pivot.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t == _pivot) continue;
            if (LooksLikeDoorLeaf(t)) t.localRotation = Quaternion.identity;
        }
    }

    private void AutoAlignPivotOffset()
    {
        if (!autoAlignHinge) return;
        if (hingeLocalOffset != Vector3.zero) return;
        if (_pivot == null) return;

        var renderers = _pivot.GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null && LooksLikeDoorLeaf(r.transform))
            .ToArray();
        if (renderers.Length == 0) return;

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
        if (debugLog)
            Debug.Log($"[DoorController] Auto-aligned hinge offset to {hingeLocalOffset} on {name} (side={(hingeOnLeft ? "Left" : "Right")})");
    }

    private void ApplyPivotOffset()
    {
        if (_pivot == null) return;
        if (hingeLocalOffset == Vector3.zero) return;

        var children = new List<Transform>();
        foreach (Transform t in _pivot) children.Add(t);

        // Move pivot to a stable base+offset position (avoid accumulating offsets on repeated rebuild/enable).
        Vector3 oldWorld = _pivot.position;
        _pivot.localPosition = _pivotBaseCaptured ? (_pivotBaseLocalPosition + hingeLocalOffset) : (_pivot.localPosition + hingeLocalOffset);
        Vector3 newWorld = _pivot.position;
        Vector3 worldDelta = newWorld - oldWorld;
        foreach (var child in children)
        {
            if (child == null) continue;
            child.position -= worldDelta;
        }
    }

    private void CapturePivotBaseLocalPosition()
    {
        if (_pivot == null) return;
        if (_pivot.parent == null) return;
        // Treat the currently-authored pivot pose as "base", even if an offset was already applied earlier.
        _pivotBaseLocalPosition = _pivot.localPosition - hingeLocalOffset;
        _pivotBaseCaptured = true;
    }
}
