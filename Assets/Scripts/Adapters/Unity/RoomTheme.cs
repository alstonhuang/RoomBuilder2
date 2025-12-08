using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Adapters.Unity
{
    [CreateAssetMenu(menuName = "PCG/RoomTheme")]
    public class RoomTheme : ScriptableObject
    {
        public string themeID;
        public List<string> requiredItems;
    }
}