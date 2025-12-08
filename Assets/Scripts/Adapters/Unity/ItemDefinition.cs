using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Adapters.Unity
{
    public enum PlacementType { Fixed, Scatter, Stack }

    [System.Serializable]
    public struct GenerationRule
    {
        public bool useTag;
        public string targetIDorTag;
        public PlacementType type;

        [Header("自動化數量")]
        public bool useDensity;
        public float density;
        public int minCount;
        public int maxCount;
        
        [Range(0f, 1f)]
        public float probability;
        public Vector3 offset;
        public float radius;
    }

    [CreateAssetMenu(menuName = "PCG/ItemDefinition")]
    public class ItemDefinition : ScriptableObject
    {
        public string itemID;
        public GameObject prefab;
        public string categoryTag;

        [Header("邏輯數據")]
        public Vector3 logicalSize = Vector3.one;
        public Vector3 minBounds = Vector3.one;

        [Header("生成邏輯")]
        public List<GenerationRule> rules;

        [ContextMenu("Auto Calculate Size")]
        private void CalculateSize()
        {
            if (prefab != null)
            {
                var col = prefab.GetComponentInChildren<Collider>();
                if (col != null) logicalSize = col.bounds.size;
                else
                {
                    var ren = prefab.GetComponentInChildren<Renderer>();
                    if (ren != null) logicalSize = ren.bounds.size;
                }
                Debug.Log($"[{itemID}] Calculated Size: {logicalSize}");
            }
        }
    }
}