using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MyGame.Core;
using ILogger = MyGame.Core.ILogger;
using ImportedCore = MyGame_1.Core;

namespace MyGame.Adapters.Unity
{
    public class RoomBuilder : MonoBehaviour
    {
        public List<ItemDefinition> database;
        public List<RoomTheme> themeDatabase;

        [Header("Optional Default Database (Resources)")]
        public string resourcesItemDatabasePath = "RoomBuilder2/ItemDatabase";

        [Header("Room Settings")]
        public string themeToBuild = "LivingRoom";
        public Vector3 roomSize = new Vector3(10, 2, 10);

        public RoomBlueprint blueprint;

        [Header("Debug")]
        public bool debugSnapping;

        private bool m_SkipNorthWallGeneration; // +Z
        private bool m_SkipSouthWallGeneration; // -Z
        private bool m_SkipEastWallGeneration;  // +X
        private bool m_SkipWestWallGeneration;  // -X

        private Vector3? m_CachedWallSize;
        private readonly HashSet<string> m_PhysicalInstanceIds = new HashSet<string>();

        public void SetWallGenerationFlags(bool skipNorth, bool skipSouth, bool skipEast, bool skipWest)
        {
            m_SkipNorthWallGeneration = skipNorth;
            m_SkipSouthWallGeneration = skipSouth;
            m_SkipEastWallGeneration = skipEast;
            m_SkipWestWallGeneration = skipWest;

            Debug.Log(
                $"[{name}] Wall generation flags set: North={m_SkipNorthWallGeneration}, South={m_SkipSouthWallGeneration}, East={m_SkipEastWallGeneration}, West={m_SkipWestWallGeneration}");
        }

        [ContextMenu("Clear All")]
        public void Clear()
        {
            var children = transform.Cast<Transform>().ToList();
            foreach (var child in children)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            Debug.Log("[RoomBuilder] Cleared spawned objects");
        }

        [ContextMenu("Generate Blueprint")]
        public void GenerateBlueprint()
        {
            Debug.Log($"[{name}] Generating blueprint...");

            var importedGen = GetComponent<ImportedCore.RoomGenerator>();
            if (importedGen != null)
            {
                var importedBp = importedGen.GenerateStackDemo();
                blueprint = MyGame.Adapters.Imported.ImportedCoreMapper.ToCore(importedBp);
                Debug.Log($"[{name}] Blueprint generated (imported) with {blueprint.nodes?.Count ?? 0} nodes.");
                return;
            }

            ILogger logger = new LoggerAdapter();
            IItemLibrary library = new ItemLibraryAdapter(database, themeDatabase);
            RoomGenerator generator = new RoomGenerator(logger, library);

            var coreCenter = new SimpleVector3(0, roomSize.y / 2f, 0);
            var bounds = new SimpleBounds(coreCenter, new SimpleVector3(roomSize.x, roomSize.y, roomSize.z));

            blueprint = generator.GenerateFromTheme(
                bounds,
                themeToBuild,
                m_SkipNorthWallGeneration,
                m_SkipSouthWallGeneration,
                m_SkipEastWallGeneration,
                m_SkipWestWallGeneration);

            Debug.Log($"[{name}] Blueprint generated with {blueprint.nodes.Count} nodes.");
        }

        [ContextMenu("Build from Generated Blueprint")]
        public void BuildFromGeneratedBlueprint()
        {
            Debug.Log($"[{name}] Building from generated blueprint...");

            if (blueprint == null)
            {
                Debug.LogError($"[{name}] Blueprint is not generated yet. Please call GenerateBlueprint() first.");
                return;
            }

            Clear();

            var spawnedMap = BuildFromBlueprint(blueprint);
            ApplyPhysicsSnapping(spawnedMap, blueprint);

            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = roomSize;
            collider.isTrigger = true;

            gameObject.AddComponent<RoomTrigger>();
            Debug.Log($"[{name}] Finished building.");
        }

        [ContextMenu("Build")]
        public void Build()
        {
            GenerateBlueprint();
            BuildFromGeneratedBlueprint();
        }

        [ContextMenu("Debug/Dump Height Outliers")]
        public void DebugDumpHeightOutliers()
        {
            var all = GetComponentsInChildren<Transform>(true);
            var entries = new List<(string name, float posY, float minY, float maxY)>();

            foreach (var t in all)
            {
                if (t == null || t == transform) continue;
                float posY = t.position.y;
                float minY = posY;
                float maxY = posY;
                if (TryGetBounds(t.gameObject, out var b))
                {
                    minY = b.min.y;
                    maxY = b.max.y;
                }
                entries.Add((t.name, posY, minY, maxY));
            }

            var top = entries
                .OrderByDescending(e => e.posY)
                .Take(20)
                .ToList();

            var bottom = entries
                .OrderBy(e => e.posY)
                .Take(20)
                .ToList();

            Debug.Log($"[RoomBuilder] Height outliers (top {top.Count} by position.y):");
            foreach (var e in top)
            {
                Debug.Log($"[RoomBuilder]  {e.name}: posY={e.posY:F3}, boundsMinY={e.minY:F3}, boundsMaxY={e.maxY:F3}");
            }

            Debug.Log($"[RoomBuilder] Height outliers (bottom {bottom.Count} by position.y):");
            foreach (var e in bottom)
            {
                Debug.Log($"[RoomBuilder]  {e.name}: posY={e.posY:F3}, boundsMinY={e.minY:F3}, boundsMaxY={e.maxY:F3}");
            }
        }

        [ContextMenu("Debug/Dump Floating Walls (>2m)")]
        public void DebugDumpFloatingWalls()
        {
            var walls = GetComponentsInChildren<Transform>(true)
                .Where(t => t != null && t.name.StartsWith("Wall_", System.StringComparison.Ordinal))
                .Where(t => t.position.y > 2f)
                .OrderByDescending(t => t.position.y)
                .Take(30)
                .ToList();

            if (walls.Count == 0)
            {
                Debug.Log("[RoomBuilder] No floating walls found (posY > 2).");
                return;
            }

            Debug.Log($"[RoomBuilder] Floating walls (posY > 2): {walls.Count}");
            foreach (var w in walls)
            {
                string path = GetHierarchyPath(w);
                var lp = w.localPosition;
                Debug.Log($"[RoomBuilder]  {path}: posY={w.position.y:F3}, localPos={lp}");
            }
        }

        private static string GetHierarchyPath(Transform t)
        {
            if (t == null) return "<null>";
            var parts = new System.Collections.Generic.List<string>();
            var cur = t;
            while (cur != null)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        [ContextMenu("Debug/Dump FloorY (Computed)")]
        public void DebugDumpComputedFloorY()
        {
            // Prefer blueprint-based computation (matches snapping logic), but fall back to hierarchy-based detection
            // because RoomBlueprint is not Unity-serializable and can be null after domain reload.
            if (blueprint != null)
            {
                EnsureNodes(blueprint);
                var spawned = new Dictionary<string, Transform>();
                var all = GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (t == null) continue;
                    if (string.IsNullOrEmpty(t.name)) continue;
                    if (!spawned.ContainsKey(t.name)) spawned[t.name] = t;
                }

                if (TryGetFloorSurfaceY(spawned, blueprint, out float y))
                {
                    Debug.Log($"[RoomBuilder] Computed floorY={y:F3} (from blueprint)");
                    return;
                }
            }

            // Fallback: find floor instances by name and compute the minimum top surface among them.
            var floorTiles = GetComponentsInChildren<Transform>(true)
                .Where(t => t != null && t.name.StartsWith("Floor_", System.StringComparison.Ordinal))
                .ToList();

            if (floorTiles.Count == 0)
            {
                Debug.LogWarning("[RoomBuilder] No Floor_* objects found; cannot compute floorY.");
                return;
            }

            bool found = false;
            float minTop = float.PositiveInfinity;
            foreach (var t in floorTiles)
            {
                if (TryGetWorldBounds(t, null, out var b))
                {
                    if (b.max.y < minTop) minTop = b.max.y;
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning("[RoomBuilder] Floor_* objects found but none had bounds; cannot compute floorY.");
                return;
            }

            Debug.Log($"[RoomBuilder] Computed floorY={minTop:F3} (from hierarchy Floor_*)");
        }

        public Dictionary<string, Transform> BuildFromBlueprint(RoomBlueprint bp)
        {
            EnsureNodes(bp);

            Debug.Log($"[{name}] BuildFromBlueprint processing {bp.nodes.Count} nodes.");

            var spawned = new Dictionary<string, Transform>();
            var defMap = BuildDefinitionMap();
            m_CachedWallSize = null;
            m_PhysicalInstanceIds.Clear();

            PostProcessDoorWallOverlaps(bp, defMap);

            // Pass 1: create objects (prefabs or placeholders) so parenting order doesn't matter.
            foreach (var node in bp.nodes)
            {
                if (node == null) continue;
                if (string.IsNullOrWhiteSpace(node.instanceID))
                {
                    Debug.LogWarning($"[{name}] Node has empty instanceID; skipping.");
                    continue;
                }

                var go = CreateNodeObject(node, defMap, out bool isPhysical);
                spawned[node.instanceID] = go.transform;
                if (isPhysical) m_PhysicalInstanceIds.Add(node.instanceID);
            }

            // Pass 2: hierarchy + transforms.
            foreach (var node in bp.nodes)
            {
                if (node == null) continue;
                if (string.IsNullOrWhiteSpace(node.instanceID)) continue;
                if (!spawned.TryGetValue(node.instanceID, out var t)) continue;

                Transform parent = transform;
                if (!string.IsNullOrEmpty(node.parentID) && spawned.TryGetValue(node.parentID, out var parentT) && parentT != null)
                {
                    // Parent under a scale-isolating anchor so container scaling doesn't blow up child transforms.
                    parent = GetContentAnchor(parentT);
                }

                var pos = new Vector3(node.position.x, node.position.y, node.position.z);
                var rot = Quaternion.Euler(node.rotation.x, node.rotation.y, node.rotation.z);

                // Blueprint positions are expressed in the parent's local space (container coordinates).
                t.SetParent(parent, false);
                t.localPosition = pos;
                t.localRotation = rot;

                if (!string.IsNullOrEmpty(node.itemID) && node.itemID == "DoorSystem")
                {
                    float logicalWidth = 1f;
                    if (defMap.TryGetValue(node.itemID, out var def) && def != null && def.logicalSize.x > 0)
                        logicalWidth = def.logicalSize.x;

                    ConfigureDoorSystem(t, GetWallSize(defMap), roomSize, logicalWidth);
                }
            }

            Debug.Log($"[{name}] Spawned {spawned.Count} objects.");
            return spawned;
        }

        private static Transform GetContentAnchor(Transform parent)
        {
            if (parent == null) return null;

            const string AnchorName = "__Content";
            var anchor = parent.Find(AnchorName);
            if (anchor == null)
            {
                var go = new GameObject(AnchorName);
                anchor = go.transform;
                anchor.SetParent(parent, false);
            }

            anchor.localPosition = Vector3.zero;
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = InverseScale(parent.localScale);
            return anchor;
        }

        private static Vector3 InverseScale(Vector3 scale)
        {
            float ix = Mathf.Abs(scale.x) > 0.00001f ? 1f / scale.x : 1f;
            float iy = Mathf.Abs(scale.y) > 0.00001f ? 1f / scale.y : 1f;
            float iz = Mathf.Abs(scale.z) > 0.00001f ? 1f / scale.z : 1f;
            return new Vector3(ix, iy, iz);
        }

        private void EnsureNodes(RoomBlueprint bp)
        {
            if (bp == null) return;
            if (bp.nodes != null && bp.nodes.Count > 0) return;
            if (bp.containers == null || bp.containers.Count == 0) return;

            bp.nodes = bp.containers[0].FlattenToPropNodes().ToList();
            Debug.Log($"[{name}] Blueprint nodes were empty; flattened container tree to {bp.nodes.Count} nodes.");
        }

        private Dictionary<string, ItemDefinition> BuildDefinitionMap()
        {
            var defMap = new Dictionary<string, ItemDefinition>();

            if (!string.IsNullOrEmpty(resourcesItemDatabasePath))
            {
                var db = Resources.Load<ItemDatabaseAsset>(resourcesItemDatabasePath);
                if (db != null && db.items != null)
                {
                    foreach (var d in db.items)
                    {
                        if (d == null) continue;
                        if (string.IsNullOrEmpty(d.itemID)) continue;
                        defMap[d.itemID] = d;
                    }
                }
            }

            if (database == null) return defMap;

            foreach (var d in database)
            {
                if (d == null) continue;
                if (string.IsNullOrEmpty(d.itemID)) continue;
                defMap[d.itemID] = d;
            }

            return defMap;
        }

        private void PostProcessDoorWallOverlaps(RoomBlueprint bp, Dictionary<string, ItemDefinition> defMap)
        {
            if (bp?.nodes == null) return;

            int wallCountBefore = bp.nodes.Count(n => n?.itemID != null && n.itemID.Contains("Wall"));
            int doorCount = bp.nodes.Count(n => n?.itemID != null && n.itemID.ToLower().Contains("door"));

            BlueprintPostProcessor.RemoveDoorWallOverlaps(bp, id =>
            {
                if (string.IsNullOrEmpty(id)) return SimpleVector3.Zero;
                if (!defMap.TryGetValue(id, out var def) || def == null) return SimpleVector3.Zero;

                var s = def.logicalSize;
                if (s.x > 0 && s.y > 0 && s.z > 0)
                    return new SimpleVector3(s.x, s.y, s.z);

                if (def.prefab != null && TryGetBounds(def.prefab, out var b))
                    return new SimpleVector3(b.size.x, b.size.y, b.size.z);

                return SimpleVector3.Zero;
            });

            int wallCountAfter = bp.nodes.Count(n => n?.itemID != null && n.itemID.Contains("Wall"));
            int removed = wallCountBefore - wallCountAfter;
            if (doorCount > 0)
            {
                Debug.Log(
                    $"[{name}] PostProcess RemoveDoorWallOverlaps: doors={doorCount}, wallsBefore={wallCountBefore}, wallsAfter={wallCountAfter}, removed={removed}.");
            }
        }

        private GameObject CreateNodeObject(PropNode node, Dictionary<string, ItemDefinition> defMap, out bool isPhysical)
        {
            isPhysical = false;

            if (node == null) return new GameObject("Node_NULL");

            if (string.IsNullOrEmpty(node.itemID))
            {
                return new GameObject(node.instanceID);
            }

            defMap.TryGetValue(node.itemID, out var def);
            if (def == null || def.prefab == null)
            {
                Debug.LogWarning($"[{name}] Missing prefab for ItemID '{node.itemID}', creating fallback primitive.");
                isPhysical = true;
                return CreateFallbackPrimitive(node, def);
            }

            var go = Instantiate(def.prefab);
            go.name = node.instanceID;
            isPhysical = true;

            if (node.itemID != "DoorSystem")
            {
                ApplySizing(go.transform, node, def);
            }

            return go;
        }

        private void ApplySizing(Transform target, PropNode node, ItemDefinition def)
        {
            if (target == null || node == null) return;

            // Prefer container-provided bounds size, but many child placements (scatter/fixed) don't populate logicalBounds.
            Vector3 desired = Vector3.zero;
            var s = node.logicalBounds.size;
            if (s.x > 0 && s.y > 0 && s.z > 0) desired = new Vector3(s.x, s.y, s.z);
            else if (def != null && def.logicalSize != Vector3.zero) desired = def.logicalSize;

            if (desired == Vector3.zero) return;

            if (TryGetBounds(target.gameObject, out var bounds))
            {
                Vector3 ratio = new Vector3(
                    bounds.size.x != 0 ? desired.x / bounds.size.x : 1f,
                    bounds.size.y != 0 ? desired.y / bounds.size.y : 1f,
                    bounds.size.z != 0 ? desired.z / bounds.size.z : 1f);

                target.localScale = Vector3.Scale(target.localScale, ratio);
            }
        }

        private static GameObject CreateFallbackPrimitive(PropNode node, ItemDefinition def)
        {
            PrimitiveType type = PrimitiveType.Cube;
            if (node != null && !string.IsNullOrEmpty(node.itemID))
            {
                string id = node.itemID.ToLowerInvariant();
                if (id.Contains("cup")) type = PrimitiveType.Sphere;
                else if (id.Contains("key")) type = PrimitiveType.Capsule;
            }

            // Best-effort sizing: prefer node bounds (from container system), then ItemDefinition logical size.
            Vector3 targetSize = Vector3.one;
            if (node != null)
            {
                var s = node.logicalBounds.size;
                if (s.x > 0 && s.y > 0 && s.z > 0) targetSize = new Vector3(s.x, s.y, s.z);
            }
            if (targetSize == Vector3.one && def != null && def.logicalSize != Vector3.zero)
            {
                targetSize = def.logicalSize;
            }

            var go = GameObject.CreatePrimitive(type);
            go.name = node?.instanceID ?? "Fallback";
            go.transform.localScale = targetSize;

            // Ensure it's solid for snapping/physics unless the consumer decides otherwise.
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;

            return go;
        }

        // ApplyContainerSizing replaced by ApplySizing (node bounds or ItemDefinition logicalSize).

        private void ApplyPhysicsSnapping(Dictionary<string, Transform> spawned, RoomBlueprint bp)
        {
            if (spawned == null || bp?.nodes == null) return;

            Physics.SyncTransforms();

            float floorY = transform.position.y;
            if (TryGetFloorSurfaceY(spawned, bp, out var fy)) floorY = fy;
            if (debugSnapping)
            {
                Debug.Log($"[RoomBuilder] Snapping start: floorY={floorY:F3} (room='{name}')");
            }

            // Snap in parent-first order so children (e.g., cups) land on already-snapped parents (e.g., tables).
            var nodeById = new Dictionary<string, PropNode>();
            var physicalChildrenByParentId = new Dictionary<string, List<string>>();
            foreach (var n in bp.nodes)
            {
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.instanceID)) continue;
                if (!nodeById.ContainsKey(n.instanceID)) nodeById[n.instanceID] = n;

                if (!string.IsNullOrEmpty(n.parentID) &&
                    m_PhysicalInstanceIds.Contains(n.instanceID) &&
                    !string.IsNullOrEmpty(n.parentID))
                {
                    if (!physicalChildrenByParentId.TryGetValue(n.parentID, out var list))
                    {
                        list = new List<string>();
                        physicalChildrenByParentId[n.parentID] = list;
                    }
                    list.Add(n.instanceID);
                }
            }

            int GetDepth(PropNode n)
            {
                int depth = 0;
                var seen = new HashSet<string>();
                string cur = n.parentID;
                while (!string.IsNullOrEmpty(cur) && seen.Add(cur) && nodeById.TryGetValue(cur, out var p))
                {
                    depth++;
                    cur = p.parentID;
                }
                return depth;
            }

            var ordered = bp.nodes
                .Where(n => n != null &&
                            !string.IsNullOrEmpty(n.instanceID) &&
                            m_PhysicalInstanceIds.Contains(n.instanceID) &&
                            (string.IsNullOrEmpty(n.itemID) || !n.itemID.Contains("Floor")))
                .OrderBy(GetDepth)
                .ToList();

            foreach (var node in ordered)
            {
                if (!spawned.TryGetValue(node.instanceID, out var child) || child == null) continue;

                // Only snap onto an IMMEDIATE physical parent (e.g., Cup -> Table). Do not walk up to logical containers:
                // doing so is prone to using inflated bounds and causes "stacking" (walls/furniture floating up in the air).
                // Structural pieces (walls/doors/windows) should never snap onto parents; they always snap to floor.
                bool isStructural =
                    node.containerKind == ContainerKind.Wall ||
                    node.containerKind == ContainerKind.Door ||
                    node.containerKind == ContainerKind.Window ||
                    node.containerKind == ContainerKind.Ceiling ||
                    (!string.IsNullOrEmpty(node.itemID) && node.itemID.Contains("Wall"));

                if (!isStructural &&
                    !string.IsNullOrEmpty(node.parentID) &&
                    m_PhysicalInstanceIds.Contains(node.parentID) &&
                    spawned.TryGetValue(node.parentID, out var parent) &&
                    parent != null)
                {
                    float before = child.position.y;
                    if (TrySnapChildToParentSurface(child, parent, excludeFromParentBounds: null))
                    {
                        if (debugSnapping && Mathf.Abs(child.position.y - before) > 0.25f)
                        {
                            Debug.Log($"[RoomBuilder] SnapToParent '{node.instanceID}' ({node.itemID}) parent='{node.parentID}' Δy={(child.position.y - before):F3}");
                        }
                        continue;
                    }
                }

                float beforeFloor = child.position.y;
                SnapToFloor(child, floorY);
                if (debugSnapping && Mathf.Abs(child.position.y - beforeFloor) > 0.25f)
                {
                    Debug.Log($"[RoomBuilder] SnapToFloor '{node.instanceID}' ({node.itemID}) floorY={floorY:F3} Δy={(child.position.y - beforeFloor):F3}");
                }
                Physics.SyncTransforms();
            }
        }

        private static bool TryResolveSupportParent(
            PropNode childNode,
            Dictionary<string, PropNode> nodeById,
            Dictionary<string, Transform> spawned,
            Dictionary<string, List<string>> physicalChildrenByParentId,
            out Transform supportParent,
            out List<Transform> excludeRootsForBounds)
        {
            supportParent = null;
            excludeRootsForBounds = null;

            if (childNode == null || string.IsNullOrEmpty(childNode.parentID)) return false;

            // Only snap onto a parent that has its own geometry (or stable bounds), not a logical grouping container.
            // Otherwise, siblings inflate the parent's bounds and cause "stacking" in mid-air (walls/furniture floating).
            var seen = new HashSet<string>();
            string curId = childNode.parentID;
            while (!string.IsNullOrEmpty(curId) && seen.Add(curId) && nodeById.TryGetValue(curId, out var parentNode))
            {
                if (!spawned.TryGetValue(curId, out var parentT) || parentT == null)
                {
                    curId = parentNode.parentID;
                    continue;
                }

                // Build an exclusion list so we can measure only the parent's own geometry (not its physical children).
                excludeRootsForBounds = null;
                if (physicalChildrenByParentId.TryGetValue(curId, out var childIds) && childIds != null && childIds.Count > 0)
                {
                    excludeRootsForBounds = new List<Transform>(childIds.Count);
                    foreach (var id in childIds)
                    {
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!spawned.TryGetValue(id, out var t) || t == null) continue;
                        excludeRootsForBounds.Add(t);
                    }
                }

                bool hasStableBounds =
                    TryGetOwnWorldBounds(parentT, out _) ||
                    (excludeRootsForBounds != null && excludeRootsForBounds.Count > 0 &&
                     TryGetWorldBoundsExcludingRoots(parentT, excludeRootsForBounds, out _));

                if (hasStableBounds)
                {
                    supportParent = parentT;
                    return true;
                }

                // If this parent has no stable geometry, walk up (it might be a logical group container).
                curId = parentNode.parentID;
            }

            supportParent = null;
            excludeRootsForBounds = null;
            return false;
        }

        private bool TrySnapChildToParentSurface(Transform child, Transform parent, List<Transform> excludeFromParentBounds)
        {
            if (child == null || parent == null) return false;

            // Prefer using only the parent's own geometry as the support surface (excluding physical children such as cups),
            // otherwise earlier-snapped siblings can inflate the surface height and cause "stacking" in mid-air.
            bool gotParent = false;
            Bounds parentBounds = default;

            if (TryGetOwnWorldBounds(parent, out parentBounds))
            {
                gotParent = true;
            }
            if (excludeFromParentBounds != null && excludeFromParentBounds.Count > 0)
            {
                gotParent = TryGetWorldBoundsExcludingRoots(parent, excludeFromParentBounds, out parentBounds);
            }
            if (!gotParent)
            {
                gotParent = TryGetWorldBounds(parent, child, out parentBounds);
            }
            if (!gotParent) return false;

            if (!TryGetWorldBounds(child, null, out var childBounds)) return false;

            float childBottomOffset = child.position.y - childBounds.min.y;
            Vector3 p = child.position;
            p.y = parentBounds.max.y + childBottomOffset;
            child.position = p;
            return true;
        }

        private static bool TryGetOwnWorldBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            var renderers = root.GetComponents<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.name.Contains("Outline")) continue;
                if (r is ParticleSystemRenderer) continue;

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (hasBounds) return true;

            var colliders = root.GetComponents<Collider>();
            foreach (var c in colliders)
            {
                if (c == null) continue;
                if (!c.enabled) continue;
                if (c.isTrigger) continue;

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetWorldBounds(Transform root, Transform excludeSubtree, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.name.Contains("Outline")) continue;
                if (r is ParticleSystemRenderer) continue;
                if (excludeSubtree != null && r.transform.IsChildOf(excludeSubtree)) continue;

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (hasBounds) return true;

            // Prefer solid colliders; if none exist, fall back to triggers (e.g., some interactables).
            var colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
            {
                if (c == null) continue;
                if (!c.enabled) continue;
                if (c.isTrigger) continue;
                if (excludeSubtree != null && c.transform.IsChildOf(excludeSubtree)) continue;

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            if (hasBounds) return true;

            foreach (var c in colliders)
            {
                if (c == null) continue;
                if (!c.enabled) continue;
                if (excludeSubtree != null && c.transform.IsChildOf(excludeSubtree)) continue;

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetWorldBoundsExcludingRoots(Transform root, List<Transform> excludeRoots, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            bool IsExcluded(Transform t)
            {
                if (t == null) return false;
                if (excludeRoots == null) return false;
                for (int i = 0; i < excludeRoots.Count; i++)
                {
                    var ex = excludeRoots[i];
                    if (ex == null) continue;
                    if (t == ex || t.IsChildOf(ex)) return true;
                }
                return false;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.name.Contains("Outline")) continue;
                if (r is ParticleSystemRenderer) continue;
                if (IsExcluded(r.transform)) continue;

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (hasBounds) return true;

            var colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
            {
                if (c == null) continue;
                if (!c.enabled) continue;
                if (c.isTrigger) continue;
                if (IsExcluded(c.transform)) continue;

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return hasBounds;
        }

        private bool TryGetFloorSurfaceY(Dictionary<string, Transform> spawned, RoomBlueprint bp, out float y)
        {
            y = transform.position.y;
            if (spawned == null || bp?.nodes == null) return false;

            bool found = false;
            float minTopY = float.PositiveInfinity;
            foreach (var n in bp.nodes)
            {
                if (n == null) continue;
                // Prefer semantic kind over string matching to avoid accidentally treating non-floor nodes
                // (or grouping containers) as the floor and floating everything.
                if (n.containerKind != ContainerKind.Floor) continue;
                if (string.IsNullOrEmpty(n.instanceID)) continue;
                if (!spawned.TryGetValue(n.instanceID, out var t) || t == null) continue;

                // Floor tiles are leaf geometry; use their world bounds directly.
                if (TryGetWorldBounds(t, null, out var b))
                {
                    if (b.max.y < minTopY) minTopY = b.max.y;
                    found = true;
                }
            }

            if (!found) return false;
            y = minTopY;
            return true;
        }

        private void SnapToFloor(Transform item, float floorY)
        {
            if (item == null) return;

            if (TryGetBounds(item.gameObject, out var b))
            {
                float bottomOffset = item.position.y - b.min.y;
                var p = item.position;
                p.y = floorY + bottomOffset;
                item.position = p;
                return;
            }

            // Fallback: raycast down.
            var allColliders = item.GetComponentsInChildren<Collider>();
            foreach (var c in allColliders)
            {
                if (c == null) continue;
                c.enabled = false;
            }

            Vector3 startPos = item.position + Vector3.up * 10f;
            if (Physics.Raycast(startPos, Vector3.down, out RaycastHit hit, 50f))
            {
                var p = item.position;
                p.y = hit.point.y;
                item.position = p;
            }

            foreach (var c in allColliders)
            {
                if (c == null) continue;
                c.enabled = true;
            }
        }

        private void ConfigureDoorSystem(Transform doorRoot, Vector3 wallSize, Vector3 roomSizeVec, float logicalWidthMultiplier)
        {
            if (doorRoot == null) return;

            float depth = wallSize.z > 0 ? wallSize.z : 0.2f;
            float height = wallSize.y > 0 ? wallSize.y : roomSizeVec.y;
            if (logicalWidthMultiplier < 1f) logicalWidthMultiplier = 1f;

            float baseWidth = (wallSize.x > 0 ? wallSize.x : 1f) * logicalWidthMultiplier;
            float frameThickness = Mathf.Clamp(baseWidth * 0.05f, 0.08f, 0.12f);
            float totalWidth = baseWidth;
            float doorWidth = Mathf.Max(totalWidth - frameThickness * 2f, totalWidth * 0.7f);

            // Prefer configuring the modular art loader when present; it provides a clear placeholder even without external art.
            var artLoader = doorRoot.GetComponent<MyGame.Adapters.Unity.DoorArtLoader>();
            if (artLoader != null)
            {
                // Normalize authored skeleton transforms so runtime sizing is deterministic (avoids layered scale/offset issues).
                if (doorRoot.localScale != Vector3.one) doorRoot.localScale = Vector3.one;

                // Enforce a stable skeleton:
                // - Frame is a fixed sibling of the hinge (should NOT be under the hinge pivot)
                // - DoorSlot is under hinge pivot
                var hingeT = FindDeepChild(doorRoot, "DoorHinge") ?? FindDeepChild(doorRoot, "Hinge") ?? FindDeepChild(doorRoot, "HingeSlot");
                if (hingeT != null && hingeT.parent != doorRoot) hingeT.SetParent(doorRoot, false);
                if (hingeT != null)
                {
                    hingeT.localRotation = Quaternion.identity;
                    hingeT.localScale = Vector3.one;
                }

                var frameSlot = FindDeepChild(doorRoot, "Frame");
                if (frameSlot != null && frameSlot.parent != doorRoot) frameSlot.SetParent(doorRoot, false);
                if (frameSlot != null)
                {
                    frameSlot.localPosition = Vector3.zero;
                    frameSlot.localRotation = Quaternion.identity;
                    frameSlot.localScale = Vector3.one;
                }

                var doorSlotT = FindDeepChild(doorRoot, "DoorSlot");
                if (doorSlotT != null && hingeT != null && doorSlotT.parent != hingeT) doorSlotT.SetParent(hingeT, false);
                if (doorSlotT != null)
                {
                    doorSlotT.localRotation = Quaternion.identity;
                    doorSlotT.localScale = Vector3.one;
                }

                artLoader.createFallbackPrimitives = true;
                artLoader.rebuildOnEnable = true;
                artLoader.alignDoorToFrame = false;
                artLoader.scaleDoorToFrame = false;

                artLoader.sideSize = new Vector3(frameThickness, height, depth);
                artLoader.topSize = new Vector3(totalWidth, frameThickness, depth);
                // Make the door leaf slightly shorter than the opening so the top frame trim is visible.
                float doorLeafH = Mathf.Max(0.5f, height - frameThickness);
                artLoader.doorSize = new Vector3(doorWidth, doorLeafH, Mathf.Max(0.05f, depth * 0.5f));

                artLoader.RebuildArt();

                // Ensure the door controller rebinds to the rebuilt leaf/pivot and starts closed.
                var ctrl = doorRoot.GetComponent<DoorController>();
                if (ctrl != null)
                {
                    ctrl.autoAlignHinge = false; // DoorArtLoader already positions the hinge pivot.
                    ctrl.hingeLocalOffset = Vector3.zero;
                    ctrl.RefreshAfterArt(resetClosed: true);
                }

                // Make sure interaction targets a non-trigger collider (raycasts often ignore triggers).
                Transform doorLeaf = FindDeepChild(doorRoot, "DoorLeaf");
                if (doorLeaf == null && doorSlotT != null && doorSlotT.childCount > 0) doorLeaf = doorSlotT.GetChild(0);
                if (doorLeaf != null)
                {
                    var leafCol = doorLeaf.GetComponent<Collider>();
                    if (leafCol != null) leafCol.isTrigger = false;

                    var interactable = doorLeaf.GetComponent<Interactable>();
                    if (interactable == null) interactable = doorLeaf.gameObject.AddComponent<Interactable>();

                    interactable.onInteract = new UnityEngine.Events.UnityEvent();
                    if (ctrl != null) interactable.onInteract.AddListener(ctrl.TryOpen);
                    interactable.outlineScript = null; // let Interactable auto-resolve/add outline/highlight
                }

                var rootCol = doorRoot.GetComponent<BoxCollider>();
                if (rootCol != null)
                {
                    rootCol.size = new Vector3(totalWidth, height, depth);
                    rootCol.center = new Vector3(0f, height / 2f, 0f);
                    // Keep root collider as an interaction volume; the door leaf collider should handle blocking.
                    rootCol.isTrigger = true;
                }

                return;
            }

            Transform left = doorRoot.Find("Frame_Left");
            Transform right = doorRoot.Find("Frame_Right");
            Transform top = doorRoot.Find("Frame_Top");
            Transform hinge = doorRoot.Find("DoorHinge");
            Transform door = hinge != null ? hinge.Find("Door") : null;

            float half = totalWidth * 0.5f;
            if (left != null)
            {
                left.localPosition = new Vector3(-half + frameThickness * 0.5f, 0, 0);
                left.localScale = new Vector3(frameThickness, height, depth);
            }
            if (right != null)
            {
                right.localPosition = new Vector3(half - frameThickness * 0.5f, 0, 0);
                right.localScale = new Vector3(frameThickness, height, depth);
            }
            if (top != null)
            {
                top.localPosition = new Vector3(0, height * 0.5f + frameThickness * 0.5f, 0);
                top.localScale = new Vector3(totalWidth, frameThickness, depth);
            }

            if (hinge != null)
            {
                hinge.localPosition = Vector3.zero;
                hinge.localScale = Vector3.one;
            }
            if (door != null)
            {
                door.localPosition = Vector3.zero;
                door.localScale = new Vector3(doorWidth, height, depth);

                var col = door.GetComponent<BoxCollider>();
                if (col != null)
                {
                    col.size = new Vector3(doorWidth, height, depth);
                    col.center = new Vector3(0, height / 2f, 0);
                    col.isTrigger = false;
                }
            }
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == childName) return t;
            }
            return null;
        }

        private Vector3 GetWallSize(Dictionary<string, ItemDefinition> defMap)
        {
            if (m_CachedWallSize.HasValue) return m_CachedWallSize.Value;

            foreach (var pair in defMap)
            {
                if (pair.Value == null) continue;
                if (string.IsNullOrEmpty(pair.Key) || !pair.Key.Contains("Wall")) continue;

                if (pair.Value.logicalSize != Vector3.zero)
                {
                    m_CachedWallSize = pair.Value.logicalSize;
                    return m_CachedWallSize.Value;
                }

                if (pair.Value.prefab != null && TryGetBounds(pair.Value.prefab, out var bounds))
                {
                    m_CachedWallSize = bounds.size;
                    return m_CachedWallSize.Value;
                }
            }

            m_CachedWallSize = Vector3.zero;
            return m_CachedWallSize.Value;
        }

        private bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (go == null) return false;

            bool hasBounds = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.name.Contains("Outline")) continue;
                if (r is ParticleSystemRenderer) continue;

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (hasBounds) return true;

            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return hasBounds;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Vector3 center = transform.position + new Vector3(0, roomSize.y / 2f, 0);
            Gizmos.DrawWireCube(center, roomSize);

            if (transform.childCount > 0) DrawRecursive(transform);
        }

        private void DrawRecursive(Transform t)
        {
            if (t == null || database == null) return;

            string id = t.name.Split('_')[0];
            var def = database.Find(d => d != null && d.itemID == id);
            if (def != null)
            {
                Gizmos.color = Color.red;
                var prev = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, def.logicalSize);
                Gizmos.matrix = prev;
                if (!id.Contains("Floor"))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(t.position, def.logicalSize.x * 1.5f * 0.5f);
                }
            }

            foreach (Transform c in t) DrawRecursive(c);
        }
    }
}
