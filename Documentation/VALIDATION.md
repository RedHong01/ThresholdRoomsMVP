# FrontRooms 2D — Validation

Checked September 30, 2026 (America/Los_Angeles), Unity **6000.3.10f1**. This record covers the 2D prototype only; it does not establish the status of the separate first-person 3D version.

## Build and automated evidence

- **macOS build succeeded, 0 errors, 105.2 MB reported by Unity.** [Build log](../Verification/build-mac.log). The executable in [FrontRooms.app](../Builds/Mac/FrontRooms.app) is a universal binary containing **arm64 and x86_64**; both architectures were inspected, but execution on an Intel Mac has not been verified.
- **8/8 deterministic level checks passed.** [JSON report](../Logs/FrontRooms/level-verification.json): five-room route, all exit kinds, unlocked reachability, landmarks, door/window passability, sealed halls, wall sight, and diagonal corner blocking. These are level-data and pathfinding checks, not input or visual tests.
- **Three subsequent headless logic routes passed.** Each JSON explicitly identifies scripted player simulation. The pilot supplies movement and action intent directly, so it does not validate keyboard or mouse input.

| Scripted route | Result | Time | Notes / keys | Glass broken | Hunter doors broken | Room transitions / shifts | Evidence |
| --- | --- | ---: | --- | ---: | ---: | --- | --- |
| Read notes and use the keyed door | Escaped, pass | 28.72 s | 3 / 1 | 1 | 1 | 4 / 3 | [Result](../Verification/logic-door/result.json) |
| Skip notes and use glass | Escaped, pass | 13.27 s | 0 / 0 | 2 | 0 | 4 / 1 | [Result](../Verification/logic-fast/result.json) |
| Remain at the start | Caught, pass | 6.59 s | 0 / 0 | 0 | 0 | 0 / 1 | [Result](../Verification/logic-caught/result.json) |

These timings describe the scripted routes. They are not player completion times, evidence of fun, or a measured 8–12 minute experience. Earlier images in `Verification/door` and `Verification/door-final` predate the simplified HUD and must not be presented as current UI screenshots.

## Interactive verification and controls

After simplifying the UI, native checks confirmed the **title screen, pause menu, and C control-mode switch**. The normal display now keeps the room name, hunter distance and one contextual prompt. Full controls are in the Esc pause menu; diagnostic state text requires F1.

A fresh ordinary launch waits for **Space / Enter** and uses manual input. The scripted pilot starts only when launched with an explicit `-autotest <folder>` argument. The delivered app should be opened normally, without that test argument.

| Input | Action |
| --- | --- |
| WASD / arrows; Shift | Move; run. |
| Hold Q near a white note | Stop and read for approximately 1.5 seconds. |
| Hold E near glass | Break the window. |
| C after starting | Switch between keyboard and cursor controls. |
| Cursor mode: hold left / right mouse | Move toward the pointer / break nearby glass. |
| Tab | Map and collected notes; the game continues. |
| Esc; R from pause or results | Pause / resume; restart. |

Real movement and note reading through native input **remain unverified**: the interactive attempt was interrupted by changes in application focus. **Mouse movement and mouse interaction have not been validated.** The headless passes above do not close those gaps. Audio perception, a complete human-operated run, Windows execution and player comparisons also remain unverified.

## Assignment completion boundary

The macOS executable and automated evidence are available. **Diary Entry #2 remains incomplete because actual classroom pitch feedback has not been supplied.** The [prototype brief](PROTOTYPE_BRIEF.md) separates implementation observations and this session's UI feedback from the missing classroom feedback. Do not invent feedback, player reactions, or personal learning claims to fill that requirement.

## First-person 3D comparison

The separate project at `/Users/redwang/Developer/FrontRooms3DMVP` now has a successful macOS build at `Builds/Mac/FrontRooms3D.app` (104,327,305 bytes; universal arm64/x86_64). I opened the title screen and started the Lobby manually; the first-person view visibly contains 3D walls, floor, ceiling, lights, note geometry and a compact room/distance/crosshair HUD. Repeated native W input visibly moved the view toward the note, and the unattended manual run reached the Caught result. The 3D headless route harness is still unreliable under batch mode, so it is not represented as a passing route suite.
