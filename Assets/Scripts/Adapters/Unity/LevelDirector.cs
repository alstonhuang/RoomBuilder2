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

                    // Determine wall skipping flags
                    // Keep the east wall so a door can be carved out; only skip the west wall to avoid double thickness.
                    bool skipEast = false;
                    bool skipWest = (i > 0); // Let the room on the left provide the shared wall

                    // Set wall generation flags before generating the blueprint (North & South always generated for now)
                    roomBuilder.SetWallGenerationFlags(false, false, skipEast, skipWest);
                    
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
            if (m_RoomBuilders.Count < 2)
            {
                Debug.Log("Not enough rooms to connect with doors.");
                return;
            }

            for (int i = 0; i < m_RoomBuilders.Count - 1; i++)
            {
                Debug.Log($"Adding door between Room {i} and Room {i + 1}...");
                RoomBuilder roomA = m_RoomBuilders[i];
                RoomBuilder roomB = m_RoomBuilders[i + 1]; // Room B is to the East of Room A

                // Add the DoorSystem to Room A's blueprint at its East side
                float doorPosX = roomSize.x / 2; // Position on the +X side of Room A

                // Calculate available range for door placement along the Z-axis of the wall
                // We need to ensure the door is not placed too close to the corners.
                // Let's assume a buffer of 1 unit from each corner.
                float minZ = -roomSize.z / 2 + 1.0f; // 1 unit from corner
                float maxZ = roomSize.z / 2 - 1.0f;  // 1 unit from corner
                
                // If room is too small, just center it
                if (minZ > maxZ) 
                {
                    minZ = 0;
                    maxZ = 0;
                }
                float randomZ = UnityEngine.Random.Range(minZ, maxZ);

                roomA.blueprint.nodes.Add(new PropNode
                {
                    instanceID = $"Door_{roomA.name}_{roomB.name}",
                    itemID = "DoorSystem",
                    position = new SimpleVector3(doorPosX, 0, randomZ), // Random Z position
                    rotation = new SimpleVector3(0, 90, 0) // Rotate to face along the X-axis
                });
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
