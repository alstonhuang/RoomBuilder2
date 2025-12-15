using System.Collections.Generic;
using System.Linq; // ?? ?啣???嚗鈭 ToList() 摰?芷
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
        
        [Header("??閮剖?")]
        public string themeToBuild = "LivingRoom";
        public Vector3 roomSize = new Vector3(10, 2, 10); // 擃漲閮剔 2 瘥?摰寞???璆?

        public RoomBlueprint blueprint;

        // Flags for controlling wall generation based on neighboring rooms
        private bool m_SkipNorthWallGeneration = false; // +Z side
        private bool m_SkipSouthWallGeneration = false; // -Z side
        private bool m_SkipEastWallGeneration = false;  // +X side
        private bool m_SkipWestWallGeneration = false;  // -X side

        private Vector3? cachedWallSize;

        /// <summary>
        /// Sets flags to skip wall generation on specific sides.
        /// </summary>
        /// <param name="skipNorth">True to skip wall generation on the +Z (North) side.</param>
        /// <param name="skipSouth">True to skip wall generation on the -Z (South) side.</param>
        /// <param name="skipEast">True to skip wall generation on the +X (East) side.</param>
        /// <param name="skipWest">True to skip wall generation on the -X (West) side.</param>
        public void SetWallGenerationFlags(bool skipNorth, bool skipSouth, bool skipEast, bool skipWest)
        {
            m_SkipNorthWallGeneration = skipNorth;
            m_SkipSouthWallGeneration = skipSouth;
            m_SkipEastWallGeneration = skipEast;
            m_SkipWestWallGeneration = skipWest;
            Debug.Log($"[{name}] Wall generation flags set: North={m_SkipNorthWallGeneration}, South={m_SkipSouthWallGeneration}, East={m_SkipEastWallGeneration}, West={m_SkipWestWallGeneration}");
        }

        // ==========================================
        // 1. ?啣?皜?
        // ==========================================
        [ContextMenu("Clear All")]
        public void Clear()
        {
            // 雿輻 ToList() 頧?皜??歹??踹??刻艘?葉靽格??撠?航炊
            var children = transform.Cast<Transform>().ToList();
            foreach (var child in children)
            {
                // ?函楊頛舀芋撘?敹???DestroyImmediate
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            Debug.Log("[RoomBuilder] 撌脫??斗??????拐辣??); 
        }

        [ContextMenu("Generate Blueprint")] 
        public void GenerateBlueprint()
        {
            Debug.Log($"[{name}] Generating blueprint...");
            // If an imported package generator is present on the same GameObject, use it
            // and map its blueprint into the Core blueprint. Otherwise use the Core generator.
            var importedGen = GetComponent<ImportedCore.RoomGenerator>();
            if (importedGen != null)
            {
                var importedBp = importedGen.GenerateStackDemo();
                blueprint = MyGame.Adapters.Imported.ImportedCoreMapper.ToCore(importedBp);
            }
            else
            {
                ILogger logger = new LoggerAdapter();
                IItemLibrary library = new ItemLibraryAdapter(database, themeDatabase);
                RoomGenerator generator = new RoomGenerator(logger, library);

                var coreCenter = new SimpleVector3(0, roomSize.y / 2, 0);
                var bounds = new SimpleBounds(coreCenter, new SimpleVector3(roomSize.x, roomSize.y, roomSize.z));

                blueprint = generator.GenerateFromTheme(bounds, themeToBuild,
                                                        m_SkipNorthWallGeneration, m_SkipSouthWallGeneration,
                                                        m_SkipEastWallGeneration, m_SkipWestWallGeneration);
            }
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

            // Add a trigger collider to the room
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = roomSize;
            collider.isTrigger = true;

            // Add the RoomTrigger component
            gameObject.AddComponent<RoomTrigger>();
            Debug.Log($"[{name}] Finished building.");
        }

        [ContextMenu("Build")]
        public void Build()
        {
            GenerateBlueprint();
            BuildFromGeneratedBlueprint();
        }

        public Dictionary<string, Transform> BuildFromBlueprint(RoomBlueprint bp)
        {
            if (bp.containers != null && bp.containers.Count > 0)
            {
                bp.nodes = bp.containers[0].FlattenToPropNodes().ToList();
                Debug.Log($"[{name}] Using container tree, flattened {bp.nodes.Count} nodes.");
            }

            Debug.Log($"[{name}] BuildFromBlueprint processing {bp.nodes.Count} nodes.");
            var spawned = new Dictionary<string, Transform>();
            var defMap = new Dictionary<string, ItemDefinition>();
            cachedWallSize = null;
            foreach (var d in database) defMap[d.itemID] = d;

            int wallCountBefore = bp.nodes.FindAll(n => n.itemID != null && n.itemID.Contains("Wall")).Count;
            int doorCount = bp.nodes.FindAll(n => n.itemID != null && n.itemID.ToLower().Contains("door")).Count;

            BlueprintPostProcessor.RemoveDoorWallOverlaps(bp, id =>
            {
                if (defMap.TryGetValue(id, out var def))
                {
                    // Prefer logical size (so DoorSystem can describe its intended opening), fall back to prefab bounds.
                    var s = def.logicalSize;
                    if (s.x > 0 && s.y > 0 && s.z > 0)
                        return new SimpleVector3(s.x, s.y, s.z);

                    if (def.prefab != null && TryGetBounds(def.prefab, out var b))
                        return new SimpleVector3(b.size.x, b.size.y, b.size.z);
                }
                return SimpleVector3.Zero;
            });

            int wallCountAfter = bp.nodes.FindAll(n => n.itemID != null && n.itemID.Contains("Wall")).Count;
            int removed = wallCountBefore - wallCountAfter;
            if (doorCount > 0)
            {
                Debug.Log($"[{name}] PostProcess RemoveDoorWallOverlaps: doors={doorCount}, wallsBefore={wallCountBefore}, wallsAfter={wallCountAfter}, removed={removed}.");
            }

            int spawnedCount = 0;
            foreach (var node in bp.nodes)
            {
                if (!defMap.ContainsKey(node.itemID))
                {
                    Debug.LogWarning($"[{name}] ItemID '{node.itemID}' not found in database. Skipping.");
                    continue;
                }
                GameObject prefab = defMap[node.itemID].prefab;
                if (prefab == null)
                {
                    Debug.LogWarning($"[{name}] Prefab for ItemID '{node.itemID}' is null. Skipping.");
                    continue;
                }

                GameObject go = Instantiate(prefab);
                go.name = node.instanceID;
                
                Vector3 pos = new Vector3(node.position.x, node.position.y, node.position.z);
                Quaternion rot = Quaternion.Euler(node.rotation.x, node.rotation.y, node.rotation.z);

                ApplyContainerSizing(go.transform, node);

                // DoorSystem runtime adjust to avoid prefab scale layering issues
                if (node.itemID.Equals("DoorSystem"))
                {
                    float logicalWidth = 1f;
                    if (defMap.TryGetValue(node.itemID, out var def) && def.logicalSize.x > 0)
                        logicalWidth = def.logicalSize.x;
                    ConfigureDoorSystem(go.transform, GetWallSize(defMap), roomSize, logicalWidth);
                }

                if (!string.IsNullOrEmpty(node.parentID) && spawned.ContainsKey(node.parentID))
                {
                    go.transform.SetParent(spawned[node.parentID]);
                    go.transform.localPosition = pos;
                    go.transform.localRotation = rot;
                }
                else
                {
                    go.transform.SetParent(transform);
                    go.transform.position = pos + transform.position;
                    go.transform.localRotation = rot;
                }

                // Align/scale door to match wall height/thickness and sit on floor.
                if (node.itemID.ToLower().Contains("door"))
                {
                    AlignDoorToFloor(go.transform, GetWallSize(defMap));
                }

                spawned[node.instanceID] = go.transform;
                spawnedCount++;
            }
            Debug.Log($"[{name}] Spawned {spawnedCount} objects.");
            return spawned;
        }

        private void ApplyContainerSizing(Transform target, Core.PropNode node)
        {
            if (target == null) return;

            bool shouldFit = node.containerKind == Core.ContainerKind.Wall
                             || node.containerKind == Core.ContainerKind.Floor
                             || node.containerKind == Core.ContainerKind.Ceiling
                             || node.containerKind == Core.ContainerKind.Door
                             || node.containerKind == Core.ContainerKind.Window;
            var size = node.logicalBounds.size;
            if (!shouldFit || size.x <= 0 || size.y <= 0 || size.z <= 0) return;

            if (TryGetBounds(target.gameObject, out var bounds))
            {
                Vector3 ratio = new Vector3(
                    bounds.size.x != 0 ? size.x / bounds.size.x : 1f,
                    bounds.size.y != 0 ? size.y / bounds.size.y : 1f,
                    bounds.size.z != 0 ? size.z / bounds.size.z : 1f);

                target.localScale = Vector3.Scale(target.localScale, ratio);
            }
        }

        private void ApplyPhysicsSnapping(Dictionary<string, Transform> spawned, RoomBlueprint bp)
        {
            Physics.SyncTransforms();
            foreach (var node in bp.nodes)
            {
                // ?唳銝?閬??(摰歇蝬 StructureGenerator 蝞末雿蔭鈭?
                if (node.itemID.Contains("Floor")) continue;

                if (spawned.TryGetValue(node.instanceID, out var child))
                {
                    // ?斗?臬??拐辣
                    if (!string.IsNullOrEmpty(node.parentID) && spawned.TryGetValue(node.parentID, out var parent))
                    {
                        // ?臬?鞎潭?摮?
                        SnapChildToParentSurface(child, parent);
                    }
                    else
                    {
                        // 獢?鞎澆??(憒? StructureGenerator 蝞?皞??嗅祕?郊?臭???
                        SnapToGround(child);
                    }
                }
            }
        }

        private void SnapChildToParentSurface(Transform child, Transform parent)
        {
            float parentTop = parent.position.y;
            var pCol = parent.GetComponentInChildren<Collider>();
            if (pCol) parentTop = pCol.bounds.max.y;

            float childBottom = 0;
            var cCol = child.GetComponentInChildren<Collider>();
            if (cCol) childBottom = child.position.y - cCol.bounds.min.y;

            Vector3 p = child.position;
            p.y = parentTop + childBottom;
            child.position = p;
        }

        private void SnapToGround(Transform item)
        {
            float bottomOffset = 0;
            // ??????拐辣??Collider (??芸楛???Ｙ??臬?)
            var allColliders = item.GetComponentsInChildren<Collider>();
            
            if (allColliders.Length > 0)
            {
                // 閮??雿? (?喳???
                float minY = float.MaxValue;
                foreach (var c in allColliders)
                {
                    if (c.bounds.min.y < minY) minY = c.bounds.min.y;
                }
                bottomOffset = item.position.y - minY;
            }

            // ?? ?甇仿?嚗??????Collider
            // ?見撠??????啗撌梧?撠瘚桀蝛箔葉
            foreach (var c in allColliders) c.enabled = false;

            // ?祇?皞??澆?
            Vector3 startPos = item.position + Vector3.up * 10f; 
            RaycastHit hit;
            
            // ?澆?撠? (?ㄐ?臭誑??LayerMask 蝣箔??芣??唳嚗???????芸楛?镼?
            if (Physics.Raycast(startPos, Vector3.down, out hit, 50f))
            {
                // ?芣?????航撌?(?撌脩???鈭???靽) 銝??Ｗ???蝘餃?
                item.position = hit.point + Vector3.up * bottomOffset;
            }

            // ???Ｗ儔甇仿?嚗??圈?????Collider
            foreach (var c in allColliders) c.enabled = true;
        }
        
        private void AlignDoorToFloor(Transform door, Vector3 wallSize)
        {
            if (door == null) return;
            
            // Keep door's prefab scale; only ensure depth does not exceed wall thickness.
            if (wallSize != Vector3.zero && wallSize.z > 0)
            {
                Vector3 s = door.localScale;
                if (s.z > wallSize.z) s.z = wallSize.z;
                door.localScale = s;
            }

            if (!TryGetBounds(door.gameObject, out var bounds)) return;

            // Raycast to the floor and place the door so its bounds.min.y aligns to the hit point
            // (after scaling).
            Vector3 rayStart = door.position + Vector3.up * 5f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f))
            {
                float bottomOffset = bounds.min.y - door.position.y; // how far the current min is below the pivot
                Vector3 p = door.position;
                p.y = hit.point.y - bottomOffset;
                door.position = p;
            }
        }

        private void ConfigureDoorSystem(Transform doorRoot, Vector3 wallSize, Vector3 roomSizeVec, float logicalWidthMultiplier = 1f)
        {
            if (doorRoot == null) return;

            Debug.Log($"[RoomBuilder] ConfigureDoorSystem wallSize={wallSize}");

            float depth = wallSize.z > 0 ? wallSize.z : 0.2f;
            float height = wallSize.y > 0 ? wallSize.y : roomSizeVec.y;
            if (logicalWidthMultiplier < 1f) logicalWidthMultiplier = 1f;
            float baseWidth = (wallSize.x > 0 ? wallSize.x : 4.0f) * logicalWidthMultiplier; // prefer actual wall width, scaled by logical tiles
            float frameThickness = Mathf.Clamp(baseWidth * 0.05f, 0.08f, 0.12f);
            float totalWidth = baseWidth; // the opening to fill
            float doorWidth = Mathf.Max(totalWidth - frameThickness * 2f, totalWidth * 0.7f);
            Debug.Log($"[RoomBuilder] ConfigureDoorSystem baseWidth={baseWidth:F2}, frameThickness={frameThickness:F2}, doorWidth={doorWidth:F2}, depth={depth:F2}, height={height:F2}");

            doorRoot.localScale = Vector3.one;
            doorRoot.localRotation = Quaternion.identity;

            Transform left = doorRoot.Find("Frame_Left");
            Transform right = doorRoot.Find("Frame_Right");
            Transform top = doorRoot.Find("Frame_Top");
            Transform hinge = doorRoot.Find("DoorHinge");
            Transform door = hinge != null ? hinge.Find("Door") : null;

            // Position frames
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
                // Keep hinge at the prefab pivot to avoid drifting; frames define the visible edges.
                hinge.localPosition = Vector3.zero;
                hinge.localScale = Vector3.one;
            }
            if (door != null)
            {
                // Center the leaf between frames; hinge stays at pivot (0)
                door.localPosition = new Vector3(0f, 0f, 0f);
                door.localScale = new Vector3(doorWidth, height, depth);

                var col = door.GetComponent<BoxCollider>();
                if (col != null)
                {
                    col.size = new Vector3(doorWidth, height, depth);
                    col.center = new Vector3(0, height / 2f, 0);
                    col.isTrigger = false;
                }
            }

            string lp = left != null ? left.localPosition.ToString() : "null";
            string rp = right != null ? right.localPosition.ToString() : "null";
            string tp = top != null ? top.localPosition.ToString() : "null";
            string hp = hinge != null ? hinge.localPosition.ToString() : "null";
            string dp = door != null ? door.localPosition.ToString() : "null";
            Debug.Log($"[RoomBuilder] DoorSystem parts: leftPos={lp}, rightPos={rp}, topPos={tp}, hingePos={hp}, doorPos={dp}");
        }

        private Vector3 GetWallSize(Dictionary<string, ItemDefinition> defMap)
        {
            if (cachedWallSize.HasValue) return cachedWallSize.Value;

            foreach (var pair in defMap)
            {
                if (!pair.Key.Contains("Wall") || pair.Value == null || pair.Value.prefab == null) continue;

                if (TryGetBounds(pair.Value.prefab, out var bounds))
                {
                    cachedWallSize = bounds.size;
                    return cachedWallSize.Value;
                }
            }

            cachedWallSize = Vector3.zero;
            return cachedWallSize.Value;
        }

        private bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (go == null) return false;

            bool hasBounds = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Outline") || r is ParticleSystemRenderer) continue;
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (hasBounds) return true;

            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                if (!hasBounds) { bounds = c.bounds; hasBounds = true; }
                else bounds.Encapsulate(c.bounds);
            }

            return hasBounds;
        }

        void OnDrawGizmos()
        {
            // ?怠暺獢?隞?”?輸?蝭?
            Gizmos.color = Color.yellow;
            // ?ㄐ閬?敺株?蝞?銝?Gizmo ?葉敹???? transform.position ?虜?刻摨?
            // ??DrawWireCube ?閬葉敹?
            Vector3 center = transform.position + new Vector3(0, roomSize.y / 2, 0);
            Gizmos.DrawWireCube(center, roomSize);

            if (transform.childCount > 0) DrawRecursive(transform);
        }

        void DrawRecursive(Transform t)
        {
            string id = t.name.Split('_')[0];
            var def = database.Find(d => d.itemID == id);
            if (def)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(t.position, def.logicalSize);
                // ?芣?摰嗅???????唳銝??
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
