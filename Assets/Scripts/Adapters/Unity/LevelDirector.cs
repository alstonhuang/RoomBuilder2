using System.Collections.Generic;
using UnityEngine;
using MyGame.Core;

namespace MyGame.Adapters.Unity
{
    public class LevelDirector : MonoBehaviour
    {
        [Header("Level Settings")]
        public int roomsToClear = 3;
        public Vector3 roomSize = new Vector3(10, 3, 10);
        public string themeToBuild = "LivingRoom";

        [Header("Dependencies")]
        public GameObject roomBuilderPrefab; // Prefab of the RoomBuilder
        public GameObject playerPrefab;      // Prefab of the player (optional, will instantiate if none exists)

        private List<RoomBuilder> m_RoomBuilders = new List<RoomBuilder>();
        private int m_RoomsCleared = 0;

        void Start()
        {
            // Ensure we begin with a single listener even before generation.
            EnsureSingleAudioListener();
        }

        [ContextMenu("Generate Level")]
        public void GenerateLevel()
        {
            Debug.Log("--- Starting Level Generation ---");
            ClearLevel();

            // 1. Create builders and generate blueprints
            Debug.Log("Step 1: Creating builders and generating blueprints...");
            for (int i = 0; i < roomsToClear; i++)
            {
                GameObject roomBuilderGO = Instantiate(roomBuilderPrefab, transform);
                roomBuilderGO.name = $"Room_{i}";
                roomBuilderGO.transform.position = new Vector3(i * roomSize.x, 0, 0);

                RoomBuilder roomBuilder = roomBuilderGO.GetComponent<RoomBuilder>();
                if (roomBuilder != null)
                {
                    roomBuilder.roomSize = this.roomSize;
                    roomBuilder.themeToBuild = this.themeToBuild;

                    // 為避免相鄰牆重疊：左邊房保留西牆，右邊房略過西牆；東牆保留以便開門
                    bool skipNorth = false;
                    bool skipSouth = false;
                    bool skipEast = false;          // 保留東牆開門
                    bool skipWest = (i > 0);        // 除了最左邊，其餘房間略過西牆
                    roomBuilder.SetWallGenerationFlags(skipNorth, skipSouth, skipEast, skipWest);
                    
                    roomBuilder.GenerateBlueprint();
                    m_RoomBuilders.Add(roomBuilder);
                }
                else
                {
                    Debug.LogError("RoomBuilder prefab does not have a RoomBuilder component. Stopping generation.");
                    return;
                }
            }
            Debug.Log($"Successfully created {m_RoomBuilders.Count} RoomBuilder instances.");

            // 2. Spawn player if not present
            SpawnPlayerIfMissing();
            EnsureSingleAudioListener();

            // 2. Add doors between rooms
            Debug.Log("Step 3: Adding doors between rooms...");
            AddDoorsBetweenRooms(); // New method
            Debug.Log("Finished adding doors.");


            // 3. Build rooms from modified blueprints
            Debug.Log("Step 4: Building rooms from blueprints...");
            foreach (var builder in m_RoomBuilders)
            {
                builder.BuildFromGeneratedBlueprint();
                // 4. Set the level director on the room trigger
                var roomTrigger = builder.GetComponent<RoomTrigger>();
                if (roomTrigger != null)
                {
                    roomTrigger.levelDirector = this;
                }
            }

            Debug.Log($"--- Level Generation Complete ---");
        }

        private void SpawnPlayerIfMissing()
        {
            // If a player already exists, skip spawning.
            if (FindAnyObjectByType<PlayerMovement>() != null) return;

            if (playerPrefab == null)
            {
                Debug.LogWarning("No playerPrefab assigned; skipping player spawn. Add a prefab to LevelDirector to auto-spawn a player.");
                return;
            }

            // Spawn near the first room; place slightly above the floor.
            Vector3 spawnPos = transform.position + new Vector3(0, 1.5f, 0);
            if (m_RoomBuilders.Count > 0)
            {
                spawnPos = m_RoomBuilders[0].transform.position + new Vector3(0, 1.5f, 0);
            }

            Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            Debug.Log($"Spawned player at {spawnPos}");
        }

        private void EnsureSingleAudioListener()
        {
            AudioListener[] listeners;
#if UNITY_2023_1_OR_NEWER
            listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
#else
            listeners = FindObjectsOfType<AudioListener>();
#endif
            if (listeners == null || listeners.Length == 0) return;

            // Prefer the listener under the current player (if any), otherwise keep the first.
            AudioListener preferred = null;
#if UNITY_2023_1_OR_NEWER
            var player = FindFirstObjectByType<PlayerMovement>();
#else
            var player = FindObjectOfType<PlayerMovement>();
#endif
            if (player != null)
            {
                preferred = player.GetComponentInChildren<AudioListener>(true);
            }
            if (preferred == null) preferred = listeners[0];

            foreach (var listener in listeners)
            {
                listener.enabled = (listener == preferred);
            }
        }

        private void AddDoorsBetweenRooms()
        {
            if (m_RoomBuilders.Count == 0)
            {
                Debug.Log("No rooms to connect.");
                return;
            }

            // Determine door footprint so we can carve enough wall tiles.
            Vector3 doorSize = GetDoorFootprintSize();
            float halfX = Mathf.Max(0.5f, (doorSize.x * 0.5f) + 0.05f);
            float halfZ = Mathf.Max(0.5f, (doorSize.z * 0.5f) + 0.05f);

            // Special case: only one room -> place a single door on its east wall.
            if (m_RoomBuilders.Count == 1)
            {
                var room = m_RoomBuilders[0];
                float wallThickness = GetWallThickness();
                float doorPosX = (roomSize.x / 2) + (wallThickness * 0.5f);
                float doorPosZ = 0f;

                room.blueprint.nodes.Add(new PropNode
                {
                    instanceID = $"Door_{room.name}_Exit",
                    itemID = "DoorSystem",
                    position = new SimpleVector3(doorPosX, 0, doorPosZ),
                    rotation = new SimpleVector3(0, 90, 0)
                });

                int removedWall = RemoveWallSegments(room.blueprint, doorPosX, doorPosZ, halfX, halfZ, 90f);
                Debug.Log($"Single-room door added to {room.name}, wall removed={removedWall}");

                // Drop a key in the single room so the player can open the door.
                room.blueprint.nodes.Add(new PropNode
                {
                    instanceID = $"Key_{room.name}_Single",
                    itemID = "Key",
                    position = new SimpleVector3(0, 1, 0),
                    rotation = new SimpleVector3(0, 0, 0)
                });
                return;
            }

            for (int i = 0; i < m_RoomBuilders.Count - 1; i++)
            {
                Debug.Log($"Adding door between Room {i} and Room {i + 1}...");
                RoomBuilder roomA = m_RoomBuilders[i];
                RoomBuilder roomB = m_RoomBuilders[i + 1]; // Room B is to the East of Room A

                // Place the door centered on the shared wall (+X of A)
                float wallThickness = GetWallThickness();
                float doorPosX = (roomSize.x / 2) + (wallThickness * 0.5f); // Align with wall center (walls are pushed outward by half thickness)
                float doorPosZ = 0f; // centered to avoid hitting corners

                roomA.blueprint.nodes.Add(new PropNode
                {
                    instanceID = $"Door_{roomA.name}_{roomB.name}",
                    itemID = "DoorSystem",
                    position = new SimpleVector3(doorPosX, 0, doorPosZ), // centered Z
                    rotation = new SimpleVector3(0, 90, 0) // Rotate to face along the X-axis
                });
                // Remove one wall segment on both sides where the door sits
                int removedA = RemoveWallSegments(roomA.blueprint, doorPosX, doorPosZ, halfX, halfZ, 90f);
                int removedB = RemoveWallSegments(roomB.blueprint, -doorPosX, doorPosZ, halfX, halfZ, 90f);
                Debug.Log($"Door carve result A={removedA}, B={removedB} at z={doorPosZ}");

                // Add a Key to the room where the door is placed (Room A)
                roomA.blueprint.nodes.Add(new PropNode
                {
                    instanceID = $"Key_{roomA.name}_{roomB.name}", // Unique ID for key
                    itemID = "Key",
                    position = new SimpleVector3(0, 1, 0), // Default position within the room
                    rotation = new SimpleVector3(0, 0, 0)
                });
                Debug.Log($"Added Key node to {roomA.name}.");
            }
        }

        private int RemoveWallSegments(RoomBlueprint bp, float targetX, float targetZ, float halfWidth, float halfDepth, float rotationYDeg)
        {
            int removed = 0;
            bool swapXZ = Mathf.Abs(Mathf.DeltaAngle(rotationYDeg, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(rotationYDeg, 270f)) < 1f;
            float halfX = swapXZ ? halfDepth : halfWidth;
            float halfZ = swapXZ ? halfWidth : halfDepth;

            for (int i = bp.nodes.Count - 1; i >= 0; i--)
            {
                var n = bp.nodes[i];
                if (n.itemID == null || !n.itemID.Contains("Wall")) continue;
                if (Mathf.Abs(n.position.x - targetX) > halfX) continue;
                if (Mathf.Abs(n.position.z - targetZ) > halfZ) continue;
                bp.nodes.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        private Vector3 GetDoorFootprintSize()
        {
            if (m_RoomBuilders.Count == 0) return Vector3.one;
            var db = m_RoomBuilders[0].database;
            if (db == null) return Vector3.one;

            ItemDefinition def = db.Find(d => d != null && (d.itemID == "DoorSystem" || d.itemID.ToLower().Contains("door")));
            if (def == null) return Vector3.one;

            // Prefer logical size so carving stays correct even when art/prefab bounds are missing or inconsistent.
            if (def.logicalSize != Vector3.zero) return def.logicalSize;

            if (def.prefab != null && TryGetPrefabBounds(def.prefab, out var b)) return b.size;
            return Vector3.one;
        }

        private float GetWallThickness()
        {
            if (m_RoomBuilders.Count == 0) return 0f;
            var db = m_RoomBuilders[0].database;
            if (db == null) return 0f;

            var def = db.Find(d => d != null && d.itemID == "Wall");
            if (def == null) return 0f;

            if (def.logicalSize.z > 0) return def.logicalSize.z;
            if (def.prefab != null && TryGetPrefabBounds(def.prefab, out var b)) return b.size.z;
            return 0f;
        }

        private bool TryGetPrefabBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (prefab == null) return false;

            bool hasBounds = false;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Outline") || r is ParticleSystemRenderer) continue;
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (hasBounds) return true;

            foreach (var c in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (!hasBounds) { bounds = c.bounds; hasBounds = true; }
                else bounds.Encapsulate(c.bounds);
            }
            return hasBounds;
        }


        [ContextMenu("Clear Level")]
        public void ClearLevel()
        {
            // Destroy all child objects (the rooms)
            var children = new List<GameObject>();
            foreach (Transform child in transform)
            {
                children.Add(child.gameObject);
            }

            foreach (var child in children)
            {
                if (Application.isEditor && !Application.isPlaying)
                {
                    DestroyImmediate(child);
                }
                else
                {
                    Destroy(child);
                }
            }
            
            m_RoomBuilders.Clear();
            m_RoomsCleared = 0;
            Debug.Log("Level cleared.");
        }

        public void OnRoomEntered(GameObject roomGO)
        {
            var roomBuilder = roomGO.GetComponent<RoomBuilder>();
            if (roomBuilder != null)
            {
                int roomIndex = m_RoomBuilders.IndexOf(roomBuilder);
                if (roomIndex != -1)
                {
                    m_RoomsCleared = roomIndex;
                    Debug.Log("Entered room " + roomIndex);

                    if (m_RoomsCleared >= roomsToClear - 1)
                    {
                        Debug.Log("Congratulations! You've reached the final room and escaped!");
                        // Unlock final exit logic here.
                    }
                }
            }
        }
    }
}
