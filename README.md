# Threshold Rooms MVP

A source-only Unity 6 prototype for the Week 1 pitch **Threshold / Running the Rooms**.

The prototype tests one question: **when a room can be searched for useful fragments, what makes the player leave before they feel safe?**

## Playable slice

The route is fixed so the test is repeatable:

1. **Level 0 / Threshold** — low-visibility blackout zone and a marker that shifts once after the player turns away.
2. **Level 4 / Abandoned Office** — Almond Water recovery zone and a black-window time/threat trap.
3. **Level ! / Run** — red chase corridor with no hiding; the third fragment is an optional bonus.
4. **Exit** — requires any two of the three fragments.

Controls: \`WASD\` or arrow keys to move, \`Shift\` to sprint, hold \`E\` to collect, \`Esc\` to pause, \`Tab\` to skip the tutorial, and \`R\` to retry after a result.

The scene is intentionally generated from placeholder geometry at runtime. The research sources, lore distinctions, and visual direction live in the linked Figma Week 1 deck; the runtime keeps only short room rules so the player can learn them through action.

## Open locally

- Unity Editor: **6000.3.10f1**
- Open \`Assets/Scenes/MVP_ThresholdRun.unity\`.
- The active build scene is already set to that scene.
- Keep the working clone outside iCloud Desktop. Use iCloud for exported screenshots, packaged builds, or notes only.

## Repository rules

The root \`.gitignore\` and \`.gitattributes\` cover Unity generated folders, Mac and Windows noise, iCloud placeholder/conflict files, and LF normalization. Do not commit \`Library\`, \`Temp\`, \`Obj\`, \`Logs\`, \`UserSettings\`, \`Builds\`, \`WebGL\`, \`.app\`, or crash output. Use a GitHub Release or an external handoff folder for builds.

## Scope boundary

This is an MVP prototype, not a finished replacement for Curtain. It is isolated in a new repository so the original Curtain project and its large build history remain intact.

