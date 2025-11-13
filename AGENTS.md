# Repository Guidelines

## Project Structure & Module Organization
- `Assets/` houses all gameplay code (`Scripts/Classes`, `MonoBehavioursUsed`, `Interfaces`), scenes, prefabs, and art/audio assets. Third-party drop-ins live under `CC5 - AutoSetup/`.
- `Packages/manifest.json` tracks Unity package dependencies (HDRP, Timeline, Cinemachine, Input System).
- `ProjectSettings/` and `UserSettings/` contain Unity configuration; never edit manually unless you know the downstream impact.
- Documentation is collected in `Docs/` (entry point: `Docs/IndexDocumentation.md`).

## Build, Test, and Development Commands
- `unity -projectPath . -executeMethod UnityEditor.BuildPlayerWindow.DefaultBuildMethods.BuildPlayer` — produces a build using the active build target.
- `unity -projectPath . -runTests -testPlatform playmode` — executes play mode tests; replace `playmode` with `editmode` as needed.
- `rg --files -g '*.cs'` and `rg TODO` are preferred for source scans over `grep` (already configured in this repo’s workflows).

## Coding Style & Naming Conventions
- C# scripts use the default Unity style: four-space indentation, PascalCase for types/methods, camelCase for fields, and `kConstantCase` for consts.
- ScriptableObject assets are suffixed with `SO` or a descriptive type (`MusicalMoveSO`, `TimelineBattleConfigSO`).
- Keep MonoBehaviour filenames identical to class names and store them inside the closest relevant feature folder (e.g., combat logic under `MonoBehavioursUsed/`).

## Testing Guidelines
- PlayMode/EditMode tests follow Unity Test Framework conventions (`Tests/PlayMode`, `Tests/EditMode`). Mirror production namespaces and append `Tests` to the assembly definition.
- Name tests with the pattern `Method_WhenCondition_ShouldResult`.
- Run targeted suites via the Unity Test Runner UI or the CLI commands above. Maintain coverage for combat state machines, timeline management, and save/load flows.

## Commit & Pull Request Guidelines
- Commits typically follow the imperative mood (`Fix timeline trigger double-play`, `Add input-safe time manager`). Group related asset meta changes with their source files.
- Pull requests should describe gameplay impact, list verification steps (editor playthrough, automated tests), and attach screenshots/GIFs for visual/UI changes.
- Reference Jira/Trello tickets or GitHub issues in the PR body using `Closes #123` when applicable, and ensure conflicts are resolved before requesting review.

## Instructions spécifiques pour Codex
- Par défaut, Codex doit communiquer en français (messages de commit, résumés, commentaires de revue).
- Basculer en anglais uniquement si la tâche ou le ticket le demande explicitement.
