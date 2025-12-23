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
- **重要：不要把引用 `Downloaded` 內容的 prefab/scene commit 上去**，否則別台電腦 pull 後會缺檔。
- 推薦策略（Code 公開 / Art 私有）：
  - 第三方美術放在「私有 Art repo」，每台電腦把它同步到專案內的 `Assets/ThirdParty/Downloaded/`（包含 `.meta`）。
  - code repo 內永遠保留可運作的 fallback（sphere）版本；私有 repo 存在時才自動替換外觀。
- Key 美術覆蓋（不修改 `Assets/Prefabs/Key.prefab`）：
  - 私有 repo 放置：`Assets/ThirdParty/Downloaded/RoomBuilder2Art/Resources/RoomBuilder2Overrides/KeyArt.prefab`
  - 執行時 `KeyController` 會 `Resources.Load("RoomBuilder2Overrides/KeyArt")`，存在就取代 fallback 外觀。
  - 產生/更新 override：`Tools/Art/Build Key Art Override (...)`
