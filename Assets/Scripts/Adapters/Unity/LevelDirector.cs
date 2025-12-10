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

        private List<RoomBuilder> m_RoomBuilders = new List<RoomBuilder>();
        private int m_RoomsCleared = 0;

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

            // 2. Connect rooms
            Debug.Log("Step 2: Connecting rooms...");
            ConnectRooms();
            Debug.Log("Finished connecting rooms.");

            // 3. Build rooms from modified blueprints
            Debug.Log("Step 3: Building rooms from blueprints...");
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

        private void ConnectRooms()
        {
            if (m_RoomBuilders.Count < 2)
            {
                Debug.Log("Not enough rooms to connect.");
                return;
            }

            for (int i = 0; i < m_RoomBuilders.Count - 1; i++)
            {
                Debug.Log($"Connecting Room {i} and Room {i + 1}...");
                RoomBuilder roomA = m_RoomBuilders[i];
                RoomBuilder roomB = m_RoomBuilders[i + 1];
                CreateOpening(roomA, roomB);
            }
        }

        private void CreateOpening(RoomBuilder roomA, RoomBuilder roomB)
        {
            RoomBlueprint bpA = roomA.blueprint;
            RoomBlueprint bpB = roomB.blueprint;

            // Find the wall to remove in Room A (+X side)
            float wallAPosX = roomSize.x / 2;
            float wallBPosX = -roomSize.x / 2;
            var wallsA = GetWallsOnPlane(bpA, wallAPosX);
            var wallsB = GetWallsOnPlane(bpB, wallBPosX);

            bool keepA = wallsA.Count > 0 || wallsB.Count == 0; // prefer A if it has walls, or if both empty just pick A
            bool keepB = !keepA && wallsB.Count > 0;

            if (keepA)
            {
                // Remove B walls on the shared plane (dedupe)
                int removedBAll = RemoveAllWallsOnPlane(bpB, wallBPosX, roomSize.x);
                // Remove one segment on A to place door
                bool removedAOne = RemoveSingleWallOnPlane(bpA, wallAPosX);
                Debug.Log($"Connecting {roomA.name}<->{roomB.name}: keepA=true, removedAOne={removedAOne}, removedBAll={removedBAll}");

                if (removedAOne || removedBAll > 0)
                {
                    bpA.nodes.Add(new PropNode
                    {
                        instanceID = "Door_" + roomA.name + "_" + roomB.name,
                        itemID = "DoorSystem",
                        position = new SimpleVector3(wallAPosX, 0, 0),
                        rotation = new SimpleVector3(0, 90, 0)
                    });
                    // Optional: keep B door for symmetry? skip to avoid double doors.
                    AddKeyToRoomA(bpA, roomA.name);
                }
            }
            else if (keepB)
            {
                int removedAAll = RemoveAllWallsOnPlane(bpA, wallAPosX, roomSize.x);
                bool removedBOne = RemoveSingleWallOnPlane(bpB, wallBPosX);
                Debug.Log($"Connecting {roomA.name}<->{roomB.name}: keepB=true, removedAAll={removedAAll}, removedBOne={removedBOne}");

                if (removedBOne || removedAAll > 0)
                {
                    bpB.nodes.Add(new PropNode
                    {
                        instanceID = "Door_" + roomB.name + "_" + roomA.name,
                        itemID = "DoorSystem",
                        position = new SimpleVector3(wallBPosX, 0, 0),
                        rotation = new SimpleVector3(0, 90, 0)
                    });
                    AddKeyToRoomA(bpB, roomB.name);
                }
            }
            else
            {
                Debug.LogWarning($"Could not find walls on shared plane between {roomA.name} and {roomB.name}.");
            }
        }

        private bool RemoveSingleWallOnPlane(RoomBlueprint bp, float targetX, float epsilon = 0.5f)
        {
            int bestIndex = -1;
            float bestAbsZ = float.MaxValue;

            for (int i = 0; i < bp.nodes.Count; i++)
            {
                var n = bp.nodes[i];
                if (n.itemID != null && n.itemID.Contains("Wall") && Mathf.Abs(n.position.x - targetX) <= epsilon)
                {
                    float absZ = Mathf.Abs(n.position.z);
                    if (absZ < bestAbsZ)
                    {
                        bestAbsZ = absZ;
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex >= 0)
            {
                bp.nodes.RemoveAt(bestIndex);
                return true;
            }
            return false;
        }

        private int RemoveAllWallsOnPlane(RoomBlueprint bp, float targetX, float epsilon = 0.5f)
        {
            int removed = 0;
            bp.nodes = bp.nodes.FindAll(n =>
            {
                if (n.itemID != null && n.itemID.Contains("Wall") && Mathf.Abs(n.position.x - targetX) <= epsilon)
                {
                    removed++;
                    return false;
                }
                return true;
            });
            return removed;
        }

        private List<PropNode> GetWallsOnPlane(RoomBlueprint bp, float targetX, float epsilon = 0.5f)
        {
            var list = new List<PropNode>();
            foreach (var n in bp.nodes)
            {
                if (n.itemID != null && n.itemID.Contains("Wall") && Mathf.Abs(n.position.x - targetX) <= epsilon)
                {
                    list.Add(n);
                }
            }
            return list;
        }

        private void AddKeyToRoomA(RoomBlueprint bp, string roomName)
        {
            bp.nodes.Add(new PropNode
            {
                instanceID = "Key_" + roomName,
                itemID = "Key",
                position = new SimpleVector3(0, 1, 0),
                rotation = new SimpleVector3(0, 0, 0)
            });
            Debug.Log("Added Key node.");
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
