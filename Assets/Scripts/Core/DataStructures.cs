using System.Collections.Generic;

namespace MyGame.Core
{
    public enum ContainerKind
    {
        Unknown,
        Region,
        Floor,
        Ceiling,
        Wall,
        Corner,
        Door,
        Window,
        Table,
        Chair,
        Passage,
        Stair,
        Shelf,
        Decor,
        Custom
    }

    public enum Facing
    {
        None,
        North,
        East,
        South,
        West,
        Up,
        Down
    }

    public enum RelationType
    {
        Adjacent,
        Stacked,
        Contains,
        Attached,
        Aligned
    }

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

    public class ContainerRelation
    {
        public string targetInstanceID;
        public RelationType type;
        public Facing side;
        public string note;
    }

    public class ContainerNode
    {
        public string instanceID;
        public ContainerKind kind;
        public SimpleBounds bounds;
        public SimpleVector3 rotation;
        public Facing facing;
        public string parentID;
        public string itemID; // optional: desired asset
        public List<ContainerRelation> relations = new List<ContainerRelation>();
        public List<ContainerNode> children = new List<ContainerNode>();

        public IEnumerable<PropNode> FlattenToPropNodes()
        {
            var list = new List<PropNode>();
            FlattenInto(list, parentID);
            return list;
        }

        private void FlattenInto(List<PropNode> list, string parent)
        {
            var node = new PropNode
            {
                instanceID = instanceID,
                itemID = itemID,
                parentID = parent,
                position = bounds.center,
                rotation = rotation,
                containerKind = kind,
                logicalBounds = bounds,
                facing = facing
            };
            list.Add(node);
            foreach (var child in children)
            {
                child.FlattenInto(list, instanceID);
            }
        }
    }

    public class PropNode
    {
        public string instanceID;
        public string itemID;
        public string parentID;
        public SimpleVector3 position;
        public SimpleVector3 rotation;

        // Container metadata (optional; ignored by older code)
        public ContainerKind containerKind;
        public SimpleBounds logicalBounds;
        public Facing facing;
        public List<ContainerRelation> relations = new List<ContainerRelation>();
    }

    public class RoomBlueprint
    {
        public List<PropNode> nodes = new List<PropNode>();
        public List<ContainerNode> containers = new List<ContainerNode>();
    }
}
