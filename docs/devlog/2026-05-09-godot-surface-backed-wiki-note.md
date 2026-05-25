# 2026-05-09 - Godot surface-backed wiki note

## Summary

- Checked the wiki and development log for Godot-specific notes.
- Confirmed there was no explicit `Godot` or `고도` entry.
- Added the Godot/surface-backed window relationship to the compatibility wiki.

## Context

Earlier rendering-engine-related changes are represented in code by the `surface-backed window` compatibility path, especially `RequiresSurfaceReset`, `MoveSurfaceBackedWindow`, and `RefreshSurfaceBackedWindows`. That made the Godot context hard to find from the wiki.

## Changes

- Added a `Godot and Surface-Backed Windows` section to `docs/wiki/window-rotation-compatibility.md`.
- Documented the redraw/DWM flush strategy and the need to record a stable Godot class or process identifier if one is confirmed later.

## Verification

- Searched docs and code for `Godot`, `godot`, `고도`, `surface`, and related rendering terms.
- No build was run because this was a documentation-only update.

## Follow-ups

- Capture the exact Godot window class/process name from a live Godot runtime if the issue recurs or needs a narrower compatibility rule.
