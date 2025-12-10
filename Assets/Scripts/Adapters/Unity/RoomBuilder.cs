using System.Collections.Generic;
using System.Linq; // 👈 新增這行，為了用 ToList() 安全刪除
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
        
        [Header("生成設定")]
        public string themeToBuild = "LivingRoom";
        public Vector3 roomSize = new Vector3(10, 2, 10); // 高度設為 2 比較容易看清楚

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
        // 1. 新增清除功能
        // ==========================================
        [ContextMenu("Clear All")]
        public void Clear()
        {
            // 使用 ToList() 轉成清單再刪除，避免在迴圈中修改集合導致錯誤
            var children = transform.Cast<Transform>().ToList();
            foreach (var child in children)
            {
                // 在編輯模式下必須用 DestroyImmediate
                DestroyImmediate(child.gameObject);
            }
            Debug.Log("[RoomBuilder] 已清除所有生成的物件。");
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
                    if (def.prefab != null && TryGetBounds(def.prefab, out var b))
                        return new SimpleVector3(b.size.x, b.size.y, b.size.z);

                    var s = def.logicalSize;
                    return new SimpleVector3(s.x, s.y, s.z);
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

                // Auto-fix door pieces to match wall dimensions (height/thickness) even if the prefab was authored differently.
                if (node.itemID.ToLower().Contains("door"))
                {
                    AutoSizeDoor(go.transform, defMap, node.rotation);
                }

                spawned[node.instanceID] = go.transform;
                spawnedCount++;
            }
            Debug.Log($"[{name}] Spawned {spawnedCount} objects.");
            return spawned;
        }

        private void ApplyPhysicsSnapping(Dictionary<string, Transform> spawned, RoomBlueprint bp)
        {
            Physics.SyncTransforms();
            foreach (var node in bp.nodes)
            {
                // 地板不需要落地 (它已經在 StructureGenerator 算好位置了)
                if (node.itemID.Contains("Floor")) continue;

                if (spawned.TryGetValue(node.instanceID, out var child))
                {
                    // 判斷是否有父物件
                    if (!string.IsNullOrEmpty(node.parentID) && spawned.TryGetValue(node.parentID, out var parent))
                    {
                        // 杯子貼桌子
                        SnapChildToParentSurface(child, parent);
                    }
                    else
                    {
                        // 桌子貼地板 (如果 StructureGenerator 算得準，其實這步是保險)
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
            // 取得所有子物件的 Collider (包含自己和上面的杯子)
            var allColliders = item.GetComponentsInChildren<Collider>();
            
            if (allColliders.Length > 0)
            {
                // 計算最低點 (腳底板)
                float minY = float.MaxValue;
                foreach (var c in allColliders)
                {
                    if (c.bounds.min.y < minY) minY = c.bounds.min.y;
                }
                bottomOffset = item.position.y - minY;
            }

            // 🛑 關鍵步驟：暫時關閉所有 Collider
            // 這樣射線才不會打到自己，導致浮在空中
            foreach (var c in allColliders) c.enabled = false;

            // 抬高準備發射
            Vector3 startPos = item.position + Vector3.up * 10f; 
            RaycastHit hit;
            
            // 發射射線 (這裡可以加 LayerMask 確保只打地板，目前先打所有非自己的東西)
            if (Physics.Raycast(startPos, Vector3.down, out hit, 50f))
            {
                // 只有打到的不是自己 (雖然已經關閉了，雙重保險) 且距離合理才移動
                item.position = hit.point + Vector3.up * bottomOffset;
            }

            // ✅ 恢復步驟：重新開啟所有 Collider
            foreach (var c in allColliders) c.enabled = true;
        }
        
        private void AutoSizeDoor(Transform door, Dictionary<string, ItemDefinition> defMap, SimpleVector3 nodeRotation)
        {
            if (door == null) return;

            Vector3 wallSize = GetWallSize(defMap); // This is size of a single wall segment
            
            // Prefer wall height but never exceed the configured room height.
            float targetHeight = wallSize.y > 0 ? Mathf.Min(wallSize.y, roomSize.y) : roomSize.y;
            float targetWidth;
            float targetDepth;

            // Assuming a standard door width, e.g., 1 unit for now.
            // Its thickness should match the wall thickness.
            // If the door is rotated 90/270 degrees (East/West wall), its local X (width) should be the door width,
            // and its local Z (depth) should be the wall thickness (wallSize.x).
            // If the door is rotated 0/180 degrees (North/South wall), its local X (width) should be the door width,
            // and its local Z (depth) should be the wall thickness (wallSize.z).

            float standardDoorWidth = 1.0f; // A reasonable default for a door opening

            float yRotation = nodeRotation.y;
            if (Mathf.Approximately(yRotation, 90f) || Mathf.Approximately(yRotation, 270f)) // East/West wall
            {
                targetWidth = standardDoorWidth;
                targetDepth = wallSize.x; // Use wall thickness for door depth
            }
            else // North/South wall (or default 0/180)
            {
                targetWidth = standardDoorWidth;
                targetDepth = wallSize.z; // Use wall thickness for door depth
            }

            if (!TryGetBounds(door.gameObject, out var doorBounds)) return;

            const float minSize = 0.001f;
            Vector3 current = doorBounds.size;
            if (current.x < minSize || current.y < minSize || current.z < minSize) return;

            Vector3 scaleAdjust = new Vector3(
                targetWidth / current.x,
                targetHeight / current.y,
                targetDepth / current.z
            );

            door.localScale = Vector3.Scale(door.localScale, scaleAdjust);

            // After scaling, align the bottom of the door to the room's floor so it doesn't float or tower.
            if (TryGetBounds(door.gameObject, out var scaledBounds))
            {
                float roomBottom = transform.position.y - (roomSize.y / 2f);
                float deltaY = roomBottom - scaledBounds.min.y;
                door.position += Vector3.up * deltaY;
            }
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
            // 畫出黃色框框代表房間範圍
            Gizmos.color = Color.yellow;
            // 這裡要稍微計算一下 Gizmo 的中心，因為我們的 transform.position 通常在腳底
            // 而 DrawWireCube 需要中心點
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
                // 只有家具才畫散佈圈，地板不用畫
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
