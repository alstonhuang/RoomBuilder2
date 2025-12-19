# Specification-Driven Development (SDD) — RoomBuilder2

本文件定義專案的「規格與不變條件（invariants）」；實作與測試必須以此為準。
測試不做 layout 逐一比對，而是驗證規則是否被違反。

## 範圍（Phase 1：單一房間）
- 只涵蓋 single-room 的 Blueprint 與 Scene 生成。
- 測試分層：
  - **EditMode（Core）**：只驗證 Blueprint / Container / Rules，不依賴 Prefab / Physics。
  - **PlayMode（Unity Adapter）**：驗證場景生成、Physics snapping、互動、focus 高亮。

## 容差（Tolerances）
- `ε_snap = 0.01m`：貼齊判定容差（落地/落桌）
- `ε_closeAngle = 1°`：門關閉角度容差

## 規格（Phase 1）

### BP-1 生成正確藍圖（Core）
Given
- 一個房間 bounds（`SimpleBounds`）
- 一個 generation theme（例如 `LivingRoom`）

When
- `RoomGenerator.GenerateFromTheme(...)`

Then（invariants）
- `RoomBlueprint.nodes` 非空，且至少包含：
  - Floor 類（例如 `FloorTile`）
  - Wall 類（例如 `Wall`）
  - theme 需要的家具/道具（例如 `Table`、`Cup`）
- `instanceID` 必須唯一
- `parentID` 只能為空或指向同 blueprint 內存在的 `instanceID`
- 不允許形成 parent cycle
- `logicalBounds.size` 為「容器語意大小」，可用於 Adapter 端 fit / snapping 決策

### SC-1 生成正確場景（Unity Adapter）
Given
- 一份 Blueprint（single room）
- 一組 ItemDefinition（允許 placeholder/fallback，不依賴外部美術）

When
- `RoomBuilder.BuildFromGeneratedBlueprint()`

Then（invariants）
- **永遠可見（fallback）**
  - 即使缺少美術/Prefab/材質，也必須以 fallback primitives 生成可見物件（用於結構驗證與測試）
- **落地/落桌（ε_snap）**
  - Table 底部貼齊 floor 表面（within `ε_snap`）
  - Cup 底部貼齊 table top（within `ε_snap`），且 XZ 投影落在桌面 bounds 內
- **Door**
  - Door leaf collider 不可為 trigger（未開門不可穿過）
  - Door 關閉角度可回到 closeAngle 容差 `ε_closeAngle`
- **容器到實體**
  - 生成時允許多層容器，但只有末端才落到「實體物件容器」並產生可見 GameObject（避免中間層拿到不必要的 scale/renderer/collider）

### INT-1 互動一定有 focus 顯示（Unity Adapter）
Given
- 玩家視線 Raycast 命中 Interactable（Key / Door…）

When
- `PlayerInteraction` 更新 hover

Then（invariants）
- 必須滿足其一：
  - `Outline.enabled == true`（QuickOutline）
  - fallback highlight 生效（材質/PropertyBlock 的可觀測變化）

### PL-1 人物可移動轉頭且互動（Unity Adapter）
Phase 1 最小驗收
- 玩家生成後不懸空、可落地
- 可轉頭（MouseLook）且可互動（E）

## Seed 與「無限生成」的測試策略
- 測試不做 layout 一一比對，而是驗證 invariants 不被破壞。
- 仍建議支援 seed（或可注入 RNG），用途：
  - fuzz 測試時可重現（輸出 seed + 參數）
  - 減少 flaky：固定少數 seeds + 額外隨機 seeds

## Phase 2（只換美術不改布局：Reskin / ArtSet）
目標：同一份 `RoomBlueprint`（容器關係不變），只切換 Theme/ArtSet 就能重建外觀（同房間可不停輪換不同美術）。

### 設計拆分（建議）
- **Generation Theme（RoomTheme）**：影響「生成什麼、怎麼放」（權重、規則、偏好）。
- **Visual Theme（ArtSet）**：影響「長什麼樣」（itemID → prefab/parts/material），不改動 Blueprint 的容器關係。

### VT-1 同一 blueprint 可 reskin
Given
- 一份已生成的 `RoomBlueprint bp`（single room）
- 兩套以上的 `ArtSet`（例如 `Modern` 與 `SciFi`）

When
- 使用相同 `bp` rebuild 場景，但套用不同 `ArtSet`

Then（invariants）
- 容器關係不變（同 `instanceID` 的拓撲、parent/child 關係不變）
- 位置/旋轉/尺寸語意不變（仍需滿足 Phase 1 的 snapping/door/interaction invariants）
- 允許外觀不同（mesh/material/prefab 不同）

### VT-2 覆寫優先序（避免被整套 Theme 蓋掉）
建議優先序（高 → 低）：
1. 明確的 item override（例如某個 itemID 的強制指定）
2. `ArtSet` mapping（整套風格）
3. `ItemDefinition.prefab` 預設
4. fallback primitive（保底可見）

### VT-3 Slot-based 資產契約（以 DoorSystem 為例）
DoorSystem 最低契約（美術照此交付即可替換）：
- 必須能定位：
  - `Frame_Left` / `Frame_Right` / `Frame_Top`
  - `DoorHinge`（旋轉軸）
  - `Door`（門片）
- pivot 規則：
  - `DoorHinge` pivot 在鉸鏈邊（避免門從中間旋轉）
- 尺寸規則：
  - 允許 Adapter 端依 wall opening 自動調整 frame/door 的寬高與厚度

### VT-4 使用者更換美術的操作（驗收）
- 可「整套換」：只切換 `ArtSet`，並 rebuild（不需要改 Core / Rules）
- 可「單項換」：只覆寫某一個 itemID 的 prefab/parts，不影響其他 item

## 測試位置與入口
- 規格：`Docs/SPEC.md`
- EditMode tests：`Assets/Tests/Editor`
- PlayMode tests：`Assets/Tests/PlayMode`
- Unity Test Runner：`Window > General > Test Runner`
- 一鍵執行（Editor menu）：
  - `Tools > Tests > Run All (Edit + Play) and Export Reports`
  - `Tools > Tests > Run All (Edit + Play + Player) and Export Reports`
