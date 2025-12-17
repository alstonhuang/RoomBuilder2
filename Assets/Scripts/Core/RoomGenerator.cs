using System.Collections.Generic;
using System.Linq;
using System;

namespace MyGame.Core
{
    /// <summary>
    /// Generates a container tree (Region -> Floor/Walls/Furniture groups) and flattens to PropNodes for legacy pipeline.
    /// Leaves are actual items (prefabs) while intermediate nodes are logical containers.
    /// </summary>
    public class RoomGenerator
    {
        private readonly ILogger _logger;
        private readonly IItemLibrary _library;
        private readonly StructureGenerator _structureGen;

        public RoomGenerator(ILogger logger, IItemLibrary library)
        {
            _logger = logger;
            _library = library;
            _structureGen = new StructureGenerator(library);
        }

        public RoomBlueprint GenerateFromTheme(SimpleBounds roomBounds, string themeID,
                                            bool skipNorthWall, bool skipSouthWall,
                                            bool skipEastWall, bool skipWestWall)
        {
            _logger.Log($"Director: Generating theme '{themeID}' ...");
            var bp = new RoomBlueprint();

            var floorNodes = _structureGen.GenerateFloor(roomBounds, "FloorTile");
            var wallNodes = _structureGen.GenerateWalls(roomBounds, "Wall",
                                                        skipNorthWall, skipSouthWall,
                                                        skipEastWall, skipWestWall);

            List<string> itemsToPlace = _library.GetItemsInTheme(themeID);
            if (itemsToPlace.Count == 0)
            {
                _logger.LogWarning($"Theme '{themeID}' item list is empty.");
                return bp;
            }

            IContainer rootContainer = CreateAutoSplitLayout(itemsToPlace);
            var furnitureNodes = rootContainer.Resolve(roomBounds, null);

            // Build container tree: Region -> floor/wall/furniture groups -> leaves
            var region = new ContainerNode
            {
                instanceID = "Region_Room",
                kind = ContainerKind.Region,
                bounds = roomBounds,
                rotation = SimpleVector3.Zero,
                facing = Facing.None,
                parentID = null
            };

            var floorGroup = new ContainerNode
            {
                instanceID = "Group_Floor",
                kind = ContainerKind.Region,
                bounds = roomBounds,
                rotation = SimpleVector3.Zero,
                parentID = region.instanceID
            };
            floorGroup.children.AddRange(floorNodes.Select(ToContainerNode));

            var wallGroup = new ContainerNode
            {
                instanceID = "Group_Wall",
                kind = ContainerKind.Region,
                bounds = roomBounds,
                rotation = SimpleVector3.Zero,
                parentID = region.instanceID
            };
            wallGroup.children.AddRange(wallNodes.Select(ToContainerNode));

            var furnitureGroup = new ContainerNode
            {
                instanceID = "Group_Furniture",
                kind = ContainerKind.Region,
                bounds = roomBounds,
                rotation = SimpleVector3.Zero,
                parentID = region.instanceID
            };
            furnitureGroup.children.AddRange(BuildTreeFromParentIds(furnitureNodes));

            region.children.Add(floorGroup);
            region.children.Add(wallGroup);
            region.children.Add(furnitureGroup);

            bp.containers.Add(region);

            // Important: Keep the original PropNodes list for runtime building.
            // Some placement strategies (e.g., RandomScatterStrategy) emit child positions as LOCAL offsets from the parent.
            // Flattening into ContainerNodes would lose that semantic and break nesting (cups end up far from tables, etc.).
            bp.nodes = new List<PropNode>();
            bp.nodes.AddRange(floorNodes);
            bp.nodes.AddRange(wallNodes);
            bp.nodes.AddRange(furnitureNodes);

            return bp;
        }

        private List<ContainerNode> BuildTreeFromParentIds(List<PropNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return new List<ContainerNode>();

            // Build nodes map first (stable even if input is out of order).
            var map = new Dictionary<string, ContainerNode>();
            foreach (var n in nodes)
            {
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.instanceID)) continue;
                if (!map.ContainsKey(n.instanceID))
                {
                    map[n.instanceID] = ToContainerNode(n);
                }
            }

            // Attach children by original parentID.
            foreach (var n in nodes)
            {
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.instanceID)) continue;
                if (!map.TryGetValue(n.instanceID, out var child)) continue;

                if (!string.IsNullOrEmpty(n.parentID) && map.TryGetValue(n.parentID, out var parent))
                {
                    parent.children.Add(child);
                }
            }

            // Roots are those whose parent isn't in the same set.
            var roots = new List<ContainerNode>();
            foreach (var n in nodes)
            {
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.instanceID)) continue;
                if (!map.TryGetValue(n.instanceID, out var node)) continue;

                if (string.IsNullOrEmpty(n.parentID) || !map.ContainsKey(n.parentID))
                {
                    roots.Add(node);
                }
            }

            return roots;
        }

        private IContainer CreateAutoSplitLayout(List<string> items)
        {
            if (items.Count == 1)
            {
                return new ItemContainer(items[0], _library);
            }

            int mid = items.Count / 2;
            var leftItems = items.GetRange(0, mid);
            var rightItems = items.GetRange(mid, items.Count - mid);

            var leftChild = CreateAutoSplitLayout(leftItems);
            var rightChild = CreateAutoSplitLayout(rightItems);

            bool splitVertical = new System.Random().Next(2) == 0;
            
            return new SplitContainer(leftChild, rightChild, splitVertical);
        }

        private ContainerNode ToContainerNode(PropNode n)
        {
            var size = n.logicalBounds.size;
            var center = n.logicalBounds.center;
            if (size.x == 0 && size.y == 0 && size.z == 0)
            {
                size = new SimpleVector3(1, 1, 1);
            }
            if (center.x == 0 && center.y == 0 && center.z == 0)
            {
                center = n.position;
            }

            return new ContainerNode
            {
                instanceID = n.instanceID,
                itemID = n.itemID,
                parentID = n.parentID,
                bounds = new SimpleBounds(center, size),
                rotation = n.rotation,
                kind = n.containerKind,
                facing = n.facing,
                relations = n.relations ?? new List<ContainerRelation>(),
                children = new List<ContainerNode>()
            };
        }
    }
}
