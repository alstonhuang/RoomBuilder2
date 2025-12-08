using System.Collections.Generic;
using System.Linq;
using MyGame.Core;
using Random = System.Random;

namespace MyGame.Adapters.Unity
{
    public class ItemLibraryAdapter : IItemLibrary
    {
        private Dictionary<string, ItemDefinition> _database = new Dictionary<string, ItemDefinition>();
        private Dictionary<string, RoomTheme> _themes = new Dictionary<string, RoomTheme>();
        private Random _random = new Random();

        public ItemLibraryAdapter(List<ItemDefinition> items, List<RoomTheme> themes)
        {
            foreach (var i in items) if (i && !_database.ContainsKey(i.itemID)) _database.Add(i.itemID, i);
            foreach (var t in themes) if (t && !_themes.ContainsKey(t.themeID)) _themes.Add(t.themeID, t);
        }

        public SimpleVector3 GetMinBounds(string itemID)
        {
            return _database.TryGetValue(itemID, out var def) 
                ? new SimpleVector3(def.minBounds.x, def.minBounds.y, def.minBounds.z) 
                : new SimpleVector3(0,0,0);
        }

        public SimpleVector3 GetItemSize(string itemID)
        {
            return _database.TryGetValue(itemID, out var def) 
                ? new SimpleVector3(def.logicalSize.x, def.logicalSize.y, def.logicalSize.z) 
                : new SimpleVector3(1,1,1);
        }

        public List<CoreGenerationRule> GetRules(string itemID)
        {
            var result = new List<CoreGenerationRule>();
            if (_database.TryGetValue(itemID, out var def) && def.rules != null)
            {
                foreach (var r in def.rules)
                {
                    result.Add(new CoreGenerationRule {
                        useTag = r.useTag, targetIDorTag = r.targetIDorTag, type = (CorePlacementType)r.type,
                        useDensity = r.useDensity, density = r.density, minCount = r.minCount, maxCount = r.maxCount,
                        probability = r.probability, offset = new SimpleVector3(r.offset.x, r.offset.y, r.offset.z), radius = r.radius
                    });
                }
            }
            return result;
        }

        public string GetRandomItemIDByTag(string tag)
        {
            var list = _database.Values.Where(d => d.categoryTag == tag).Select(d => d.itemID).ToList();
            return list.Count > 0 ? list[_random.Next(list.Count)] : null;
        }

        public List<string> GetItemsInTheme(string themeID)
        {
            return _themes.TryGetValue(themeID, out var t) ? new List<string>(t.requiredItems) : new List<string>();
        }
    }
}