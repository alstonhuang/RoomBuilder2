using UnityEngine;

namespace MyGame.Adapters.Unity
{
    /// <summary>
    /// Instantiates replaceable art Prefabs into predefined slots so the logical door skeleton stays the same.
    /// </summary>
    public class DoorArtLoader : MonoBehaviour
    {
        private const string FrameContentName = "__FrameContent";
        private const string DefaultFrameName = "Frame";
        private const string DefaultDoorSlotName = "DoorSlot";
        private const string DefaultTopSlotName = "TopSlot";
        private const string DefaultLeftSlotName = "LeftSlot";
        private const string DefaultRightSlotName = "RightSlot";

        [Header("Slots")]
        public Transform frameSlot;
        public Transform topSlot;
        public Transform leftSlot;
        public Transform rightSlot;
        public Transform doorSlot; 

        [Header("Art Prefabs")]
        [Tooltip("Single prefab for frame (full surround). Optional.")]
        public GameObject framePrefab;
        [Tooltip("If true, trimPrefab is used for top/left/right. Otherwise use individual prefabs.")]
        public bool useSharedTrimPrefab = true;
        public GameObject trimPrefab;
        public GameObject topPrefab;
        public GameObject leftPrefab;
        public GameObject rightPrefab;
        public GameObject doorPrefab;

        [Header("Behaviour")]
        public bool rebuildOnEnable = true;
        public bool createFallbackPrimitives = true;

#if UNITY_EDITOR
        [Tooltip("If enabled, rebuilds art in Edit Mode too (useful in Prefab Mode).")]
        public bool rebuildInEditMode = false;

        [Tooltip("If enabled, only rebuild in Edit Mode while editing a prefab in Prefab Mode (not in regular scenes).")]
        public bool onlyInPrefabModeInEdit = true;
#endif

        [Header("Target Sizes (slot scales)")]
        public Vector3 topSize = new Vector3(1f, 0.2f, 0.2f);
        public Vector3 sideSize = new Vector3(0.2f, 2f, 0.2f);
        public Vector3 doorSize = new Vector3(1f, 2f, 0.1f);
        public Vector3 frameOffset = Vector3.zero;
        public Vector3 frameScale = Vector3.one;
        public Vector3 doorOffset = Vector3.zero;
        public Vector3 doorScale = Vector3.one;
        public bool alignDoorToFrame = true;
        public bool scaleDoorToFrame = true;
        [Tooltip("Extra multiplicative padding applied after scaling to frame bounds (e.g., slightly smaller door leaf).")]
        public Vector3 doorFitPadding = new Vector3(0.98f, 0.99f, 0.98f);
        [Tooltip("Extra inset along X toward the hinge side to avoid poking through the frame.")]
        public float doorInsetX = 0.02f;

        [ContextMenu("Rebuild Art")]
        public void RebuildArt()
        {
            EnsureSlotRefs();

            // Reset door slot before alignment so repeated rebuilds don't drift.
            if (doorSlot != null) doorSlot.localPosition = Vector3.zero;

            var frameContent = GetOrCreateChild(frameSlot, FrameContentName);
            BuildFrameIfProvided(frameContent);

            // Decide which prefabs to use for trims
            var top = useSharedTrimPrefab && trimPrefab ? trimPrefab : topPrefab;
            var left = useSharedTrimPrefab && trimPrefab ? trimPrefab : leftPrefab;
            var right = useSharedTrimPrefab && trimPrefab ? trimPrefab : rightPrefab;

            // Compute a top span that covers the door + both side trims.
            var computedTopSize = new Vector3(doorSize.x + sideSize.x * 2f, topSize.y, topSize.z);

            // Place trims around the opening using a bottom-at-0 convention.
            // Frame height is driven by sideSize.y (opening height), while doorSize.y is the leaf height.
            float openingH = Mathf.Max(0.01f, sideSize.y);
            float openingHalfH = openingH * 0.5f;
            float doorHalfH = doorSize.y * 0.5f;
            // Position hinge pivot at the door edge so the leaf can be centered in the opening when closed.
            var hinge = ResolveHinge();
            bool hingeLeft = true;
            var ctrl = GetComponent<DoorController>();
            if (ctrl != null) hingeLeft = ctrl.hingeOnLeft;
            float hingeX = hingeLeft ? -(doorSize.x * 0.5f) : (doorSize.x * 0.5f);
            if (hinge != null) hinge.localPosition = new Vector3(hingeX, 0f, 0f);

            if (doorSlot != null)
            {
                // Keep the door leaf centered in the opening.
                float slotX = hinge != null ? -hinge.localPosition.x : 0f;
                doorSlot.localPosition = new Vector3(slotX, doorHalfH, 0f);
            }
            if (leftSlot != null) leftSlot.localPosition = new Vector3(-(doorSize.x * 0.5f + sideSize.x * 0.5f), openingHalfH, 0f);
            if (rightSlot != null) rightSlot.localPosition = new Vector3(doorSize.x * 0.5f + sideSize.x * 0.5f, openingHalfH, 0f);
            if (topSlot != null) topSlot.localPosition = new Vector3(0f, openingH - computedTopSize.y * 0.5f, 0f);

            SetSlotScale(topSlot, computedTopSize);
            SetSlotScale(leftSlot, sideSize);
            SetSlotScale(rightSlot, sideSize);
            SetSlotScale(doorSlot, doorSize);

            BuildSlot(topSlot, top, Vector3.one, Vector3.zero, Vector3.one);
            BuildSlot(leftSlot, left, Vector3.one, Vector3.zero, Vector3.one);
            BuildSlot(rightSlot, right, Vector3.one, Vector3.zero, Vector3.one);
            BuildSlot(doorSlot, doorPrefab, Vector3.one, doorOffset, doorScale);

            if (alignDoorToFrame)
            {
                AlignDoorToFrame();
            }
        }

        void OnEnable()
        {
            if (rebuildOnEnable)
            {
                if (Application.isPlaying)
                {
                    RebuildArt();
                    return;
                }

#if UNITY_EDITOR
                if (rebuildInEditMode && (!onlyInPrefabModeInEdit || IsInPrefabStage()))
                {
                    RebuildArt();
                }
#endif
            }
        }

#if UNITY_EDITOR
        private bool IsInPrefabStage()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return false;
            return gameObject.scene == stage.scene;
        }
#endif

        private bool BuildFrameIfProvided(Transform frameContentSlot)
        {
            if (frameContentSlot == null) return false;
            ClearChildren(frameContentSlot);

            if (framePrefab != null)
            {
                var instance = Instantiate(framePrefab, frameContentSlot);
                ResetLocal(instance.transform);
                ApplyOffsetAndScale(instance.transform, frameOffset, frameScale);
                return true;
            }

            return false;
        }

        private void BuildSlot(Transform slot, GameObject prefab, Vector3 fallbackScale, Vector3 offset, Vector3 scaleMult)
        {
            if (slot == null) return;
            ClearChildren(slot);

            GameObject instance = null;
            if (prefab != null)
            {
                instance = Instantiate(prefab, slot);
                ResetLocal(instance.transform);
            }
            else if (createFallbackPrimitives)
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.SetParent(slot, false);
                ResetLocal(instance.transform);
                instance.transform.localScale = Vector3.one;
            }

            if (instance != null)
            {
                // Help DoorController identify what is the door leaf even when using fallbacks.
                if (slot == doorSlot) instance.name = "DoorLeaf";
                else if (slot == topSlot) instance.name = "FrameTop";
                else if (slot == leftSlot) instance.name = "FrameLeft";
                else if (slot == rightSlot) instance.name = "FrameRight";

                // The leaf prefab might itself contain interaction/door scripts. When nested under a DoorSystem,
                // the DoorController on the DoorSystem should be the single authority to prevent double-rotation.
                if (slot == doorSlot)
                {
                    StripLeafControlComponents(instance);
                }

                ApplyOffsetAndScale(instance.transform, offset, scaleMult);

                if (slot == doorSlot)
                {
                    EnsureNonTriggerCollider(instance);
                }
            }
        }

        private static void EnsureNonTriggerCollider(GameObject go)
        {
            if (go == null) return;

            var existing = go.GetComponentInChildren<Collider>(includeInactive: true);
            if (existing != null)
            {
                existing.isTrigger = false;
                existing.enabled = true;
                return;
            }

            if (!TryComputeWorldBounds(go.transform, out var worldBounds))
            {
                var fallback = go.AddComponent<BoxCollider>();
                fallback.isTrigger = false;
                fallback.center = Vector3.zero;
                fallback.size = Vector3.one;
                return;
            }

            var localBounds = WorldBoundsToLocalAabb(go.transform, worldBounds);
            if (localBounds.size.sqrMagnitude <= 0f)
            {
                var fallback = go.AddComponent<BoxCollider>();
                fallback.isTrigger = false;
                fallback.center = Vector3.zero;
                fallback.size = Vector3.one;
                return;
            }

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = false;
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

        private void StripLeafControlComponents(GameObject leaf)
        {
            if (leaf == null) return;

            var door = leaf.GetComponent<DoorController>();
            if (door != null)
            {
                if (Application.isPlaying) Destroy(door);
                else DestroyImmediate(door);
            }

            var interactable = leaf.GetComponent<Interactable>();
            if (interactable != null)
            {
                if (Application.isPlaying) Destroy(interactable);
                else DestroyImmediate(interactable);
            }
        }

        private void EnsureSlotRefs()
        {
            if (frameSlot == null) frameSlot = FindDeepChild(transform, DefaultFrameName);
            if (doorSlot == null) doorSlot = FindDeepChild(transform, DefaultDoorSlotName);

            // Create trim slots if missing (keep them under frameSlot so they're not part of the door hinge pivot).
            var trimsParent = frameSlot != null ? frameSlot : transform;

            if (topSlot == null) topSlot = GetOrCreateChild(trimsParent, DefaultTopSlotName);
            if (leftSlot == null) leftSlot = GetOrCreateChild(trimsParent, DefaultLeftSlotName);
            if (rightSlot == null) rightSlot = GetOrCreateChild(trimsParent, DefaultRightSlotName);

            // Move any legacy authored frame mesh under a dedicated content slot so we can safely clear/rebuild it.
            if (frameSlot != null)
            {
                var frameContent = GetOrCreateChild(frameSlot, FrameContentName);
                var toMove = new System.Collections.Generic.List<Transform>();
                foreach (Transform c in frameSlot)
                {
                    if (c == null) continue;
                    if (c == frameContent) continue;
                    if (c == topSlot || c == leftSlot || c == rightSlot) continue;
                    toMove.Add(c);
                }
                foreach (var c in toMove)
                {
                    c.SetParent(frameContent, true);
                }
            }
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            var t = parent.Find(childName);
            if (t != null) return t;
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == name) return t;
            }
            return null;
        }

        private Transform ResolveHinge()
        {
            var ctrl = GetComponent<DoorController>();
            if (ctrl != null && ctrl.hinge != null) return ctrl.hinge;
            if (doorSlot != null && doorSlot.parent != null) return doorSlot.parent;
            return FindDeepChild(transform, "HingeSlot") ?? FindDeepChild(transform, "DoorHinge") ?? FindDeepChild(transform, "Hinge");
        }

        private void ClearChildren(Transform slot)
        {
            if (slot == null) return;
            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                var child = slot.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void ResetLocal(Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        private void SetSlotScale(Transform slot, Vector3 targetScale)
        {
            if (slot == null) return;
            slot.localScale = targetScale;
        }

        private void ApplyOffsetAndScale(Transform t, Vector3 offset, Vector3 scaleMultiplier)
        {
            if (t == null) return;
            t.localPosition += offset;
            t.localScale = Vector3.Scale(t.localScale, scaleMultiplier);
        }

        private void AlignDoorToFrame()
        {
            if (frameSlot == null || doorSlot == null) return;
            if (!TryGetBounds(frameSlot, out var fb) || !TryGetBounds(doorSlot, out var db)) return;

            // Align bottoms and center depth to frame
            Vector3 adjust = Vector3.zero;
            adjust.y = fb.min.y - db.min.y;
            adjust.z = fb.center.z - db.center.z;

            // Align hinge side horizontally
            bool hingeLeft = true;
            var ctrl = GetComponent<DoorController>();
            if (ctrl != null) hingeLeft = ctrl.hingeOnLeft;

            adjust.x = hingeLeft ? (fb.min.x - db.min.x + doorInsetX) : (fb.max.x - db.max.x - doorInsetX);

            doorSlot.position += adjust;

            if (scaleDoorToFrame)
            {
                var scale = doorSlot.localScale;
                if (Mathf.Abs(db.size.x) > 0.0001f) scale.x *= (fb.size.x * doorFitPadding.x) / db.size.x;
                if (Mathf.Abs(db.size.y) > 0.0001f) scale.y *= (fb.size.y * doorFitPadding.y) / db.size.y;
                if (Mathf.Abs(db.size.z) > 0.0001f) scale.z *= (fb.size.z * doorFitPadding.z) / db.size.z;
                doorSlot.localScale = scale;

                // Recompute bounds after scaling for a tighter fit
                if (TryGetBounds(doorSlot, out db))
                {
                    adjust = Vector3.zero;
                    adjust.y = fb.min.y - db.min.y;
                    adjust.z = fb.center.z - db.center.z;
                    adjust.x = hingeLeft ? (fb.min.x - db.min.x + doorInsetX) : (fb.max.x - db.max.x - doorInsetX);
                    doorSlot.position += adjust;
                }
            }
        }

        private bool TryGetBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return true;
        }
    }
}
