using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    public class RandomScatterStrategy : IPlacementStrategy
    {
        private readonly System.Random _random;
        private readonly float _radius;
        private readonly float _minSpacing;
        private readonly int _maxAttempts = 10;

        public RandomScatterStrategy(float radius, float minSpacing)
        {
            _random = new System.Random();
            _radius = radius;
            _minSpacing = minSpacing;
        }

        public List<PropNode> Generate(string parentID, string itemID, int count)
        {
            var result = new List<PropNode>();
            var allocatedPoints = new List<SimpleVector3>();

            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < _maxAttempts; attempt++)
                {
                    float randomX = (float)(_random.NextDouble() * (_radius * 2) - _radius);
                    float randomZ = (float)(_random.NextDouble() * (_radius * 2) - _radius);
                    var candidatePos = new SimpleVector3(randomX, 0, randomZ);

                    if (IsPositionValid(candidatePos, allocatedPoints))
                    {
                        var node = new PropNode
                        {
                            instanceID = $"{itemID}_{Guid.NewGuid().ToString().Substring(0, 5)}",
                            itemID = itemID,
                            parentID = parentID,
                            position = candidatePos,
                            rotation = SimpleVector3.Zero
                        };
                        result.Add(node);
                        allocatedPoints.Add(candidatePos);
                        break; 
                    }
                }
            }
            return result;
        }

        private bool IsPositionValid(SimpleVector3 candidate, List<SimpleVector3> others)
        {
            foreach (var other in others)
            {
                float dx = candidate.x - other.x;
                float dz = candidate.z - other.z;
                if ((dx * dx + dz * dz) < _minSpacing * _minSpacing) return false;
            }
            return true;
        }
    }
}