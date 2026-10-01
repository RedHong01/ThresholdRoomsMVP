# Procedural race slice

`FrontRoomsRaceSpec` and `FrontRoomsRaceGenerator` are a data-only procedural layer for a future 2D/3D adapter. They do not replace `FrontRoomsLevel` yet, so the current playable builds and their fixed verification routes remain unchanged.

## Contract

- `Generate(int seed)` accepts the full signed 32-bit seed space and uses a local xorshift32 stream. The same seed produces the same JSON on both projects.
- Every spec has 8–12 main-route role nodes in this order: `Start` → `Shift` → `Office` → `Run` → `Exit`, with filler roles between them.
- Each spec adds branch nodes equal to 20–35% of the main route. Every branch is a two-edge detour that rejoins the next main node.
- The main route always includes one keyed `Door` decision and one loud `Window` decision. Branch edges are slower, quieter alternatives and expose their nominal time/noise costs.
- `FrontRoomsRaceValidation` checks counts, role ordering, edge continuity, branch ratio, required decisions, and race-time difference. It does not claim collision or human-playability validation because it is not connected to the current tile/room geometry.

## Editor harness

Use **FrontRooms → Race Slice → Verify 100 seeds** to write `Verification/race-slice-latest.json` in the project root. For a batch run:

```text
Unity -batchmode -projectPath <project> -executeMethod FrontRoomsRaceVerification.RunBatch -race-count 100 -race-seed 324508639 -quit
```

Optional `-race-output <absolute-path>` writes to a custom location. The report includes every generated node/edge and its validation metrics. The harness is Editor-only and never loads the existing gameplay scene.

## Integration order

1. Add semantic role references to `FrontRoomsLevel` (`ShiftGateA/B`, `KeyRoom`, `Door`, `RunWindow`, `ExitCenter`) and keep the current fixed constructor as a compatibility template.
2. Replace game-side opening indexes, hunter coordinates, and verification waypoints with those semantic references.
3. Add a tile-aware player validator before allowing a generated spec to drive geometry. The current race slice only validates its abstract graph.
4. Generalize 3D decoration to `FrontRoom.Interior` and `FrontOpening.WallIsVertical` before generating branches in more than one row.
