# 2026-05-09 - Documentation workspace setup

## Summary

- Added a lightweight documentation workspace under `docs/`.
- Added a development log index and starter template.
- Added a project wiki index with initial project and build notes.
- Added local agent guidance in `AGENTS.md` and ignored it through `.gitignore`.

## Context

The repository did not have a dedicated place for ongoing implementation notes or durable project knowledge. The new structure separates chronological work history from stable reference material.

## Changes

- `docs/devlog/` now contains an index, a reusable entry template, and this initial entry.
- `docs/wiki/` now contains the wiki index, project overview, and build/package notes.
- `AGENTS.md` tells future agents to read the wiki and development log before starting work.
- `.gitignore` ignores `AGENTS.md`, keeping local agent instructions out of tracked changes.

## Verification

- Checked existing README files and build scripts before writing the initial wiki notes.
- No application source code or runtime behavior was changed.
- Build was not run because this was documentation-only setup.

## Follow-ups

- Add feature-specific wiki pages when a code change touches window rotation, monitor selection, localization, installer behavior, or settings persistence.

