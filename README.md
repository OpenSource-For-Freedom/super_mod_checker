# Supa Mod Checker (Formerly Moon Mod Checker)

![Superman Banner](superman.png)

A BepInEx plugin for Gorilla Tag that shows which players in your current room have mods installed, and lists the mod names.
- We play GTAG so we felt we should improve this tool to work, and discover mods in use during gameplay for our safety and integrity in modded servers. 

## Installation

1. Install **BepInEx 5.4.23.5** into your Gorilla Tag folder:
	- Steam: `C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\`
	- Oculus: `C:\Program Files\Oculus\Software\Software\another-axiom-gorilla-tag\`
	- Extract the BepInEx zip into that folder so you have a `BepInEx\` directory alongside `Gorilla Tag.exe`.
2. Copy `supamodcheck.dll` into `BepInEx\plugins\`.
3. Launch the game. The mod checker toggle appears in the top-left corner of the screen.

## How it works

The plugin is a standard BepInEx `BaseUnityPlugin` that hooks Unity's `OnGUI` loop. It renders a draggable IMGUI window with the following logic:

- A toggle button labeled **Moon Mod Checker** shows/hides the window.
- When the window is open and you are **not in a Photon room**, it displays `"Not connected to a room."`.
- When you **are in a room**, it iterates over `PhotonNetwork.PlayerList` and reads each player's `CustomProperties["mods"]` key.
- It aggregates a room-wide list of reported mods and displays only mod names with counts (no usernames).

Mods that self-report to Photon custom properties under the key `"mods"` will be visible here.

## Reverse engineering notes

The original `MoonModChecker.dll` (credited to Moon, redistributed by cgtsaturn) had **no source published**. We recovered it as follows:

1. **Decompiled** the managed .NET DLL using `ilspycmd 8.2.0` (`ilspycmd MoonModChecker.dll -p -o src/`).
2. **Identified obfuscation**: the decompiled output used Ether_Obfuscator — all identifiers were renamed to long garbage strings, and every string constant was replaced with an XXTEA-encrypted blob decoded at runtime.
3. **Decrypted strings**: wrote a standalone .NET 8 console program to run the same XXTEA decryption routine from the decompiled code. Recovered all four string constants:
	- `"+CqihIy6Debo8zPF9sJM8NztswI="` -> `"Moon Mod Checker"` (window title / toggle label)
	- `"Jy2RAlC3ioH7w2rPkDmuhgM2cyQfhG6BYKpeYw=="` -> `"Not connected to a room."`
	- `"6/zMN4IxWF0="` -> `"mods"` (Photon CustomProperties key)
	- `"XoRznsVjqgiK6J4powu7DsOZCwM="` -> `" IS USING MODS: "` (display separator)
4. **Rewrote** the plugin as clean, readable C# (`src/ModMenuPatch.cs`) with named constants replacing all encrypted strings and meaningful variable/method names replacing the obfuscated ones.
5. **Updated the build target** from BepInEx 5.4.21.0 to **BepInEx 5.4.23.5** (latest stable as of 2026), referencing the BepInEx core DLL and the game's Photon/UnityEngine DLLs directly via HintPaths.
6. **Built** with `dotnet build -c Release` — 0 warnings, 0 errors.

This rebuild was done to address older BepInEx compatibility problems and to remove risky/obfuscated implementation details that made security review difficult.

## Security scan summary

Latest rebuilt binary reviewed: `supamodcheck.dll`

- SHA256: `73824A6C595C772A9AADC3CB0436267F07B5FB72AB9D66D01428A3DBC5E5DB0E`
- Authenticode signature: **Not signed**
- Referenced assemblies: expected Unity/Photon/BepInEx dependencies
- Static source scan: no `Process.Start`, no shell execution, no web download/exfiltration APIs, no P/Invoke, no registry writes
- Strings scan: no hardcoded URLs, IPs, webhooks, tokens, or passwords related to credential leakage

Overall risk assessment of this rebuilt DLL: **Low** (GUI utility behavior only; no malicious patterns detected in static review).

## Credits

Original mod by **Moon**. Reverse engineered and rebuilt by `opensource-for-freedom`.

## Disclaimer

This product is not affiliated with Gorilla Tag or Another Axiom LLC and is not endorsed or otherwise sponsored by Another Axiom LLC. Portions of the materials contained herein are property of Another Axiom LLC. © 2021 Another Axiom LLC.
