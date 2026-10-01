# WebGL Build Notes

FrontRooms 2D keeps the WebGL target source-first and deterministic. The checked-in project settings are tuned for a static host such as GitHub Pages:

- **Brotli artifacts** reduce transfer size for the wasm/data files.
- **Decompression fallback** is enabled because static hosts do not always send the `Content-Encoding: br` header required for pre-compressed files. This keeps the build playable when the server serves the files as ordinary static assets.
- **Hashed filenames and data caching** let browsers retain unchanged wasm/data files between runs while avoiding stale content after a new build.
- **128 MB initial heap / 1 GB maximum heap** keeps startup memory lower while leaving room for the procedural room graph and generated audio. The heap grows geometrically when needed.
- **Default WebGL template** is used. The previous `APPLICATION:DuoCurtain` value referred to a template that is not part of this repository and could produce a missing-template build or a blank loader.
- **Exception support and debug symbols stay off** for release builds. The code keeps its own lightweight validation path through `FrontRoomsVerification`.

Build from Unity 6000.3.10f1 with:

```text
Unity -batchmode -quit -projectPath <project> -executeMethod FrontRoomsBuild.BuildWebGL -logFile <log>
```

The output is `Builds/WebGL/` and is ignored by Git. Serve that folder from a web server; opening `index.html` directly from `file://` is not supported by browsers because wasm/data requests are blocked by CORS/security rules. For GitHub Pages, publish the contents of `Builds/WebGL/` as the site root and keep the generated `.unityweb` files unchanged.

The browser control path continues to use the legacy `Input` API (WASD/arrows, mouse cursor mode, Space/Return, Escape, R, Tab, F1, F), and the project remains set to **Both** input backends so the existing editor and standalone controls are unchanged.
