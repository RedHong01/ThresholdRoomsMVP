# FrontRooms — Functional Prototype

This week's prototype asks: **How much information is worth the seconds it costs to collect?** Read a room's rule while the hunter approaches, or use those seconds to escape. The short route is **Lobby → Level 0 → Level 4 → Level ! → Exit**, with shifting halls, a keyed office door, a noisy window alternative, and three notes that remain on the map.

The design guidance is the [FrontRooms deck](https://www.figma.com/deck/NmYGRYKlhfX6H4rbJ7QcSN). This is the five-cell functional slice; the deck's 8–12 minute experience remains a later scope target.

## Play or open the current project

- macOS app: `Builds/Mac/FrontRooms.app` — open it and press **Space** to start.
- Working project: this folder, or the editable copy in the assignment folder at ThresholdRoomsMVP/.
- Unity Editor: **6000.3.10f1**.
- Current scene: `Assets/Scenes/FP_FrontRooms.unity`.
- Build again with **FrontRooms → Build macOS** in Unity. `FrontRoomsBuild.BuildMac` runs the level verification before building.
- Optional Windows build: first install **Windows Build Support** for this Editor version through Unity Hub; that module is currently absent. Then use **FrontRooms → Build Windows**.

The macOS build succeeded with a reported size of **105.2 MB**. Its executable is a verified universal binary containing **arm64 and x86_64**. Runtime checks and level verification are documented in the [validation report](Documentation/VALIDATION.md).

## Controls and handoff

**WASD** moves, **Shift** runs, **hold E** breaks nearby glass, and **hold Q** reads a nearby white note. During play, **C** switches to cursor movement: hold left mouse to move and right mouse to break glass. **Tab** shows the map and pinned notes, **Esc** opens pause and controls, **R** retries from pause or results, and **F** toggles fullscreen. Keys are picked up on contact; the matching door opens when approached.

The 2D interface keeps only the room name, hunter distance, and one relevant interaction prompt during play. Controls live in the **Esc** menu. The title waits for **Space** to start; **C** does not change controls there. Results show **Escaped** or **Caught**, elapsed seconds and notes collected. The log still distinguishes an informed escape (three notes plus the office key) from a fast escape as a small proxy for the deck's rich escape goal.

- [Prototype brief](Documentation/PROTOTYPE_BRIEF.md): Chinese design explanation, English critique preparation, and the incomplete Diary #2 draft.
- [Validation report](Documentation/VALIDATION.md): build and runtime evidence, with remaining limitations.

The assignment requires the interactive build **and Diary Entry #2**. Actual pitch feedback still needs to be supplied by the designer; the diary draft does not invent classroom feedback.

## Current design feedback and upcoming comparison

In this session, the designer asked for a simpler interface and a first-person 3D version with a feel closer to *Dark Deception* and *Escape the Backrooms*. The 2D UI has been simplified. The [FrontRooms3DMVP comparison project](../FrontRooms3DMVP) is a separate first-person Unity project. Its purpose is to compare room reading and pursuit pressure in first person with the existing 2D view. The assignment-folder copy includes LEVEL_DESIGN_GUIDE.md for continuing the greybox. This session feedback is separate from the still-missing class pitch feedback.

## First-person 3D comparison

A separate first-person 3D MVP is available at `/Users/redwang/Developer/FrontRooms3DMVP`; open its `Builds/Mac/FrontRooms3D.app` and press **Space**. It uses WASD + mouse, Shift, hold E, Esc, Tab and R. This is a comparison slice for first-person readability and pursuit pressure; its build and runtime evidence are documented in that project’s `README.md` and `VALIDATION.md`.

## Week 1 archive — Threshold / Running the Rooms

The original source-only Unity prototype remains intact in `Assets/Scenes/MVP_ThresholdRun.unity`. Its earlier question was: **when a room can be searched for useful fragments, what makes the player leave before they feel safe?** These are the old scene's mechanics and controls, not the current FrontRooms build.

### Original playable slice

The route is fixed so the test is repeatable:

1. **Level 0 / Threshold** — low-visibility blackout zone and a marker that shifts once after the player turns away.
2. **Level 4 / Abandoned Office** — Almond Water recovery zone and a black-window time/threat trap.
3. **Level ! / Run** — red chase corridor with no hiding; the third fragment is an optional bonus.
4. **Exit** — requires any two of the three fragments.

Controls: `WASD` or arrow keys to move, `Shift` to sprint, hold `E` to collect, `Esc` to pause, `Tab` to skip the tutorial, and `R` to retry after a result.

The scene is intentionally generated from placeholder geometry at runtime. The research sources, lore distinctions, and visual direction live in the [Figma Week 1 deck](https://www.figma.com/design/0tCbAiVUlrPId3RWd9LRif/Undergoing-Game-Projects?node-id=2099-76); the runtime keeps only short room rules so the player can learn them through action.

### Open the Week 1 scene locally

- Unity Editor: **6000.3.10f1**
- Open `Assets/Scenes/MVP_ThresholdRun.unity` and enter Play Mode.
- That was the Week 1 build scene. Current FrontRooms builds use `FP_FrontRooms.unity`; the legacy scene is preserved but disabled in the current build list.
- Keep the working clone outside iCloud Desktop. Use iCloud for exported screenshots, packaged builds, or notes only.

## Repository rules

The root `.gitignore` and `.gitattributes` cover Unity generated folders, Mac and Windows noise, iCloud placeholder/conflict files, and LF normalization. Do not commit `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, `Builds`, `WebGL`, `.app`, or crash output. Use a GitHub Release or an external handoff folder for builds.

## Scope boundary

This is an MVP prototype, not a finished replacement for Curtain. It is isolated in a new repository so the original Curtain project and its large build history remain intact.
