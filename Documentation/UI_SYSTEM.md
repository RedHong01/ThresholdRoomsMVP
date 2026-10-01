# FrontRooms UI system

This prototype uses a world-first horror HUD. The room and the player's read of it stay visually dominant; the HUD gives only the next useful signal.

## Type use

- HUD meta: IBM Plex Mono, 13 px, 16 px leading. Use for room metadata, distance, controls, and event-like labels.
- UI label: Bayon, 20 px, 20 px leading. Use for state, prompt, and section labels.
- Screen title: Bayon, 88 px, 80 px leading. Use for title, start, pause, and result moments.
- Room name: Source Serif 4, 50 px, 42 px leading. Use for the current location.
- Context: Source Serif 4, 24 px, 26 px leading. Use for a clue, note, or consequence.

The runtime loads the matching files from `Assets/Resources/Fonts` (`IBMPlexMono-Regular.ttf`, `Bayon-Regular.ttf`, and `SourceSerif4-Variable.ttf`). If those assets are unavailable in a fresh clone, it falls back to Unity's built-in face so the prototype still boots.

## Grid use

- Reference viewport: 1920 x 1080.
- Outer margin: 72 px.
- Columns: 12 columns, 126 px each, 24 px gutters.
- Rows: 6 rows, 136 px each, 24 px gutters.
- Gameplay comparison: two 6-column panels with a 24 px gap.
- Result states: three 4-column cards sharing the same baseline.
- Internal rhythm: 24 px. Keep panels and prompts on that rhythm.

## Information layers

- Persistent: current room, current state, and one threat signal (hunter distance / chase state).
- Contextual: one prompt, one clue, or the note/map view when the player is close enough or asks for it.
- Secondary: notes/map and pause controls are opened with `Tab` or `Esc`; they are not permanently stacked onto the play view.

## Runtime mapping

- 2D: WASD or arrows move, `Shift` runs, hold `Q` to read, hold `E` to break glass, `Tab` opens the map, `Esc` pauses, `R` retries.
- 3D: WASD moves, mouse looks, `Shift` runs, hold `E` reads/breaks, `E` opens a keyed door, `Tab` opens notes, `Esc` pauses, `R` retries.
- `Space` or `Return` starts from the title card.

## Runtime HUD mapping

- Persistent play layer: room name at 50 px, `HUNTER / ## M` at 20 px, and one 4 px threat rule.
- Context layer: one 24 px prompt at the bottom; it changes only for a nearby action or a short event message.
- Secondary layer: `Tab` changes the room title to `MAP / NOTES` and exposes only room names plus `YOU ARE HERE`, `EXIT`, or `NOTE READ` markers.
- Title, pause, and result overlays use an 88 px Bayon heading with one short 24 px explanation and a compact 20 px control line.
