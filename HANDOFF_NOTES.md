## Context / Current State

- Project: Unity procedural room generator (`RoomBuilder2`) using Container-first blueprint generation.
- Unity: 6000.0.62f1 (Windows).
- Current active branch (as of v0.6.6): `refactor/container-review`.
- Goal: single-room generation is stable and testable with **fallback primitives** even when no art is provided.

## Architecture Snapshot

- Core logic: `Assets/Scripts/Core/` (no Unity API)
  - `RoomGenerator` produces `RoomBlueprint` from rules/themes.
  - `RoomBlueprint` can be a container tree; Unity adapter flattens it for spawning.
- Unity adapter: `Assets/Scripts/Adapters/Unity/`
  - `RoomBuilder` instantiates prefabs, applies container sizing, and performs snapping (floor + parent surface only for physical parents like Cup→Table).
  - `DoorSystem` uses a hinge child to guarantee correct pivot; `DoorArtLoader` supports slot-based art with fallback primitives.

## Testing / Tooling

- Spec: `Docs/SPEC.md`
- Tests:
  - EditMode: `Assets/Tests/Editor`
  - PlayMode: `Assets/Tests/PlayMode`
- One-click runner + report export:
  - `Tools/Tests/Run * and Export Report`
  - `Tools/Tests/Run All (Edit + Play + Player) and Export Reports`
  - Outputs: `TestReports/` (ignored by `.gitignore`)
- Debug utilities:
  - `Tools/Tests/Copy AllTestsRunner Logs (Last 400 Lines)` / `Copy Unity Editor Log Tail`
  - If stuck: `Tools/Tests/Force Clear Active Run Lock (Stuck Fix)`
  - If scene left in temp/Untitled: `Tools/Tests/Restore Last Captured Scenes Now`

## Key Fixes (recent)

- Snapping: walls/floor/door/window snap to computed floorY; small props snap to **immediate physical parent surface** (prevents “stacking up” floating walls).
- Door stability: hinge-based rotation avoids “flip direction after toggles” and pivot drift; door opens away from player side in tests.
- AllTestsRunner: handles temp-scene isolation + cleanup phases; avoids duplicated runs/finish dialogs; supports restoring the user’s original scene.
- MouseLook: default sensitivity set to `900` with deterministic override input for tests.

## Current Behavior Guarantees (Phase 1)

- Works without external art: missing art uses fallback primitives so structure/scale/interaction remain visible and testable.
- Single-room invariants validated by tests:
  - Door opens away from player side
  - Cup ends on table top
  - Focus highlight always visible (outline or fallback)

## Planned Next Step (Phase 2, not implemented yet)

- **Reskin / ArtSet**: keep the same `RoomBlueprint` and rebuild scene with different visual sets.
- Proposed precedence: explicit item override > ArtSet mapping > ItemDefinition default > fallback primitive.

## Files to open first

- Spec: `Docs/SPEC.md`
- Scene build + snapping: `Assets/Scripts/Adapters/Unity/RoomBuilder.cs`
- Door system: `Assets/Scripts/Adapters/Unity/DoorController.cs`, `Assets/Scripts/Adapters/Unity/DoorArtLoader.cs`
- Test runner: `Assets/Scripts/Editor/AllTestsRunner.cs`
- PlayMode specs: `Assets/Tests/PlayMode/SceneGenerationSpecsTests.cs`, `Assets/Tests/PlayMode/PlayerInteractionSpecsTests.cs`

## ThirdParty Art (跨電腦注意)

- 下載型第三方資產請放在 `Assets/ThirdParty/Downloaded/`（此資料夾被 `.gitignore` 排除，不會被 push）。
- **重要：不要把引用 `Downloaded` 內容的 prefab commit 上去**（例如 `Assets/Prefabs/Key.prefab` 若已套用 Rust Key 外觀），否則別台電腦 pull 後會缺檔。
- 目前策略：repo 內永遠保留可運作的 fallback（cube）版本；想在另一台電腦看到同樣美術，請把第三方資產「另外同步」到那台電腦。
- 建議同步方式（擇一）：
  - **直接複製整個 `Assets/ThirdParty/Downloaded/`（包含 `.meta`）** 到另一台電腦同一路徑（Unity 依賴 GUID，`.meta` 很關鍵）。
  - 或用 Unity 的 `Assets > Export Package...` 把 Rust Key 資產匯出成 `.unitypackage`，到另一台電腦 `Import Package`（也會保留 GUID）。
- 安裝 Key 美術：在 Unity 執行 `Tools/Art/Install Rust Key Art (ThirdParty Downloaded)`（如果沒裝第三方包，仍會使用 fallback key，不會壞）。
