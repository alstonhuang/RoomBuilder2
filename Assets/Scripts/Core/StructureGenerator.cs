using System.Collections.Generic;

namespace MyGame.Core
{
    public class StructureGenerator
    {
        private readonly IItemLibrary _library;

        public StructureGenerator(IItemLibrary library)
        {
            _library = library;
        }

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
        }
    }
}