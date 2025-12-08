using System.Collections.Generic;

namespace MyGame.Core
{
    public struct SimpleVector3 
    { 
        public float x, y, z; 
        public SimpleVector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static SimpleVector3 Zero => new SimpleVector3(0,0,0);
    }

    public struct SimpleBounds
    {
        public SimpleVector3 center;
        public SimpleVector3 size;
        public SimpleBounds(SimpleVector3 center, SimpleVector3 size) { this.center = center; this.size = size; }
    }

    public class PropNode
    {
        public string instanceID;
        public string itemID;
        public string parentID;
        public SimpleVector3 position;
        public SimpleVector3 rotation;
    }

    public class RoomBlueprint
    {
        public List<PropNode> nodes = new List<PropNode>();
    }
}