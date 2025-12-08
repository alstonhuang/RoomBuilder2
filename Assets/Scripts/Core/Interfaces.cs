using System.Collections.Generic;

namespace MyGame.Core
{
    public enum CorePlacementType { Fixed, Scatter, Stack }

    public struct CoreGenerationRule
    {
        public bool useTag;
        public string targetIDorTag;
        public CorePlacementType type;
        public bool useDensity;  
        public float density;    
        public int minCount;
        public int maxCount;
        public float probability;
        public SimpleVector3 offset;
        public float radius;
    }

    public interface ILogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    public interface IItemLibrary
    {
        SimpleVector3 GetMinBounds(string itemID);
        List<CoreGenerationRule> GetRules(string itemID);
        SimpleVector3 GetItemSize(string itemID);
        string GetRandomItemIDByTag(string tag);
        List<string> GetItemsInTheme(string themeID);
    }

    public interface IPlacementStrategy
    {
        List<PropNode> Generate(string parentID, string itemID, int count);
    }
}