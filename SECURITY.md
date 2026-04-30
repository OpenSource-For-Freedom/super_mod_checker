# Security Policy

## Supported Versions

Only the latest release of Super Mod Checker is maintained.

| Version | Supported |
|---------|-----------|
| Latest  | Yes       |
| Older   | No        |

## Scope

Super Mod Checker is a read-only BepInEx plugin. It reads Photon custom properties and writes to a local log file. It does not make network requests, does not modify game state, and does not interact with other players beyond reading public room data.

Security reports relevant to this project include:

- Local file path traversal in log file writing.
- Any unintended code execution triggered by crafted Photon property values.

Out of scope:

- Gorilla Tag game vulnerabilities.
- BepInEx framework vulnerabilities.
- Requests to help bypass mod detection or exploit other players.

## Reporting a Vulnerability

Open a private security advisory on GitHub (Security tab > Report a vulnerability). Do not open a public issue for security reports.

Include:

- A description of the issue.
- Steps to reproduce.
- The version of Super Mod Checker and BepInEx you are using.

You will receive a response within 7 days.
