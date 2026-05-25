# 2026-05-25 - Window exclusion rules

## Summary

- Added settings-backed window exclusion rules for monitor rotation.
- Added a default `Codex pet` exclusion rule that distinguishes the pet from the main Codex window with `TopMost` and `NoActivate` style requirements.
- Added settings UI to enable, disable, add, and remove exclusion rules.

## Context

- The Codex pet should stay on its monitor when Monitor Swap rotates ordinary windows.
- Current local inspection showed both the main Codex window and the pet use `ProcessName=Codex`, `ClassName=Chrome_WidgetWin_1`, and `WindowTitle=Codex`.
- The pet candidate also exposes topmost and no-activate extended window styles, so the default rule uses those styles instead of an exact size match.

## Changes

- `AppSettings` now persists `WindowExclusionRules` and one-time `ExclusionDefaultsInitialized`.
- `WindowRotationService` filters matching windows after snapshot capture and before move/order/refresh work.
- `SettingsForm` now shows an `Excluded Windows` list, checked active rules, current-window add, and remove controls.
- `AppLocalization` includes English and Korean strings for the new settings UI.
- `docs/wiki/window-rotation-compatibility.md` documents rule matching and the Codex pet default.

## Verification

- Ran `.\build.ps1 -Configuration Debug` successfully.
- Loaded settings from the Debug assembly with the existing local settings file and confirmed the default `Codex pet` rule is initialized with no size limit.
- Enumerated current Codex windows and confirmed the small topmost/no-activate window matches the default rule while the non-topmost/non-no-activate Codex window does not.
- After a runtime report that the pet still moved, confirmed the running tray app was `bin\Release\MonitorSwap.exe` and that the old Release binary did not yet contain `WindowExclusionRules`.
- Stopped the running Release tray process, rebuilt with `.\build.ps1 -Configuration Release`, restarted `bin\Release\MonitorSwap.exe`, and confirmed the Release assembly now initializes the default `Codex pet` rule.

## Follow-ups

- If future Codex pet builds expose a more explicit window title, class, or process marker, prefer that marker over style-based detection.
