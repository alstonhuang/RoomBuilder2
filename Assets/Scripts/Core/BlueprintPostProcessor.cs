using System;
using System.Collections.Generic;

namespace MyGame.Core
{
    /// <summary>
    /// Core-side post processors for blueprint sanitation (keeps adapters thin).
    /// </summary>
    public static class BlueprintPostProcessor
    {
        /// <summary>
        /// Removes wall nodes whose centers overlap a door footprint on XZ (with tolerance) and deduplicates wall nodes.
        /// Keeps domain logic in Core; adapters supply a size provider.
        /// </summary>
        public static void RemoveDoorWallOverlaps(RoomBlueprint blueprint, Func<string, SimpleVector3> sizeProvider, float tolerance = 0.05f)
        {
            if (blueprint == null || blueprint.nodes == null) return;
            if (sizeProvider == null) sizeProvider = _ => SimpleVector3.Zero;

            var doors = new List<PropNode>();
            var walls = new List<PropNode>();
            var others = new List<PropNode>();

            foreach (var n in blueprint.nodes)
            {
                if (n.itemID != null && n.itemID.ToLower().Contains("door")) doors.Add(n);
                else if (n.itemID != null && n.itemID.Contains("Wall")) walls.Add(n);
                else others.Add(n);
            }

            var keptWalls = new List<PropNode>();
            foreach (var wall in walls)
            {
                bool removed = false;
                foreach (var door in doors)
                {
                    var doorSize = sizeProvider(door.itemID);
                    float halfX = (doorSize.x * 0.5f) + tolerance;
                    float halfZ = (doorSize.z * 0.5f) + tolerance;

                    if (Math.Abs(wall.position.x - door.position.x) <= halfX &&
                        Math.Abs(wall.position.z - door.position.z) <= halfZ)
                    {
                        removed = true;
                        break;
                    }
                }
                if (!removed) keptWalls.Add(wall);
            }

            // Deduplicate walls occupying the same position.
            var seen = new HashSet<string>();
            var dedupedWalls = new List<PropNode>();
            foreach (var wall in keptWalls)
            {
                string key = $"{wall.itemID}_{wall.position.x:F3}_{wall.position.y:F3}_{wall.position.z:F3}";
                if (seen.Add(key)) dedupedWalls.Add(wall);
            }

            blueprint.nodes = new List<PropNode>();
            blueprint.nodes.AddRange(others);
            blueprint.nodes.AddRange(doors);
            blueprint.nodes.AddRange(dedupedWalls);
        }
    }
}
