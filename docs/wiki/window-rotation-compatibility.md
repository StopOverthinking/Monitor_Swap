# Window Rotation Compatibility

`WindowRotationService` has a few compatibility paths for apps that react poorly to ordinary resize and move messages.

## Window exclusion rules

- User-configurable `WindowExclusionRule` entries are stored in settings and are applied immediately after a window snapshot is captured.
- Excluded windows are removed from the rotation set before moving, order restoration, and surface refresh. They are logged in diagnostics as `excluded-by-rule` but are not counted as warning skips.
- Process name and class name conditions match exactly, ignoring case. Window title conditions match as a case-insensitive substring. Blank text and zero size limits mean that condition is unused.
- The default `Codex pet` rule matches `ProcessName=Codex`, `ClassName=Chrome_WidgetWin_1`, `WindowTitle=Codex`, `RequireTopMost=true`, and `RequireNoActivate=true`.
- The default Codex pet rule intentionally does not use a size limit, so resizing the pet should not make it rejoin monitor rotation. Size limits remain available for user-created rules or manual settings edits when a compact utility window needs extra narrowing.

## NovelAI

- Maximized NovelAI browser windows use a safe restore rectangle before being maximized again on the target monitor.
- This behavior is keyed from the window title containing `NovelAI`, not from the browser compatibility setting, because disabling Chromium compatibility should not reintroduce the NovelAI history/sidebar collapse.
- The safe restore rectangle is the target monitor working area. This avoids a transient narrow restored size while the web app recalculates its responsive layout.

## Fast mode

- Fast mode may skip expensive order restoration only for simple windows.
- If any moved window is Chromium-based, NovelAI-titled, or otherwise requires a surface reset, order restoration still runs to preserve the browser compatibility behavior.

## Godot and Surface-Backed Windows

- The Godot-related monitor swap behavior is documented in code as the broader `surface-backed window` path.
- Surface-backed windows may leave stale rendered content or resize incorrectly if moved with only a plain `SetWindowPos` call.
- `RequiresSurfaceReset` marks affected window classes and routes them through `MoveSurfaceBackedWindow`.
- That path updates `WINDOWPLACEMENT`, moves the window, invalidates the full window and child surfaces with `RedrawWindow`, and flushes DWM.
- Current detection is class-name based. If a Godot runtime exposes a stable class name or process name in future testing, add it to `RequiresSurfaceReset` and record the exact identifier here.
