using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    public interface IContainer
    {
        List<PropNode> Resolve(SimpleBounds bounds, string parentID);
    }

    public class ItemContainer : IContainer
    { 
        private string _itemID;
        private IItemLibrary _library;
        private Random _random;

        public ItemContainer(string itemID, IItemLibrary library)
        {
            _itemID = itemID;
            _library = library;
            _random = new Random();
        }

        public List<PropNode> Resolve(SimpleBounds bounds, string parentID)
        {
            // 1. 空間檢查
            SimpleVector3 minReq = _library != null ? _library.GetMinBounds(_itemID) : new SimpleVector3(0, 0, 0);
            if (bounds.size.x < minReq.x || bounds.size.z < minReq.z) return new List<PropNode>();

            var resultList = new List<PropNode>();
            string myInstanceID = $"{_itemID}_{Guid.NewGuid().ToString().Substring(0, 4)}";

            // 2. 生成自己
            resultList.Add(new PropNode
            {
                instanceID = myInstanceID,
                itemID = _itemID,
                parentID = parentID,
                position = bounds.center,
                rotation = SimpleVector3.Zero,
                containerKind = ContainerKind.Unknown,
                logicalBounds = bounds
            });

            // 3. 連鎖反應 (生成子物件)
            if (_library != null)
            {
                var rules = _library.GetRules(_itemID);
                foreach (var rule in rules)
                {
                    if (_random.NextDouble() > rule.probability) continue;

                    // 數量計算 (支援密度)
                    int count = 0;
                    if (rule.useDensity)
                    {
                        float area = bounds.size.x * bounds.size.z; // 這裡簡化用容器面積，實際上可用物品面積
                        count = (int)(area * rule.density);
                        count = Math.Clamp(count, rule.minCount, rule.maxCount);
                    }
                    else
                    {
                        count = _random.Next(rule.minCount, rule.maxCount + 1);
                    }
                    if (count <= 0) continue;

                    // 決定目標 ID
                    string finalTargetID = rule.targetIDorTag;
                    if (rule.useTag)
                    {
                        finalTargetID = _library.GetRandomItemIDByTag(rule.targetIDorTag);
                        if (string.IsNullOrEmpty(finalTargetID)) continue;
                    }

                    // 執行策略
                    List<PropNode> childrenNodes = new List<PropNode>();
                    switch (rule.type)
                    {
                        case CorePlacementType.Scatter:
                            SimpleVector3 childSize = _library.GetItemSize(finalTargetID);
                            float spacing = childSize.x * 1.5f; // 安全係數
                            var strategy = new RandomScatterStrategy(rule.radius, spacing);
                            childrenNodes = strategy.Generate(myInstanceID, finalTargetID, count);
                            break;
                        
                        case CorePlacementType.Fixed:
                             childrenNodes.Add(new PropNode {
                                instanceID = $"{finalTargetID}_{Guid.NewGuid()}",
                                itemID = finalTargetID,
                                parentID = myInstanceID,
                                position = rule.offset,
                                rotation = SimpleVector3.Zero,
                                containerKind = ContainerKind.Unknown,
                                logicalBounds = bounds
                            });
                            break;
                    }
                    resultList.AddRange(childrenNodes);
                }
            }
            return resultList;
        }
    }

    public class SplitContainer : IContainer
    {
        private IContainer _childA;
        private IContainer _childB;
        private bool _splitVertical;

        public SplitContainer(IContainer childA, IContainer childB, bool splitVertical)
        {
            _childA = childA;
            _childB = childB;
            _splitVertical = splitVertical;
        }

        public List<PropNode> Resolve(SimpleBounds bounds, string parentID)
        {
            var result = new List<PropNode>();
            SimpleBounds boundsA, boundsB;
            
            if (_splitVertical)
            {
                float halfW = bounds.size.x / 2;
                boundsA = new SimpleBounds(new SimpleVector3(bounds.center.x - halfW / 2, bounds.center.y, bounds.center.z), new SimpleVector3(halfW, bounds.size.y, bounds.size.z));
                boundsB = new SimpleBounds(new SimpleVector3(bounds.center.x + halfW / 2, bounds.center.y, bounds.center.z), new SimpleVector3(halfW, bounds.size.y, bounds.size.z));
            }
            else
            {
                float halfD = bounds.size.z / 2;
                boundsA = new SimpleBounds(new SimpleVector3(bounds.center.x, bounds.center.y, bounds.center.z - halfD / 2), new SimpleVector3(bounds.size.x, bounds.size.y, halfD));
                boundsB = new SimpleBounds(new SimpleVector3(bounds.center.x, bounds.center.y, bounds.center.z + halfD / 2), new SimpleVector3(bounds.size.x, bounds.size.y, halfD));
            }

            result.AddRange(_childA.Resolve(boundsA, parentID));
            result.AddRange(_childB.Resolve(boundsB, parentID));
            return result;
        }
    }

    /// <summary>
    /// Generic container that places a single logical node (e.g., Floor, Wall, Corner) with metadata.
    /// </summary>
    public class PrimitiveContainer : IContainer
    {
        private readonly string _itemID;
        private readonly ContainerKind _kind;
        private readonly Facing _facing;
        private readonly SimpleVector3 _rotation;

        public PrimitiveContainer(string itemID, ContainerKind kind, Facing facing = Facing.None, SimpleVector3? rotation = null)
        {
            _itemID = itemID;
            _kind = kind;
            _facing = facing;
            _rotation = rotation ?? SimpleVector3.Zero;
        }

        public List<PropNode> Resolve(SimpleBounds bounds, string parentID)
        {
            var node = new PropNode
            {
                instanceID = $"{_itemID}_{Guid.NewGuid():N}".Substring(0, 12),
                itemID = _itemID,
                parentID = parentID,
                position = bounds.center,
                rotation = _rotation,
                containerKind = _kind,
                logicalBounds = bounds,
                facing = _facing
            };
            return new List<PropNode> { node };
        }
    }

    /// <summary>
    /// Composite region container that can emit itself (as a logical region) and its children.
    /// </summary>
    public class RegionContainer : IContainer
    {
        private readonly List<IContainer> _children;
        private readonly bool _emitSelf;
        private readonly string _itemID;
        private readonly Facing _facing;

        public RegionContainer(IEnumerable<IContainer> children, bool emitSelf = false, string itemID = "Region", Facing facing = Facing.None)
        {
            _children = new List<IContainer>(children);
            _emitSelf = emitSelf;
            _itemID = itemID;
            _facing = facing;
        }

        public List<PropNode> Resolve(SimpleBounds bounds, string parentID)
        {
            var result = new List<PropNode>();
            string myId = parentID;
            if (_emitSelf)
            {
                myId = $"{_itemID}_{Guid.NewGuid():N}".Substring(0, 12);
                result.Add(new PropNode
                {
                    instanceID = myId,
                    itemID = _itemID,
                    parentID = parentID,
                    position = bounds.center,
                    rotation = SimpleVector3.Zero,
                    containerKind = ContainerKind.Region,
                    logicalBounds = bounds,
                    facing = _facing
                });
            }

            foreach (var child in _children)
            {
                result.AddRange(child.Resolve(bounds, myId));
            }
            return result;
        }
    }

    /// <summary>
    /// Convenience containers for walls/doors/windows/corners.
    /// </summary>
    public class WallContainer : PrimitiveContainer
    {
        public WallContainer(string itemID, Facing facing) : base(itemID, ContainerKind.Wall, facing) { }
    }

    public class CornerContainer : PrimitiveContainer
    {
        public CornerContainer(string itemID) : base(itemID, ContainerKind.Corner, Facing.None) { }
    }

    public class FloorContainer : PrimitiveContainer
    {
        public FloorContainer(string itemID) : base(itemID, ContainerKind.Floor, Facing.Up) { }
    }

    public class CeilingContainer : PrimitiveContainer
    {
        public CeilingContainer(string itemID) : base(itemID, ContainerKind.Ceiling, Facing.Down) { }
    }

    public class DoorContainer : PrimitiveContainer
    {
        public DoorContainer(string itemID, Facing facing) : base(itemID, ContainerKind.Door, facing) { }
    }

    public class WindowContainer : PrimitiveContainer
    {
        public WindowContainer(string itemID, Facing facing) : base(itemID, ContainerKind.Window, facing) { }
    }
}
