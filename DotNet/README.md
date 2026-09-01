# DotNet/ — headless build of the musical domain

This folder is **invisible to Unity** (Unity only compiles `Assets/` and `Packages/`).
It exists so `Jazztures.Core` can be built and unit-tested on the desktop CLR without a
Unity install or a headset — the requirement in `CLAUDE.md` §2.1, and the M0 "done when".

See `Docs/DECISIONS.md` ADR-0002 for the rationale.

## Layout

| Project | What it is |
|---|---|
| `Jazztures.Core/Jazztures.Core.csproj` | SDK-style `netstandard2.1` build that compiles **the same `.cs` files** as `Assets/Jazztures/Core/Jazztures.Core.asmdef` (linked glob). |
| `Jazztures.Core.Tests/Jazztures.Core.Tests.csproj` | `net8.0` NUnit runner that compiles **the same test `.cs` files** as `Assets/Tests/EditMode/`. |
| `Jazztures.sln` | Both projects, for IDE use. |

## Prerequisite

The **.NET SDK 8.0+** must be installed (`dotnet --list-sdks` must list one). As of
2026-09-01 this machine has the runtime and VS 2026 MSBuild but no SDK. Install it once:

```
winget install Microsoft.DotNet.SDK.8
```

## Run the tests

```
dotnet test DotNet/Jazztures.sln
```

That is the command CI runs and the command to run locally before every commit that
touches `Assets/Jazztures/Core/`.

## Rules this setup enforces

- **`Jazztures.Core` must not reference `UnityEngine`.** If it does, this build breaks —
  which is the point.
- **EditMode tests that cover Core must not reference `UnityEngine` either.** They are
  compiled here too. Unity-dependent tests belong in `Assets/Tests/PlayMode/` or a
  separate Unity-only EditMode assembly.
- Keep `TargetFramework` / `LangVersion` / `Nullable` in the two `.csproj` files aligned
  with what Unity 6000.5 supports (C# 9, nullable on).
