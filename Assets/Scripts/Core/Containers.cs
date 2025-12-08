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
                rotation = SimpleVector3.Zero
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
                                rotation = SimpleVector3.Zero
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
}