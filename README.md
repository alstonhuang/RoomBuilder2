# RoomBuilder2 (Unity PCG Room Generator)

這是一個以 **Clean Architecture** 思路拆分的 Unity 專案：
- `Assets/Scripts/Core`：純 C# 邏輯（Blueprint / Container / Strategy / Rules），不依賴 Unity。
- `Assets/Scripts/Adapters/Unity`：把 Blueprint 實體化為 Unity GameObjects，處理 Prefab、Collider、Raycast、互動等。

## 目標架構（Container-first）

- `RoomBlueprint` 只描述「容器」的階層與相對關係（相鄰/堆疊/朝向/尺寸規則…）。
- 容器可以再裝容器（Region / Wall / Floor / Door / Window / Table / Chair...）。
- 最末端節點才會落到「可見/可互動的實體物件」；實體會依容器的尺寸規則自動 fit。

## 快速開始（Editor）

1. 打開場景後選到帶 `RoomBuilder` 的物件。
2. 在 Inspector 填好：
   - `database`：`ItemDefinition` 列表（Floor / Wall / DoorSystem / Table / Cup / Key...）
   - `themeDatabase`：`RoomTheme` 列表
3. 使用右鍵/齒輪 Context Menu：
   - `Generate Blueprint`
   - `Build from Generated Blueprint`
   - 或 `Build`（一次做完）

## 互動

- 玩家視線對準可互動物件會有 focus 高亮：
  - 有 QuickOutline 時：顯示 outline
  - 沒有/失效時：使用 fallback highlight（改材質顏色）確保一定看得到
- `E`：互動
- `Key`：撿起後可解鎖門

## DoorSystem（門框 + 門板）

- `DoorSystem` 使用 `DoorArtLoader` 在 slot 內掛載美術；缺美術時會生成 fallback primitives 方便看尺寸/結構。
- `DoorController`：
  - 旋轉軸固定在 hinge，不會再因重複開關而翻向/累積漂移
  - 開門方向只在第一次決定並鎖定（避免「第一次向內、第二次向外」）

## Troubleshooting

- Missing/ Broken GUID refs（例如 `Broken text PPtr`）：
  - 使用 `Tools/Project/Audit Missing GUID References` 產生報告：`Temp/GuidAuditReport.txt`
- QuickOutline：
  - 材質需位於 `Assets/QuickOutline/Resources/Materials/OutlineMask.mat` 與 `OutlineFill.mat`

## Changelog

### v0.6.5
- 容器支援深層嵌套，生成時以 parent-local 組裝，並用 `__Content` 隔離父層 scale，避免多層 scale 造成尺寸/位置錯誤
- Physics snapping 修正：floor 高度只以 floor 自身 bounds 計算，避免把牆/家具算進去導致整批懸空；子物件（杯子）可正確落在桌上
- DoorController 修正：方向決策固定、pivot offset 不再累積，連續開關不會翻向/亂轉
- Focus highlight 強化：outline 失效時仍有 fallback highlight 確保可見

