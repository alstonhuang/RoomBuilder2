# RoomBuilder2 (Unity PCG Room Generator)

這是一個以 **Clean Architecture** 為導向的 Unity 專案：
- `Assets/Scripts/Core`：純 C# 邏輯（Blueprint / Container / Strategy / Rules），不依賴 Unity API
- `Assets/Scripts/Adapters/Unity`：把 Blueprint 實作成 Unity GameObjects（Prefab、Collider、Raycast、互動等）

## 設計原則（Container-first）
- `RoomBlueprint` 只描述「容器」的樹狀結構與相對關係（相鄰/堆疊/朝向/尺寸規則…）。
- 容器可以巢狀（Region / Wall / Floor / Door / Window / Table / Chair…）。
- 只有最末端節點才落到「實體物件容器」，並在生成時讓實體物件自動 fit 容器尺寸。

## 快速開始（Editor）
1. 開啟場景後選取掛有 `RoomBuilder` 的物件
2. 在 Inspector 填好：
   - `database`：`ItemDefinition` 列表（Floor / Wall / DoorSystem / Table / Cup / Key…）
   - `themeDatabase`：`RoomTheme` 列表
3. 右鍵 Context Menu：
   - `Generate Blueprint`
   - `Build from Generated Blueprint`
   - 或 `Build`（一次做完）

## 互動
- 視線瞄準可互動物件會顯示 focus 高亮：
  - 有 QuickOutline 時使用 outline
  - 缺少材質時使用 fallback highlight（改材質顏色）確保一定看得見
- `E`：互動
- `Key`：撿起後可解鎖門

## DoorSystem（框 + 門片）
- `DoorSystem` 使用 `DoorArtLoader` 以 slot 載入美術；缺少美術時用 fallback primitives 方便看尺寸結構
- `DoorController`
  - 旋轉軸固定在 hinge
  - 開門方向只在第一次決定並鎖定（避免第二次變向/越轉越歪）

## 測試（Specification-Driven）
- 入口：`Tools/Tests/*`
  - `Run EditMode / PlayMode / Player` 單獨跑
  - `Run All (Edit + Play + Player) and Export Reports` 一鍵跑全套並輸出 XML 報告
- 報告輸出：預設 `TestReports/`
- 建議開啟：`Tools/Tests/Run In Temporary Scene (Avoid Modifying Current Scene)`（避免測試污染你正在編輯的場景）

## 操作手感
- `MouseLook.mouseSensitivity` 預設 `900`（可在 `Assets/Prefabs/Player.prefab` 調整）

## Troubleshooting
- QuickOutline 材質位置：
  - `Assets/ThirdParty/Vendored/QuickOutline/Resources/Materials/OutlineMask.mat`
  - `Assets/ThirdParty/Vendored/QuickOutline/Resources/Materials/OutlineFill.mat`
- 測試工具卡住（顯示 A test run is already active）：
  - `Tools/Tests/Force Clear Active Run Lock (Stuck Fix)`
  - 或 `Tools/Tests/Restore Last Captured Scenes Now` 強制回復原本場景

## Changelog

### v0.6.6
- AllTestsRunner：一鍵跑 Edit/Play/Player 並輸出 XML 報告，並加入卡住修復與場景回復工具
- 新增 SDD PlayMode 測試：門向外開/杯子落桌面/互動 focus 等規格驗證
- MouseLook：預設靈敏度調整為 `900`，並加入可測的 override input（供測試使用）

### v0.6.5
- 容器可支援深層嵌套，最終以 parent-local 組合，並用 `__Content` 隔離上層 scale，避免多層 scale 造成尺寸/位置錯誤
- Physics snapping 修正：floor 高度只以 floor 自身 bounds 計算，避免把家具算進去導致整批浮空；子物件（杯子）可正確落在桌面上
- DoorController 修正：方向決策固定、pivot offset 不會累積，避免開門翻轉/越轉越歪
- Focus highlight 強化：outline 缺材質時用 fallback highlight 確保可視
