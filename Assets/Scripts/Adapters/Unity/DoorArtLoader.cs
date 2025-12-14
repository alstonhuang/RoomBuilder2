using UnityEngine;

namespace MyGame.Adapters.Unity
{
    /// <summary>
    /// Instantiates replaceable art Prefabs into predefined slots so the logical door skeleton stays the same.
    /// </summary>
    public class DoorArtLoader : MonoBehaviour
    {
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
            BuildFrameIfProvided();

            // Decide which prefabs to use for trims
            var top = useSharedTrimPrefab && trimPrefab ? trimPrefab : topPrefab;
            var left = useSharedTrimPrefab && trimPrefab ? trimPrefab : leftPrefab;
            var right = useSharedTrimPrefab && trimPrefab ? trimPrefab : rightPrefab;

            SetSlotScale(topSlot, topSize);
            SetSlotScale(leftSlot, sideSize);
            SetSlotScale(rightSlot, sideSize);
            SetSlotScale(doorSlot, doorSize);

            BuildSlot(topSlot, top, topSize, Vector3.zero, Vector3.one);
            BuildSlot(leftSlot, left, sideSize, Vector3.zero, Vector3.one);
            BuildSlot(rightSlot, right, sideSize, Vector3.zero, Vector3.one);
            BuildSlot(doorSlot, doorPrefab, doorSize, doorOffset, doorScale);

            if (alignDoorToFrame)
            {
                AlignDoorToFrame();
            }
        }

        void OnEnable()
        {
            if (rebuildOnEnable)
            {
                RebuildArt();
            }
        }

        private bool BuildFrameIfProvided()
        {
            if (frameSlot == null) return false;
            ClearChildren(frameSlot);

            if (framePrefab != null)
            {
                var instance = Instantiate(framePrefab, frameSlot);
                ResetLocal(instance.transform);
                ApplyOffsetAndScale(instance.transform, frameOffset, frameScale);
                return true;
            }

            if (createFallbackPrimitives)
            {
                var instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.SetParent(frameSlot, false);
                instance.transform.localScale = new Vector3(1f, 2f, 0.2f);
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
                instance.transform.localScale = fallbackScale;
            }

            if (instance != null)
            {
                ResetLocal(instance.transform);
                ApplyOffsetAndScale(instance.transform, offset, scaleMult);
            }
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

            doorSlot.localPosition += adjust;

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
                    doorSlot.localPosition += adjust;
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
