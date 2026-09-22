# MVP Acceptance Log

Use this checklist when running the prototype on each operating system. Record the Unity version and any Console errors beside each run.

## Recorded smoke test

- **2026-09-22 · macOS · Unity 6000.3.10f1:** clean project import, scene load, Play Mode render, T0 tutorial display, and first-movement advance to T1 passed. No new Console errors after the font and scene import fixes. Windows fresh-clone validation remains to be run on the Windows machine.

## Tutorial

- [ ] T0 shows controls on a fresh run.
- [ ] First movement advances the tutorial.
- [ ] Level 0 blackout rule appears and the marker shifts once after leaving the blackout.
- [ ] Holding E for one second collects a fragment and starts the threat.
- [ ] Sprint guidance appears and stamina drains.
- [ ] Level 4 explains recovery and the black-window trap.
- [ ] Level ! explains the forced chase and optional bonus.
- [ ] Tab skips the tutorial without removing the HUD goal.

## Happy path

- [ ] Collect the Level 0 fragment.
- [ ] Use the Level 4 recovery area.
- [ ] Collect the Level 4 fragment.
- [ ] Reach Level ! and exit with 2/3 fragments.
- [ ] Result screen reports time, fragments, and bonus state.

## Trade-off path

- [ ] Skip the third fragment and leave quickly.
- [ ] Stop in the Level ! alcove for the optional bonus.
- [ ] Confirm that collection creates a visible threat cue.

## Failure and recovery

- [ ] Timer timeout shows a reason and allows R retry.
- [ ] Hound contact shows a reason and allows R retry.
- [ ] Exit with fewer than 2 fragments stays locked and explains the missing count.
- [ ] Esc pauses/resumes without advancing the timer.
- [ ] Three consecutive fresh runs do not duplicate timer, pursuer, or HUD objects.

## Cross-platform / Git

- [ ] Mac fresh clone opens with Unity 6000.3.10f1.
- [ ] Windows fresh clone opens with Unity 6000.3.10f1.
- [ ] \`git status\` stays clean after import and play except expected user-authored changes.
- [ ] No \`Library\`, \`Temp\`, \`Obj\`, \`Logs\`, \`UserSettings\`, \`Builds\`, \`WebGL\`, \`.app\`, \`.DS_Store\`, \`.icloud\`, \`._*\`, \`Thumbs.db\`, or \`Desktop.ini\` appears in Git status.
- [ ] The working clone is outside iCloud Desktop on both systems.
