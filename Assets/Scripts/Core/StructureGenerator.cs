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

            // 1. 取得地磚大小
            SimpleVector3 tileSize = _library.GetItemSize(floorItemID);
            if (tileSize.x <= 0 || tileSize.z <= 0) return nodes;

            // 2. 計算 X, Z 起點 (保持不變)
            float startX = roomBounds.center.x - (roomBounds.size.x / 2) + (tileSize.x / 2);
            float startZ = roomBounds.center.z - (roomBounds.size.z / 2) + (tileSize.z / 2);
            float endX = roomBounds.center.x + (roomBounds.size.x / 2);
            float endZ = roomBounds.center.z + (roomBounds.size.z / 2);

            // 🛑 3. 修正 Y 軸計算：對齊房間底部
            // 房間底部 = 中心Y - (高度 / 2)
            float roomBottomY = roomBounds.center.y - (roomBounds.size.y / 2);
            
            // 地板的位置 = 房間底部 - (地磚厚度 / 2)
            // 這樣地板的 "表面" 就會剛好切齊房間的底部線
            float yPos = roomBottomY - (tileSize.y / 2);

            // 4. 迴圈生成
            for (float x = startX; x < endX; x += tileSize.x)
            {
                for (float z = startZ; z < endZ; z += tileSize.z)
                {
                    nodes.Add(new PropNode
                    {
                        instanceID = $"Floor_{x}_{z}",
                        itemID = floorItemID,
                        parentID = null,
                        position = new SimpleVector3(x, yPos, z), // 👈 使用修正後的高度
                        rotation = SimpleVector3.Zero
                    });
                }
            }
            return nodes;
        } // end of GenerateFloor

        // 👇 新增牆壁生成邏輯
        public List<PropNode> GenerateWalls(SimpleBounds roomBounds, string wallItemID)
        {
            var nodes = new List<PropNode>();
            SimpleVector3 wallSize = _library.GetItemSize(wallItemID);

            if (wallSize.x <= 0) return nodes;

            float width = roomBounds.size.x;
            float depth = roomBounds.size.z;
            
            // 計算邊界位置 (假設 bounds.center 是 0,0)
            float xMin = roomBounds.center.x - width / 2;
            float xMax = roomBounds.center.x + width / 2;
            float zMin = roomBounds.center.z - depth / 2;
            float zMax = roomBounds.center.z + depth / 2;

            // 調整：為了讓牆壁剛好包住地板，我們通常往外推半個牆厚
            // 但 MVP 先求有，直接蓋在邊線上即可

            // 1. 南牆 (South Wall) - 沿著 X 軸，Z 固定在 zMin
            // 面向北 (Rot Y = 0)
            for (float x = xMin; x < xMax; x += wallSize.x)
            {
                nodes.Add(CreateWallNode(wallItemID, x + wallSize.x/2, zMin, 0));
            }

            // 2. 北牆 (North Wall) - 沿著 X 軸，Z 固定在 zMax
            // 面向南 (Rot Y = 180)
            for (float x = xMin; x < xMax; x += wallSize.x)
            {
                nodes.Add(CreateWallNode(wallItemID, x + wallSize.x/2, zMax, 180));
            }

            // 3. 西牆 (West Wall) - 沿著 Z 軸，X 固定在 xMin
            // 面向東 (Rot Y = 90)
            for (float z = zMin; z < zMax; z += wallSize.x) // 注意這裡間距用 wallSize.x (牆寬)
            {
                nodes.Add(CreateWallNode(wallItemID, xMin, z + wallSize.x/2, 90));
            }

            // 4. 東牆 (East Wall) - 沿著 Z 軸，X 固定在 xMax
            // 面向西 (Rot Y = 270)
            for (float z = zMin; z < zMax; z += wallSize.x)
            {
                nodes.Add(CreateWallNode(wallItemID, xMax, z + wallSize.x/2, 270));
            }

            return nodes;
        } // end of GenerateWalls

        private PropNode CreateWallNode(string itemID, float x, float z, float yRot)
        {
            return new PropNode
            {
                instanceID = $"Wall_{x}_{z}",
                itemID = itemID,
                parentID = null,
                // Y = 1.5 是因為牆高 3米，中心點在 1.5 (如果 Pivot 在底部則設為 0)
                // 這裡假設 Pivot 在底部 (符合之前的修正建議)
                position = new SimpleVector3(x, 0, z), 
                rotation = new SimpleVector3(0, yRot, 0)
            };
        } // end of CreateWallNode
    } // end of class
} // end of namespace