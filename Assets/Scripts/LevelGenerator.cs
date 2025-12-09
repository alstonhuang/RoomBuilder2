using UnityEngine;
using System.Collections.Generic;

public class LevelGenerator : MonoBehaviour
{
    public GameObject floorPrefab;
    public GameObject doorPrefab;
    public GameObject keyPrefab;

    void Start()
    {
        GenerateLevel();
    }

    void GenerateLevel()
    {
        // 1. 生成地板
        GameObject floorInstance = Instantiate(floorPrefab, Vector3.zero, Quaternion.identity);

        // 2. 生成門 (固定位置)
        Instantiate(doorPrefab, new Vector3(0.75f, 1f, 5f), Quaternion.identity);

        // 3. 在地板範圍內隨機生成鑰匙
        SpawnKeyOnFloor(floorInstance);
    }

    // 這就是剛剛修改的「貼地生成」函式
    void SpawnKeyOnFloor(GameObject floor)
    {
        Collider floorCollider = floor.GetComponent<Collider>();
        if (floorCollider == null) return;

        Bounds bounds = floorCollider.bounds;
        float padding = 0.5f;

        // 1. 先算出隨機的 X 和 Z
        float randomX = Random.Range(bounds.min.x + padding, bounds.max.x - padding);
        float randomZ = Random.Range(bounds.min.z + padding, bounds.max.z - padding);

        // 2. 從高空 (Y=10) 向下發射雷射
        Vector3 rayStart = new Vector3(randomX, 10f, randomZ);
        
        RaycastHit hit;
        
        // 向下偵測地板
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 20f))
        {
            // hit.point 是地板表面，我們往上加 0.3 (鑰匙半徑) 讓它剛好放在地上
            Vector3 spawnPos = hit.point + Vector3.up * 0.3f;

            Instantiate(keyPrefab, spawnPos, Quaternion.identity);
            Debug.Log("鑰匙已貼地生成於：" + spawnPos);
        }
        else
        {
            Debug.LogError("生成失敗！鑰匙下方沒有地板？");
        }
    } // 這裡是 SpawnKeyOnFloor 的結尾
} // <--- 這裡是 Class 的結尾 (你剛剛應該是少了這一個！)