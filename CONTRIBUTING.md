# Contributing to Super Mod Checker

## Overview

Super Mod Checker is a BepInEx plugin for Gorilla Tag that detects and surfaces modded players through Photon custom properties. Contributions should stay focused on that purpose.

## Before You Start

- Check existing issues to avoid duplicating work.
- For significant changes, open an issue first to discuss the approach.
- This project targets BepInEx 5.4.23.5 and .NET Framework 4.7.2.

## Development Setup

1. Install BepInEx 5.4.23.5 into your Gorilla Tag folder.
2. Clone this repo.
3. Add the required game assembly references to the project (not included in source):
   - `Assembly-CSharp.dll` from your Gorilla Tag install
   - `Photon.Realtime.dll`, `Photon.Unity3D.dll`
   - `UnityEngine.dll` and related Unity assemblies
4. Build with `dotnet build src/MoonModChecker.csproj -c Release`.
5. Copy `supamodcheck.dll` from `src/bin/Release/net472/` to your `BepInEx/plugins/` folder to test.

## Contribution Guidelines

- No emojis in code, comments, or UI strings.
- Keep changes scoped. Do not refactor unrelated code in the same PR.
- Do not add telemetry, analytics, or network calls beyond the existing local JSONL log.
- Mod detection logic lives in `ModMenuPatch.cs`. Keep keyword lists maintainable and documented with a comment explaining why each entry exists if it is not obvious.
- Do not commit compiled DLL files unless specifically requested. The `supamodcheck.dll` in the repo root is the distribution artifact only.

## Submitting Changes

1. Fork the repository.
2. Create a branch off `main` with a short descriptive name.
3. Make your changes and verify a clean build with no new errors.
4. Open a pull request with a clear description of what changed and why.

## Reporting Issues

Use the bug report issue template. Include your BepInEx `LogOutput.log` and `supamodcheck-log.jsonl` where relevant.
