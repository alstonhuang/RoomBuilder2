using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ArtOverrideLoader : MonoBehaviour
{
    private static readonly HashSet<string> ApplyingResourcePaths = new HashSet<string>(StringComparer.Ordinal);
    private static int ApplyDepth;
    private const int MaxApplyDepth = 8;

    [Header("Art Override (Resources)")]
    [SerializeField] private string overrideResourcePath = "RoomBuilder2Overrides/KeyArt";
    [SerializeField] private string artRootName = "Art";

    [Header("When To Apply")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private int deferFrames;
    [SerializeField] private bool applyInEditMode;

    [Header("Safety")]
    [SerializeField] private bool disablePlaceholderRendererWhenOverridePresent = true;
    [SerializeField] private bool disableCollidersOnOverride = true;
    [SerializeField] private bool clearArtRootChildren = true;
    [SerializeField] private bool refitRootColliderToOverrideBounds = true;
    [SerializeField] private bool debugLog;

    private GameObject _overrideInstance;
    private bool _isApplying;

    private void Awake()
    {
        if (!applyInEditMode && !Application.isPlaying) return;

        if (!applyOnStart)
        {
            ApplyOverrideIfPresent();
        }
    }

    private IEnumerator Start()
    {
        if (!applyOnStart) yield break;
        if (!applyInEditMode && !Application.isPlaying) yield break;

        int frames = Mathf.Max(0, deferFrames);
        for (int i = 0; i < frames; i++) yield return null;

        ApplyOverrideIfPresent();
    }

    public bool ApplyOverrideIfPresent()
    {
        if (!applyInEditMode && !Application.isPlaying) return false;
        if (string.IsNullOrWhiteSpace(overrideResourcePath)) return false;
        if (_isApplying) return false;

        // Guard against recursive overrides (e.g., if an override prefab itself contains an ArtOverrideLoader
        // pointing to the same resource path).
        if (ApplyDepth >= MaxApplyDepth)
        {
            Debug.LogError($"[ArtOverrideLoader] Aborted: recursive apply depth exceeded ({MaxApplyDepth}). Resource='{overrideResourcePath}' on {name}");
            return false;
        }

        if (!ApplyingResourcePaths.Add(overrideResourcePath))
        {
            if (debugLog) Debug.LogWarning($"[ArtOverrideLoader] Skipped recursive apply: '{overrideResourcePath}' on {name}");
            return false;
        }

        try
        {
            _isApplying = true;
            ApplyDepth++;

            var overridePrefab = Resources.Load<GameObject>(overrideResourcePath);
            if (overridePrefab == null)
            {
                if (debugLog) Debug.LogWarning($"[ArtOverrideLoader] Resources.Load failed: '{overrideResourcePath}' on {name}");
                return false;
            }

            if (overridePrefab.GetComponentInChildren<ArtOverrideLoader>(includeInactive: true) != null)
            {
                Debug.LogError($"[ArtOverrideLoader] Aborted: override prefab '{overridePrefab.name}' contains ArtOverrideLoader (must be art-only). Resource='{overrideResourcePath}' on {name}");
                return false;
            }

            var artRoot = GetOrCreateArtRoot();
            if (clearArtRootChildren) ClearChildren(artRoot);

            _overrideInstance = Instantiate(overridePrefab, artRoot, worldPositionStays: false);
            _overrideInstance.name = overridePrefab.name;

            RefreshOutlinesUnderRoot();

            if (disableCollidersOnOverride)
            {
                foreach (var c in _overrideInstance.GetComponentsInChildren<Collider>(includeInactive: true))
                {
                    c.enabled = false;
                }
            }

            if (disablePlaceholderRendererWhenOverridePresent)
            {
                var placeholder = GetComponent<Renderer>();
                if (placeholder != null) placeholder.enabled = false;
            }

            if (refitRootColliderToOverrideBounds)
            {
                TryRefitRootColliderToArtBounds(artRoot);
            }

            if (debugLog) Debug.Log($"[ArtOverrideLoader] Applied '{overrideResourcePath}' to {name}");
            return true;
        }
        finally
        {
            if (ApplyDepth > 0) ApplyDepth--;
            _isApplying = false;
            ApplyingResourcePaths.Remove(overrideResourcePath);
        }
    }

    private Transform GetOrCreateArtRoot()
    {
        var artRoot = transform.Find(artRootName);
        if (artRoot != null) return artRoot;

        var go = new GameObject(artRootName);
        artRoot = go.transform;
        artRoot.SetParent(transform, worldPositionStays: false);
        artRoot.localPosition = Vector3.zero;
        artRoot.localRotation = Quaternion.identity;
        artRoot.localScale = Vector3.one;
        return artRoot;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private void TryRefitRootColliderToArtBounds(Transform artRoot)
    {
        if (artRoot == null) return;

        if (!TryComputeWorldBounds(artRoot, out var worldBounds)) return;

        var localBounds = WorldBoundsToLocalAabb(transform, worldBounds);
        if (localBounds.size.sqrMagnitude <= 0f) return;

        var rootCollider = GetComponent<Collider>();
        if (rootCollider != null && !(rootCollider is BoxCollider))
        {
            Destroy(rootCollider);
            rootCollider = null;
        }
        if (rootCollider == null) rootCollider = gameObject.AddComponent<BoxCollider>();

        var box = (BoxCollider)rootCollider;
        box.center = localBounds.center;
        box.size = localBounds.size;
    }

    private static bool TryComputeWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool hasAny = false;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!r.enabled) continue;

            if (!hasAny)
            {
                bounds = r.bounds;
                hasAny = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return hasAny;
    }

    private static Bounds WorldBoundsToLocalAabb(Transform localSpace, Bounds worldBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        var worldCorners = new Vector3[8]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };

        Vector3 first = localSpace.InverseTransformPoint(worldCorners[0]);
        var local = new Bounds(first, Vector3.zero);
        for (int i = 1; i < worldCorners.Length; i++)
        {
            local.Encapsulate(localSpace.InverseTransformPoint(worldCorners[i]));
        }

        return local;
    }

    private void RefreshOutlinesUnderRoot()
    {
        var outlines = GetComponentsInChildren<Outline>(includeInactive: true);
        foreach (var outline in outlines)
        {
            if (outline == null) continue;
            outline.RefreshRenderers();
        }
    }
}
