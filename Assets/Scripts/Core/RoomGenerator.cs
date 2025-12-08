using System.Collections.Generic;

namespace MyGame.Core
{
    public class RoomGenerator
    {
        private readonly ILogger _logger;
        private readonly IItemLibrary _library;
        private readonly RuleGenerator _ruleGen;        // 確保你有加回這行 (如果你選了選擇1)
        private readonly StructureGenerator _structureGen; 

        public RoomGenerator(ILogger logger, IItemLibrary library)
        {
            _logger = logger;
            _library = library;
            _ruleGen = new RuleGenerator();             // 確保你有加回這行
            _structureGen = new StructureGenerator(library); 
        }

        public RoomBlueprint GenerateFromTheme(SimpleBounds roomBounds, string themeID)
        {
            _logger.Log($"Director: 開始生成主題 '{themeID}'...");
            var bp = new RoomBlueprint();

            // ==========================================
            // Phase 5: 生成結構 (地板)
            // ==========================================
            // 呼叫結構生成器鋪地板
            var floorNodes = _structureGen.GenerateFloor(roomBounds, "FloorTile");
            bp.nodes.AddRange(floorNodes);

            // ==========================================
            // Phase 4: 生成家具 (核心家具)
            // ==========================================
            // 🛑 注意：這裡只宣告一次 itemsToPlace
            List<string> itemsToPlace = _library.GetItemsInTheme(themeID);

            if (itemsToPlace.Count == 0)
            {
                _logger.LogWarning($"主題 '{themeID}' 清單為空或是找不到！");
                // 就算沒有家具，地板已經生成了，所以還是回傳 bp
                return bp;
            }

            // 自動切分佈局
            IContainer rootContainer = CreateAutoSplitLayout(itemsToPlace);
            
            // 加入家具節點
            bp.nodes.AddRange(rootContainer.Resolve(roomBounds, null));

            return bp;
        }

        private IContainer CreateAutoSplitLayout(List<string> items)
        {
            if (items.Count == 1) return new ItemContainer(items[0], _library); // 這裡如果要用 RuleGen 也可以傳入

            int mid = items.Count / 2;
            var leftChild = CreateAutoSplitLayout(items.GetRange(0, mid));
            var rightChild = CreateAutoSplitLayout(items.GetRange(mid, items.Count - mid));

            // 這裡簡單用 System.Random 來決定切分方向
            bool splitVertical = new System.Random().Next(2) == 0;
            return new SplitContainer(leftChild, rightChild, splitVertical);
        }
    }
}