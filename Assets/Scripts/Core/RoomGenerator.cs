using System.Collections.Generic;

namespace MyGame.Core
{
    public class RoomGenerator
    {
        private readonly ILogger _logger;
        private readonly IItemLibrary _library;
        
        // 結構生成器 (負責地板、牆壁)
        private readonly StructureGenerator _structureGen; 

        public RoomGenerator(ILogger logger, IItemLibrary library)
        {
            _logger = logger;
            _library = library;
            
            // 初始化結構生成器
            _structureGen = new StructureGenerator(library); 
        } // end of Constructor

        public RoomBlueprint GenerateFromTheme(SimpleBounds roomBounds, string themeID)
        {
            _logger.Log($"Director: 開始根據主題 '{themeID}' 生成...");
            var bp = new RoomBlueprint();

            // ==========================================
            // Phase 5.1: 生成地板 (Floor)
            // ==========================================
            // 呼叫結構生成器鋪地板
            var floorNodes = _structureGen.GenerateFloor(roomBounds, "FloorTile");
            bp.nodes.AddRange(floorNodes);

            // ==========================================
            // Phase 5.2: 生成牆壁 (Walls) -- 這是新加入的
            // ==========================================
            // 呼叫結構生成器蓋牆壁 (假設 ID 為 "Wall")
            var wallNodes = _structureGen.GenerateWalls(roomBounds, "Wall");
            bp.nodes.AddRange(wallNodes);

            // ==========================================
            // Phase 4: 生成家具 (Furniture)
            // ==========================================
            // 讀取主題清單
            List<string> itemsToPlace = _library.GetItemsInTheme(themeID);

            if (itemsToPlace.Count == 0)
            {
                _logger.LogWarning($"主題 '{themeID}' 清單為空或是找不到！");
                // 就算沒有家具，地板和牆壁已經生成了，所以還是回傳 bp
                return bp;
            }

            // 自動切分佈局 (遞迴切割空間)
            IContainer rootContainer = CreateAutoSplitLayout(itemsToPlace);
            
            // 加入家具節點
            bp.nodes.AddRange(rootContainer.Resolve(roomBounds, null));

            return bp;
        } // end of GenerateFromTheme

        // 自動佈局邏輯 (遞迴切分)
        private IContainer CreateAutoSplitLayout(List<string> items)
        {
            // 終止條件：只剩一個物品時，建立葉節點
            if (items.Count == 1)
            {
                return new ItemContainer(items[0], _library);
            }

            // 遞迴步驟：切半
            int mid = items.Count / 2;
            var leftItems = items.GetRange(0, mid);
            var rightItems = items.GetRange(mid, items.Count - mid);

            var leftChild = CreateAutoSplitLayout(leftItems);
            var rightChild = CreateAutoSplitLayout(rightItems);

            // 隨機決定切分方向
            bool splitVertical = new System.Random().Next(2) == 0;
            
            return new SplitContainer(leftChild, rightChild, splitVertical);
        } // end of CreateAutoSplitLayout

    } // end of class RoomGenerator
} // end of namespace MyGame.Core