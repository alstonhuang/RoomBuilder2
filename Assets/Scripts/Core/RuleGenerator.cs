using System.Collections.Generic;

namespace MyGame.Core
{
    // 這是專門負責 "動態產生規則" 的大腦
    // 目前我們先讓它回傳空清單，因為現在主要還是靠 Unity Inspector (ItemDefinition) 設定規則
    public class RuleGenerator
    {
        public RuleGenerator() { }

        public List<CoreGenerationRule> GenerateRules(string themeID, string hostItemID, string hostTag)
        {
            // 暫時回傳空，未來可以在這裡寫 code 控制生成邏輯
            return new List<CoreGenerationRule>();
        }
    }
}