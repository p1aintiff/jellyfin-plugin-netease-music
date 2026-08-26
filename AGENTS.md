# Repository Guidelines

## Project Structure & Module Organization

The solution is defined in `JellyfinMusic.slnx`; the plugin project lives in `Jellyfin.Plugin.NetEaseMusic/`. Keep HTTP endpoints in `Controllers/`, NetEase and Jellyfin integration logic in `Services/`, request and response DTOs in `Models/`, and persisted settings in `Configuration/`. `Web/configPage.html` is embedded as the Jellyfin dashboard page. Packaging metadata is split between `build.yaml` and `manifest-info.json`; release automation is in `.github/workflows/release.yml`, and local packaging is handled by `scripts/package-plugin.ps1`. Generated packages belong in `dist/` and should not be treated as source.

## Build, Test, and Development Commands

- `dotnet restore .\JellyfinMusic.slnx` restores Jellyfin and .NET dependencies.
- `dotnet build .\JellyfinMusic.slnx` compiles the plugin with nullable analysis enabled.
- `dotnet build .\JellyfinMusic.slnx -c Release` reproduces the CI build configuration.
- `powershell -ExecutionPolicy Bypass -File .\scripts\package-plugin.ps1` publishes the project and creates `dist\NetEaseMusicImporter-<version>.zip`.

Run commands from the repository root. To exercise the plugin end to end, install the packaged files in a Jellyfin 10.10.7-compatible server and use the dashboard page or the API examples in `README.md`.

## Coding Style & Naming Conventions

Use standard C# conventions: four-space indentation, file-scoped namespaces, `PascalCase` for types and public members, `camelCase` for parameters and locals, and `_camelCase` for private fields. Keep nullable annotations accurate; do not suppress warnings without a documented reason. Name asynchronous methods with an `Async` suffix and pass `CancellationToken` through network or library operations. Keep API models simple and initialize collection properties.

## Testing Guidelines

There is currently no automated test project or coverage gate. Every change must at least pass a clean `dotnet build -c Release`. For behavior changes, manually verify playlist import, matching, history refresh, and error handling in Jellyfin as applicable. New tests should use a sibling project named `Jellyfin.Plugin.NetEaseMusic.Tests`, with files named `<TypeName>Tests.cs` and test names that describe the expected behavior.

## Commit & Pull Request Guidelines

Recent commits use short, imperative subjects such as `Fix missing tag handling` and `Improve plugin manifest metadata`. Follow that pattern, keep each commit focused, and explain non-obvious compatibility decisions in the body. Pull requests should summarize the user-visible effect, list validation performed, link relevant issues, and include dashboard screenshots for changes to `Web/configPage.html`. When releasing, keep the project version, `build.yaml`, and `manifest-info.json` consistent.
