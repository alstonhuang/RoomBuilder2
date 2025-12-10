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

        [ContextMenu("Build")]
        public void Build()
        {
            // 1. 生成前先清除舊的
            Clear();

            // If an imported package generator is present on the same GameObject, use it
            // and map its blueprint into the Core blueprint. Otherwise use the Core generator.
            RoomBlueprint blueprint;
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

                // ==========================================
                // 2. 修正浮空問題
                // ==========================================
                // 舊寫法：new SimpleVector3(0, 0, 0) -> 導致房間一半在地下一半在地上
                // 新寫法：把中心點往上提 "高度的一半" -> 這樣房間底部就在 0
                var coreCenter = new SimpleVector3(0, roomSize.y / 2, 0);

                var bounds = new SimpleBounds(coreCenter, new SimpleVector3(roomSize.x, roomSize.y, roomSize.z));

                blueprint = generator.GenerateFromTheme(bounds, themeToBuild);
            }

            var spawnedMap = BuildFromBlueprint(blueprint);
            ApplyPhysicsSnapping(spawnedMap, blueprint);
        }

        private Dictionary<string, Transform> BuildFromBlueprint(RoomBlueprint bp)
        {
            var spawned = new Dictionary<string, Transform>();
            var defMap = new Dictionary<string, ItemDefinition>();
            foreach (var d in database) defMap[d.itemID] = d;

            foreach (var node in bp.nodes)
            {
                if (!defMap.ContainsKey(node.itemID)) continue;
                GameObject go = Instantiate(defMap[node.itemID].prefab);
                go.name = node.instanceID;
                
                // 注意：這裡的 node.position 已經包含了正確的 Y 軸資訊 (由 StructureGenerator 計算)
                // 或者是 0 (由家具生成器計算)
                Vector3 pos = new Vector3(node.position.x, node.position.y, node.position.z);

                // 👇👇👇【補上這一段】👇👇👇
                Quaternion rot = Quaternion.Euler(node.rotation.x, node.rotation.y, node.rotation.z);
                // 👆👆👆

                if (!string.IsNullOrEmpty(node.parentID) && spawned.ContainsKey(node.parentID))
                {
                    go.transform.SetParent(spawned[node.parentID]);
                    go.transform.localPosition = pos;
                    go.transform.localRotation = rot; // 👈 這裡也要設
                }
                else
                {
                    go.transform.SetParent(transform);
                    // 加上 RoomBuilder 本身的位置，這樣你可以拖動 RoomBuilder，房間會跟著動
                    go.transform.position = pos + transform.position;
                    go.transform.localRotation = rot; // 👈 這裡也要設
                }
                spawned[node.instanceID] = go.transform;
            }
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