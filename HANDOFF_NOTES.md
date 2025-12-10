## Context / Current State

- Project: Unity procedural room generator (`RoomBuilder2`).
- Recent issue: connecting rooms creates overlapping walls/doors. Ongoing fixes in `Assets/Scripts/Adapters/Unity/LevelDirector.cs` and `RoomBuilder.cs`.

## Key Changes Made

- Moved Unity scripts under `Assets/Scripts/Adapters/Unity/`; Core remains in `Assets/Scripts/Core/`.
- Prefabs consolidated into `Assets/Prefabs/`.
- Added `Assets/Scripts/Core/BlueprintPostProcessor.cs` for core-side wall/door overlap cleanup.
- In `RoomBuilder.cs`: logs for `RemoveDoorWallOverlaps`, door auto-resize aligned to wall height, uses prefab bounds first.
- In `LevelDirector.cs`: multiple iterations to avoid double walls/doors. Current logic:
  - When connecting Room A (+X wall) to Room B (-X wall):
    - Detect walls on both planes; keep the side that has walls (prefers A if both or both empty).
    - Remove all walls on the other side.
    - Remove one wall segment on the kept side to place a single Door node (only in kept room).
    - Adds a Key to the kept room.
  - Epsilon for wall matching currently 0.5.
  - Logs show `keepA/keepB`, removed counts.

## Outstanding Issue

- Room B ends up missing the shared wall after the connect step (no double wall, but B has no wall). Need a shared-wall approach or ensure B still has a wall surface.
- Idea: generate a shared wall prefab/segment at the boundary instead of removing B's wall; or skip generating B's wall initially if A has one (pre-check before wall generation).

## Useful Logs

- `RoomBuilder`: `PostProcess RemoveDoorWallOverlaps: doors=..., wallsBefore=..., wallsAfter=..., removed=...`
- `LevelDirector`: logs like `Connecting Room_0<->Room_1: keepA=true, removedAOne=..., removedBAll=...`

## Next Steps (suggested)

- Implement shared-wall generation: remove both side wall nodes on the shared plane, then add a single shared wall (with door cut) anchored to one room.
- Alternatively, pre-check during wall generation to avoid duplicating walls on opposite planes (e.g., core-level rule: only one room generates +X wall when adjacent room exists).
- Reduce epsilon after stable targeting; ensure wall itemID filter matches actual wall IDs (currently checks `Contains("Wall")`).

## Files to open

- `Assets/Scripts/Adapters/Unity/LevelDirector.cs`
- `Assets/Scripts/Adapters/Unity/RoomBuilder.cs`
- `Assets/Scripts/Core/BlueprintPostProcessor.cs`
