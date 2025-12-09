using UnityEngine;

namespace MyGame_1.Core
{
    public class RoomGenerator : MonoBehaviour
    {
        public RoomBlueprint GenerateStackDemo()
        {
            Debug.Log("開始生成雙杯測試...");

            var bp = new RoomBlueprint();

            // 1. 桌子 (Root)
            var tableNode = new PropNode
            {
                instanceID = "inst_table",
                itemID = "Table",
                parentID = null,
                position = new SimpleVector3(0, 0, 0),
                rotation = SimpleVector3.Zero
            };
            bp.nodes.Add(tableNode);

            // 2. 第一個杯子 (放在桌子左邊一點)
            var cup1 = new PropNode
            {
                instanceID = "inst_cup_1", // ⚠️ ID 必須唯一
                itemID = "Cup",
                parentID = "inst_table",
                // X = -0.3，讓它往左移一點，不然會跟第二個杯子重疊
                position = new SimpleVector3(-0.3f, 0, 0), 
                rotation = SimpleVector3.Zero
            };
            bp.nodes.Add(cup1);

            // 3. 第二個杯子 (放在桌子右邊一點)
            var cup2 = new PropNode
            {
                instanceID = "inst_cup_2", // ⚠️ ID 必須唯一，不能跟上面一樣
                itemID = "Cup",            // 這裡可以填 "Cup" 也可以填第二種杯子的 ID
                parentID = "inst_table",
                // X = 0.3，讓它往右移
                position = new SimpleVector3(0.3f, 0, 0), 
                rotation = SimpleVector3.Zero
            };
            bp.nodes.Add(cup2);

            return bp;
        }
    }
}