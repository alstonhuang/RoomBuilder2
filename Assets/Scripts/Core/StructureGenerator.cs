using System.Collections.Generic;

namespace MyGame.Core
{
    public class StructureGenerator
    {
        private readonly IItemLibrary _library;

        public StructureGenerator(IItemLibrary library)
        {
            _library = library;
        } // end of constructor

        public List<PropNode> GenerateFloor(SimpleBounds roomBounds, string floorItemID)
        {
            var nodes = new List<PropNode>();

            // 1. ???啁?憭批?
            SimpleVector3 tileSize = _library.GetItemSize(floorItemID);
            if (tileSize.x <= 0 || tileSize.z <= 0) return nodes;

            // 2. 閮? X, Z 韏琿? (靽?銝?)
            float startX = roomBounds.center.x - (roomBounds.size.x / 2) + (tileSize.x / 2);
            float startZ = roomBounds.center.z - (roomBounds.size.z / 2) + (tileSize.z / 2);
            float endX = roomBounds.center.x + (roomBounds.size.x / 2);
            float endZ = roomBounds.center.z + (roomBounds.size.z / 2);

            // ?? 3. 靽格迤 Y 頠貉?蝞?撠??輸?摨
            // ?輸?摨 = 銝剖?Y - (擃漲 / 2)
            float roomBottomY = roomBounds.center.y - (roomBounds.size.y / 2);
            
            // ?唳??蝵?= ?輸?摨 - (?啁??漲 / 2)
            // ?見?唳??"銵券" 撠望??末???輸????函?
            float yPos = roomBottomY - (tileSize.y / 2);

            // 4. 餈游???
            for (float x = startX; x < endX; x += tileSize.x)
            {
                for (float z = startZ; z < endZ; z += tileSize.z)
                {
                    nodes.Add(new PropNode
                    {
                        instanceID = $"Floor_{x}_{z}",
                        itemID = floorItemID,
                        parentID = null,
                        position = new SimpleVector3(x, yPos, z), // ?? 雿輻靽格迤敺?擃漲
                        rotation = SimpleVector3.Zero,
                        containerKind = ContainerKind.Floor,
                        logicalBounds = new SimpleBounds(new SimpleVector3(x, yPos, z), tileSize),
                        facing = Facing.Up
                    });
                }
            }
            return nodes;
        } // end of GenerateFloor

        // ?? ?啣??????摩
        public List<PropNode> GenerateWalls(SimpleBounds roomBounds, string wallItemID,
                                            bool skipNorth, bool skipSouth,
                                            bool skipEast, bool skipWest)
        {
            var nodes = new List<PropNode>();
            SimpleVector3 wallSize = _library.GetItemSize(wallItemID);

            if (wallSize.x <= 0) return nodes;

            float width = roomBounds.size.x;
            float depth = roomBounds.size.z;
            float thickness = wallSize.z;

            // Calculate boundary positions
            float xMin = roomBounds.center.x - width / 2;
            float xMax = roomBounds.center.x + width / 2;
            float zMin = roomBounds.center.z - depth / 2;
            float zMax = roomBounds.center.z + depth / 2;

            // 1. ?? (South Wall) - 瘝輯? X 頠賂?Z ?箏???zMin
            // ?Ｗ???(Rot Y = 0)
            if (!skipSouth)
            {
                for (float x = xMin; x < xMax; x += wallSize.x)
                {
                    nodes.Add(CreateWallNode(wallItemID, wallSize, x + wallSize.x/2, zMin - (thickness * 0.5f), 0, Facing.South));
                }
            }

            // 2. ?? (North Wall) - 瘝輯? X 頠賂?Z ?箏???zMax
            // ?Ｗ???(Rot Y = 180)
            if (!skipNorth)
            {
                for (float x = xMin; x < xMax; x += wallSize.x)
                {
                    nodes.Add(CreateWallNode(wallItemID, wallSize, x + wallSize.x/2, zMax + (thickness * 0.5f), 180, Facing.North));
                }
            }

            // 3. 镼輻? (West Wall) - 瘝輯? Z 頠賂?X ?箏???xMin
            // ?Ｗ???(Rot Y = 90)
            if (!skipWest)
            {
                for (float z = zMin; z < zMax; z += wallSize.x) // 瘜冽??ㄐ????wallSize.x (?祝)
                {
                    nodes.Add(CreateWallNode(wallItemID, wallSize, xMin - (thickness * 0.5f), z + wallSize.x/2, 90, Facing.West));
                }
            }

            // 4. ?梁? (East Wall) - 瘝輯? Z 頠賂?X ?箏???xMax
            // ?Ｗ?镼?(Rot Y = 270)
            if (!skipEast)
            {
                for (float z = zMin; z < zMax; z += wallSize.x)
                {
                    nodes.Add(CreateWallNode(wallItemID, wallSize, xMax + (thickness * 0.5f), z + wallSize.x/2, 270, Facing.East));
                }
            }
            return nodes;
        } // end of GenerateWalls

        private PropNode CreateWallNode(string itemID, SimpleVector3 wallSize, float x, float z, float yRot, Facing facing)
        {
            return new PropNode
            {
                instanceID = $"Wall_{x}_{z}",
                itemID = itemID,
                parentID = null,
                position = new SimpleVector3(x, 0, z),
                rotation = new SimpleVector3(0, yRot, 0),
                containerKind = ContainerKind.Wall,
                logicalBounds = new SimpleBounds(new SimpleVector3(x, 0, z), wallSize),
                facing = facing
            };
        }
        // end of CreateWallNode
    } // end of class
} // end of namespace
