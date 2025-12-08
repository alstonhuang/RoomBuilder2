using System.Collections.Generic;

namespace MyGame.Core
{
    public class RoomGenerator
    {
        private readonly ILogger _logger;
        private readonly IItemLibrary _library;

        public RoomGenerator(ILogger logger, IItemLibrary library)
        {
            _logger = logger;
            _library = library;
        }

        public RoomBlueprint GenerateFromTheme(SimpleBounds roomBounds, string themeID)
        {
            _logger.Log($"Director: 開始根據主題 '{themeID}' 生成...");
            var bp = new RoomBlueprint();

            List<string> itemsToPlace = _library.GetItemsInTheme(themeID);
            if (itemsToPlace.Count == 0)
            {
                _logger.LogError($"主題 '{themeID}' 清單為空！");
                return bp;
            }

            IContainer rootContainer = CreateAutoSplitLayout(itemsToPlace);
            bp.nodes = rootContainer.Resolve(roomBounds, null);
            return bp;
        }

        private IContainer CreateAutoSplitLayout(List<string> items)
        {
            if (items.Count == 1) return new ItemContainer(items[0], _library);

            int mid = items.Count / 2;
            var leftChild = CreateAutoSplitLayout(items.GetRange(0, mid));
            var rightChild = CreateAutoSplitLayout(items.GetRange(mid, items.Count - mid));

            bool splitVertical = new System.Random().Next(2) == 0;
            return new SplitContainer(leftChild, rightChild, splitVertical);
        }
    }
}