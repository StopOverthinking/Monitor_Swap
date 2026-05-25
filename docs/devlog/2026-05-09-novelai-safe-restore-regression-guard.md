# 2026-05-09 - NovelAI safe restore regression guard

## Summary

- Made the NovelAI maximized-window safe restore path independent from the Chromium browser compatibility setting.
- Kept window order restoration enabled in fast mode whenever moved windows include Chromium, NovelAI-titled, or surface-reset-sensitive windows.
- Added wiki notes for NovelAI and fast mode compatibility behavior.

## Context

The latest behavior change added fast mode and changed some order-restoration behavior. A previous NovelAI issue, where the History/sidebar area could collapse during monitor swaps, was reported as repeating.

## Changes

- `IsNovelAiWindow` now detects by title without requiring `IsChromiumWindow`.
- Maximized NovelAI windows always use the safe target-working-area restore rectangle.
- Fast mode only skips order restoration when the moved window set does not include compatibility-sensitive windows.

## Verification

- Pending: run a Release build after the code change.
- Runtime NovelAI verification still needs a live NovelAI browser window on a multi-monitor setup.

## Follow-ups

- If NovelAI changes its window title, consider adding a more explicit user-configurable compatibility rule.
