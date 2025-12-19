# Specification-Driven Development (SDD) — RoomBuilder2

本文件是本專案的「規格真相來源」。實作與測試必須以此為準；我們不做「一一比對特定 layout」，而是用 **invariants（不變條件）** 驗證無限生成下仍不違反規則。

## 範圍（Phase 1）

- 僅涵蓋 **單一房間**（single room）。
- 測試分為：
  - **EditMode（Core）**：只驗證 Blueprint/容器/規則的不變條件（不依賴 Prefab、Physics）。
  - **PlayMode（Unity Adapter）**：驗證生成後場景與互動的不變條件（含 Physics/Raycast）。

## 名詞與定義

### 容差（Tolerances）

- `ε_snap = 0.01m`：貼合判定容差（落地/桌面貼合）
- `ε_closeAngle = 1°`：門關閉角度容差

### Door（玩家側決定）

- Door 在「從關門狀態觸發開門」時，必須依 **玩家位於門平面哪一側** 決定開門方向，使門板「往玩家不在的那側」開。
- 方向決策必須遵守：
  - 在同一次開/關過程中不可翻向（避免抖動/漂移）
  - 僅允許在「關門狀態」重新決定方向（避免半開/半關時重算造成翻向）
  - 當門回到「關門狀態」後，下次互動可以重新依玩家所在側重新決定方向

> 門平面法線（Door plane normal）來源：
> 1) 優先使用牆容器提供的法線（結構資訊）
> 2) 若沒有牆資訊，使用門的 marker（例如 `DoorPlaneMarker`）
> 3) 再不行才退到 `doorRoot.transform.forward`

### TableTop（桌面）

桌面高度來源：
1) 優先使用 `TableTopMarker`（由資產定義桌面平面/區域）
2) 若沒有 marker，退回桌子 Collider/Renderer 的 bounds（近似桌面）

## 規格

### BP-1 生成正確藍圖（Core）

Given：
- 一個房間 bounds（`SimpleBounds`）與 theme

When：
- `RoomGenerator.GenerateFromTheme(...)`

Then（invariants）：
- `RoomBlueprint.nodes` 非空，且至少包含：
  - Floor 類（例如 `FloorTile`）
  - Wall 類（例如 `Wall`）
  - theme 指定的家具（例如 `Table`）
  - 規則衍生物件（例如 `Cup` 由 `Table` 規則生成）
- `instanceID` 唯一（nodes 範圍）
- parent 參照必須合法：
  - `parentID` 為 null/empty 或指向 nodes 中存在的 `instanceID`
  - parent 關係不得形成 cycle
- 牆節點朝向一致（`Facing` 與 `rotation.y` 對應）：
  - South→0°、West→90°、North→180°、East→270°
- **局部放置語意**：
  - 由規則以 Fixed/Local-offset 放置的子物件，其 `logicalBounds.size` 必須為 `default`（零），以表示 `position` 是 parent-local offset

### SC-1 生成正確場景（Unity Adapter）

Given：
- 一個 Blueprint（單房）
- 一組 ItemDefinition（可為 placeholder/fallback，不依賴外部美術）

When：
- `RoomBuilder.BuildFromGeneratedBlueprint()`

Then（invariants）：
- **落地/落桌貼合**（`ε_snap`）：
  - Table 會落在 floor 上（table bottom ≈ floor surface）
  - Cup 會落在 table 上（cup bottom ≈ table top，且 XY 投影落在桌面 bounds 範圍內）
- Door：
  - Door leaf collider 不可為 trigger（關門時不可穿透）
  - 連續開關不產生旋轉漂移（角度回到 closeAngle 容差 `ε_closeAngle` 內）

### INT-1 互動一定有 focus 顯示（Unity Adapter）

Given：
- 玩家視線 Raycast 命中 Interactable（Key / Door 等）

When：
- PlayerInteraction 更新 hover

Then：
- 至少滿足其一：
  - `Outline.enabled == true`，或
  - fallback highlight 生效（例如 MaterialPropertyBlock 或材質顏色可觀測變化）

### PL-1 人物能移動/轉頭且互動（Unity Adapter）

Phase 1（單房）最小可驗收：
- 玩家生成後受重力影響會往下落並落到地面（不懸空、不卡住）
- 玩家視線方向改變會影響 Raycast 命中目標
- 可觸發互動（撿 key、開門）

## Seed 與無限生成（測試策略）

- 測試不做「layout 一一比對」，而是用 invariants 驗證規則不被違反。
- 仍建議支援 seed/可注入 RNG（或至少能記錄 seed），用途是：
  - fuzz 測試發現違規時可重現（輸出 seed + 參數）
  - 避免 flaky：固定 seeds 做回歸 + 額外隨機 seeds 做壓力測試

## 測試位置與執行

- 規格：`Docs/SPEC.md`
- EditMode tests：`Assets/Tests/Editor`
- PlayMode tests：`Assets/Tests/PlayMode`

Unity Editor：
- `Window > General > Test Runner`
  - `EditMode`：跑 `RoomBuilder2.EditModeTests`
  - `PlayMode`：跑 `RoomBuilder2.PlayModeTests`

一鍵執行（Editor menu）：
- `Tools > Tests > Run All (Edit + Play) and Export Reports`
- `Tools > Tests > Run All (Edit + Play + Player) and Export Reports`（會建置並執行 Player tests，較慢）
