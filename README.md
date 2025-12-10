

# Unity Procedural Room Generator (PCG)

這是一個基於 **Clean Architecture** (整潔架構) 的 Unity 程序化房間生成系統。專案核心採用 **純 C# 邏輯層 (Core)** 與 **引擎實作層 (Adapter)** 分離的設計，實現了從宏觀佈局到微觀物品散佈的自動化生成。

目前版本：**v0.6.1 (Multi-room Walls & Doors)**

## 🌟 專案特色

*   **架構分離 (Decoupled Architecture)：**
    *   **Core Layer:** 純 C# 邏輯，負責計算藍圖 (`Blueprint`)、遞迴容器 (`Container`) 與生成策略 (`Strategy`)。不依賴 UnityEngine。
    *   **Adapter Layer:** 負責 Unity 實作，包含 ScriptableObject 設定檔讀取、`Physics.Raycast` 物理檢測與 GameObject 實例化。
*   **遞迴佈局 (Recursive Layout)：** 使用 `SplitContainer` 與 BSP (Binary Space Partitioning) 概念自動切分房間區域。
*   **連鎖反應 (Chain Reaction)：** 支援物件間的生成規則，例如「生成桌子後，自動在桌面上生成 2~5 個杯子」。
*   **智慧散佈 (Smart Scatter)：** 內建 `RandomScatterStrategy`，具備碰撞檢測與間距計算，防止物品重疊。
*   **物理落地 (Physics Snapping)：** 採用「射線檢測 (Raycast) + Collider 開關」技術，解決物件浮空或自我碰撞 (Bootstrap) 問題，確保家具完美貼合地面或桌面。
*   **數據驅動 (Data-Driven)：** 所有物品屬性、尺寸、生成規則皆透過 Unity `ScriptableObject` 設定。
*   **可選牆面生成：** 可針對每個房間的四個邊單獨關閉牆面，用於多房間共用牆。
*   **門框自適應：** 生成的門會依照牆厚與房高自動縮放，避免懸空或厚度不符。

## 📂 專案結構

```text
Assets/
├── Scripts/
│   ├── Core/                  # [純邏輯層 - 標準版]
│   │   ├── DataStructures.cs  # 基礎結構 (PropNode, SimpleBounds)
│   │   ├── Interfaces.cs      # 介面定義 (IPhysicsDriver, ILogger...)
│   │   ├── RoomGenerator.cs   # 生成器入口 (Director)
│   │   ├── Containers.cs      # 容器邏輯 (ItemContainer, SplitContainer)
│   │   ├── Strategies.cs      # 擺放策略 (RandomScatterStrategy)
│   │   ├── StructureGenerator.cs # 地板/牆壁生成器
│   │   └── RuleGenerator.cs   # 動態規則生成器
│   │
│   └── Adapters/
│       ├── Unity/             # [Unity 實作層]
│       │   ├── RoomBuilder.cs # 場景入口腳本 (支援匯入套件的備用生成器)
│       │   ├── LoggerAdapter.cs # 日誌適配器
│       │   ├── ItemLibraryAdapter.cs # 物品庫適配器
│       │   ├── ItemDefinition.cs # 物品設定檔 (ScriptableObject)
│       │   └── RoomTheme.cs      # 主題設定檔
│       │
│       └── Imported/          # [匯入套件適配層]
│           └── ImportedCoreMapper.cs # 類型對應器 (MyGame_1.Core → MyGame.Core)
│
├── Archive/
│   └── ImportedCoreBackup/    # [已封存的匯入套件核心] (供參考)
│       ├── RoomGenerator.cs   # 匯入的生成器 (MonoBehaviour 版本)
│       ├── RoomBlueprint.cs
│       ├── SimpleVector3.cs
│       ├── PropNode.cs
│       └── ILogger.cs
```

## 🚀 快速開始 (Getting Started)

### 1. 準備物件 (Setup Items)
在 `Assets/Create/PCG/ItemDefinition` 建立設定檔：
*   **Data_Floor:** 綁定地板 Prefab (Plane/Cube)。
*   **Data_Table:** 綁定桌子 Prefab。設定 `Min Bounds` (如 1.2, 1, 1.2) 以預留空間。設定 `Rules` (Target: Cup, Type: Scatter) 來觸發連鎖反應。
*   **Data_Cup:** 綁定杯子 Prefab。**重要：** 手動設定 `Logical Size` (如 0.3, 0.3, 0.3) 以避免重疊。

### 2. 設定主題 (Setup Theme)
在 `Assets/Create/PCG/RoomTheme` 建立主題：
*   **Theme_LivingRoom:** 在 `Required Items` 清單中填入想要生成的家具 ID (如 `Table`, `Table`)。

### 3. 設定場景 (Scene Setup)
1.  在場景中建立一個 Empty GameObject。
2.  掛上 `RoomBuilder` Component。
3.  **Database:** 拖入所有 `ItemDefinition` (Table, Cup, Floor)。
4.  **Theme Database:** 拖入 `RoomTheme`。
5.  **Room Size:** 設定房間大小 (例如 10, 2, 10)。

### 4. 生成 (Build)
*   在 `RoomBuilder` Component 上點擊右鍵 -> **Build**。
*   或是點擊 **Clear All** 清除場景。

### 5. 多房間串接 (LevelDirector 範例)
1. 在場景中建立一個空物件並掛上 `LevelDirector`，或直接使用已設好的 Prefab。
2. 將 `roomBuilderPrefab` 指向含有 `RoomBuilder` 的 Prefab（內部需先配置好 Database、Theme）。
3. 設定 `roomsToClear`、`roomSize` 與 `themeToBuild`，於 `LevelDirector` 上右鍵 **Generate Level**。
4. 系統會保留每個房間的東側牆並跳過西側牆，以避免共用牆缺失；門會隨機開在東側牆上並在同房間放入一把鑰匙。

### 選項：使用匯入套件的生成器
若要使用已封存的匯入套件版本（`MyGame_1.Core.RoomGenerator`）：
1.  在同一 GameObject 上再掛一個 `RoomGenerator` Component （位於 `Assets/Archive/ImportedCoreBackup/RoomGenerator.cs`）。
2.  點擊 `RoomBuilder` -> **Build**。系統會自動偵測並使用匯入版本，並透過 `ImportedCoreMapper` 自動轉換型別。

## 🛠️ 開發與除錯 (Debugging)

*   **Gizmos 視覺化：**
    *   **黃框：** 房間邊界。
    *   **紅框：** 程式認知的物品邏輯大小 (`Logical Size`)。如果紅框比模型小，請加大設定檔數值。
    *   **綠圈：** 散佈策略使用的安全間距。
*   **Console Log：**
    *   所有 Core 層的訊息會帶有 `[Core]` 前綴。
    *   若生成失敗，請檢查 Console 是否有 `[Error] ID not found` 或 `[Warning] Constraints failed` (空間不足)。

## 📚 架構說明 (Architecture Notes)

### Clean Architecture 分層
*   **Core Layer (`MyGame.Core`)：** 純 C# 邏輯，引擎無關，易於單元測試。包括生成器、容器、策略等核心演算法。
*   **Adapter Layer (`MyGame.Adapters.Unity` & `MyGame.Adapters.Imported`)：** 負責引擎整合與舊版本相容性。
    *   `Unity/` - 標準 Unity MonoBehaviour 實作與 ScriptableObject。
    *   `Imported/` - 類型轉換適配器，支援匯入套件無縫整合。

### 匯入套件整合策略
在匯入新套件時若遇到型別衝突，採用「適配器映射」而非直接替換：
1.  將衝突的舊版本封存至 `Assets/Archive/`（保留歷史參考）。
2.  在 `Assets/Scripts/Adapters/` 新增映射適配器（如 `ImportedCoreMapper`）。
3.  在場景入口（如 `RoomBuilder`）提供備用分支邏輯，自動偵測並適配舊版本組件。

這樣既消除編譯衝突，又保留漸進遷移的靈活性。

## 📝 版本歷程 (Changelog)

### v0.6.1 - Multi-room Walls & Doors (Latest)
*   **單邊牆控制：** `RoomBuilder`/`RoomGenerator`/`StructureGenerator` 可分別跳過南北東西牆，`LevelDirector` 預設保留東牆並跳過西牆，避免共用牆被吃掉。
*   **多房門/鑰匙佈局：** `LevelDirector` 會在東側牆隨機 Z 位置開門 (`DoorSystem`)，並於同一房間掉落一把 `Key`。
*   **門框對齊：** 門自動依牆厚及房高縮放，並貼齊地板，減少美術 Prefab 尺寸差異造成的縫隙。

### v0.6 - Architecture Consolidation
*   **整合匯入套件：** 將匯入的 `MyGame_1.Core` 套件整合至 Clean Architecture，移至 `Assets/Archive/ImportedCoreBackup/` 保留歷史。
*   **新增適配層：** `ImportedCoreMapper` 支援將匯入的舊版本類型無縫轉換至標準 `MyGame.Core` 型別。
*   **擴展 RoomBuilder：** 支援備用的匯入 `RoomGenerator` (MonoBehaviour 版本)，於同一 GameObject 時自動偵測並使用，並自動映射輸出。
*   **消除重複型別：** 刪除重複的 `MyGame_1.Core` 檔案，避免編譯衝突；保留匯入套件版本供參考。
*   **簡化依賴：** 使用 `ImportedCore` alias 避免命名空間衝突。

### v0.5 - Structure & Physics
*   新增 `StructureGenerator`：自動鋪設地板並修正 Y 軸高度。
*   修正 `RoomBuilder`：解決房間中心點浮空問題，將底部對齊 Y=0。
*   優化 `SnapToGround`：加入 Collider Toggle 機制，解決桌子射線打到自己而浮在空中的 Bug。

### v0.4 - Chain Reaction
*   實作 `ItemContainer` 連鎖反應，支援透過 Rule 生成子物件。
*   實作 `RandomScatterStrategy` 防重疊散佈。
*   引入 Tag 系統與 `RoomTheme`。

---

> 此專案展示了如何將軟體工程原則 (SOLID, DI) 應用於遊戲開發中，實現高彈性且易於測試的系統。
