using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Adapters.Unity
{
    [CreateAssetMenu(menuName = "PCG/ItemDatabase")]
    public class ItemDatabaseAsset : ScriptableObject
    {
        public List<ItemDefinition> items = new List<ItemDefinition>();
    }
}

