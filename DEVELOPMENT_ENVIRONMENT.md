# Cross-platform development notes

This project is edited on macOS and Windows. Git is the source of truth; iCloud is not a Git transport.

## Keep the working tree out of iCloud

Keep the active clone in a local developer folder such as `~/Developer/ThresholdRoomsMVP` on macOS or `C:\Dev\ThresholdRoomsMVP` on Windows. Do not put the active clone, its `.git` directory, `Library/`, or a build output inside an iCloud Drive location (this includes Desktop/Documents when iCloud Desktop & Documents is enabled). iCloud can create placeholder names ending in `.icloud`, AppleDouble files beginning with `._`, and conflict copies while Unity is rewriting files. The repository ignores those names, but the safer fix is to let GitHub synchronize source and keep iCloud out of the working tree.

If the project currently lives in an iCloud-synced folder, close Unity and GitHub Desktop, wait for pending transfers to finish, move or clone the repository into a local folder, then reopen it from that local path. Do not move the `.git` directory while Unity or Git is running.

## macOS and Windows checkout rules

- Use the Unity editor version recorded in `ProjectSettings/ProjectVersion.txt` on both machines.
- Keep every Unity asset next to its `.meta` file. Never regenerate or delete `.meta` files to resolve a merge; that changes GUID references.
- Keep paths and asset names case-consistent.
- `.gitattributes` makes C#, shaders, Unity YAML, JSON, Markdown, and Git metadata use LF in Git. Do not run a whole-project line-ending conversion from an editor. After a fresh clone, `git add --renormalize .` is safe if Git reports line-ending changes.
- Keep `core.autocrlf` disabled for this repository so the explicit `.gitattributes` rules remain authoritative:

  ```text
  git config core.autocrlf false
  git config core.safecrlf true
  ```

## What belongs in Git

Commit `Assets/`, `Packages/`, `ProjectSettings/`, source documentation, and their `.meta` files. Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, IDE caches, crash dumps, or platform builds. The local build folders remain available for smoke tests but are intentionally ignored; publish a verified build through GitHub Releases or another artifact store instead of adding it to normal source history.
