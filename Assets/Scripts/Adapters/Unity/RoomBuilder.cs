using System.Collections.Generic;
using UnityEngine;
using MyGame.Core;
using ILogger = MyGame.Core.ILogger;

namespace MyGame.Adapters.Unity
{
    public class RoomBuilder : MonoBehaviour
    {
        public List<ItemDefinition> database;
        public List<RoomTheme> themeDatabase;
        public string themeToBuild = "LivingRoom";
        public Vector3 roomSize = new Vector3(10, 1, 10);

        [ContextMenu("Build")]
        public void Build()
        {
            foreach (Transform child in transform) DestroyImmediate(child.gameObject);

            ILogger logger = new LoggerAdapter();
            IItemLibrary library = new ItemLibraryAdapter(database, themeDatabase);
            RoomGenerator generator = new RoomGenerator(logger, library);

            var bounds = new SimpleBounds(new SimpleVector3(0,0,0), new SimpleVector3(roomSize.x, roomSize.y, roomSize.z));
            RoomBlueprint blueprint = generator.GenerateFromTheme(bounds, themeToBuild);

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
                
                Vector3 pos = new Vector3(node.position.x, 0, node.position.z);
                if (!string.IsNullOrEmpty(node.parentID) && spawned.ContainsKey(node.parentID))
                {
                    go.transform.SetParent(spawned[node.parentID]);
                    go.transform.localPosition = pos;
                }
                else
                {
                    go.transform.SetParent(transform);
                    go.transform.position = pos + transform.position;
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
                if (string.IsNullOrEmpty(node.parentID)) continue;
                if (spawned.TryGetValue(node.instanceID, out var child) && spawned.TryGetValue(node.parentID, out var parent))
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
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, roomSize);
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
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(t.position, def.logicalSize.x * 1.5f * 0.5f);
            }
            foreach (Transform c in t) DrawRecursive(c);
        }
    }
}