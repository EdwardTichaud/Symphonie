# Repository Guidelines

## Project Structure & Module Organization
Unity assets and gameplay logic live inside `Assets/`. Scripts are grouped by role (`Scripts/MonoBehavioursUsed` for runtime behaviours, `Scripts/Classes` for data helpers, ScriptableObjects throughout `Assets/<Feature>`). Prefabs, animations, and VFX referenced by UI (e.g., `LevelDisplayCanvas.prefab`, `MusicInfoBoxCanvas.prefab`) stay near their driving scripts. `CC5 - AutoSetup/` stores imported tooling, `Docs/` hosts reference material, and package/engine settings are confined to `Packages/manifest.json`, `ProjectSettings/`, and `UserSettings/`. Only edit settings when you understand the downstream impact, and keep `.meta` files paired with their assets.

## Build, Test, and Development Commands
- `unity -projectPath . -runTests -testPlatform editmode` — executes Edit Mode suites headlessly; add `-testResults results.xml` for CI output.
- `unity -projectPath . -runTests -testPlatform playmode` — runs Play Mode coverage in scenes such as Ruins; useful before merging gameplay/UI work.
- `unity -projectPath . -quit -batchmode -executeMethod UnityEditor.BuildPlayerWindow.DefaultBuildMethods.BuildPlayer` — triggers the configured build target; ensure addressables are built beforehand if a scene depends on them.
- `rg <pattern> Assets/Scripts` — preferred fast search; use it instead of `grep` when triaging code paths or TODOs.

## Coding Style & Naming Conventions
Follow Unity C# defaults: four-space indentation, PascalCase for types/methods, camelCase for fields, and `k_` prefixes for serialized constants. File names must match MonoBehaviour class names. Keep feature assets together (e.g., `MusicInfoBoxUI.cs` beside `MusicInfoBoxCanvas.prefab`) and suffix ScriptableObjects with `SO` (e.g., `ZoneSO`). Particle systems, animations, and materials should use descriptive suffixes like `_Particles`, `_anim`, `_mat` to simplify searches.

## Testing Guidelines
This repo uses the Unity Test Framework. Place Edit Mode suites under `Assets/Tests/EditMode`, Play Mode suites under `Assets/Tests/PlayMode`, mirroring production namespaces and suffixed with `Tests`. Name tests `Method_WhenCondition_ShouldResult`. Before opening a PR, run both CLI commands above and perform a short in-editor validation of any scene touched (e.g., confirm LevelDisplay/MusicInfo fades). Keep regression tests around battle logic, timelines, and audio playback.

## Commit & Pull Request Guidelines
Commits should be small, scoped, and written in the imperative mood (`Implement LevelDisplay fade`, `Fix MusicInfo particles`). Always commit asset and `.meta` files together. Pull requests must summarize the change, list verification steps (tests run + manual playthrough), attach screenshots or clips for visual updates, and reference tracking issues using `Closes #123`. Resolve merge conflicts locally and ensure CI (including Unity tests) is green before requesting review.
