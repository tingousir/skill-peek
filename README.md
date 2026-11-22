# Skill Peek

**English** · [中文](README.zh-CN.md)

[![build](https://github.com/tingousir/skill-peek/actions/workflows/build.yml/badge.svg)](https://github.com/tingousir/skill-peek/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/tingousir/skill-peek?color=2f7d5b)](https://github.com/tingousir/skill-peek/releases/latest)
[![license](https://img.shields.io/github/license/tingousir/skill-peek?color=2f7d5b)](LICENSE)
![platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-2f7d5b)

One hotkey brings up a skill panel over whatever you are doing: **72 skills across three pages, the ones you use most sit top-left, anything not installed is greyed out**. Arrow keys to move, `Enter` pastes `@skill` back into the box you came from.

![Skill Peek panel](docs/screenshot.png)

## Download

**[⬇ Get the latest SkillPeek.exe](https://github.com/tingousir/skill-peek/releases/latest/download/SkillPeek.exe)** · 33 KB · Windows 10 / 11

1. Follow the link and save `SkillPeek.exe`
2. Double-click it. No installer, no dependencies, no network access
3. A tray icon appears; press `Ctrl` + `Shift` + `Space` to open the panel

A single file. No DLLs, no installer, no bundled browser. The only runtime dependency is the .NET Framework 4.x that ships with Windows.

### The first run hits SmartScreen

The exe is not code-signed, so Windows shows "Windows protected your PC". Click **More info → Run anyway**.

To check the file was not tampered with:

```powershell
Get-FileHash .\SkillPeek.exe -Algorithm SHA256
```

The result must match the digest in `SkillPeek.exe.sha256` from the release.

## Usage

| Action | Result |
|---|---|
| Global hotkey | Open / close the panel |
| Wheel, `Tab`, `Shift+Tab` | Switch between the three pages, or click a tab |
| Arrow keys, `Home`, `End` | Move the selection |
| `Enter` or click | Copy `@skill`, close, paste into the previous window |
| `Esc` | Close |
| **↻** top-right | Rescan locally installed skills |

The top-right also switches between **hold** and **once**: in hold mode the panel closes as soon as you release the hotkey. The tray menu can enable launch-at-login, or switch `Enter` to copy-only for apps where `Ctrl+V` is not paste.

## Pages and order

| Page | Contents | Skills |
|---|---|---|
| Work | Engineering + design: debugging, code review, architecture, UI, images | 28 |
| Plan | Process + research + growth: how to start, how to find out, how to ship | 28 |
| Write | Docs, copy, PRs, Office, PDF | 16 |

The order is not arbitrary. **The row order in `ui/skills.tsv` is the display order** — earlier rows are the more common skills, so they land closer to the top-left. It is based on how popular the source repositories are (`anthropics/skills`, `obra/superpowers` and similar) plus everyday use, not on telemetry.

## Your own skill list

Every time the panel opens it scans the first-level subdirectories of the configured roots. Skills that exist there are shown as available; the rest are greyed out. Roots live in the config:

```ini
; %APPDATA%\SkillPeek\settings.ini
mode=once
hotkey=Ctrl+Shift+Space
paste=1
skills=%USERPROFILE%\.dsh\skills;%USERPROFILE%\.claude\skills
```

To change the list itself — add skills, reorder, change a category colour — edit `ui/skills.tsv`. Five tab-separated columns per row:

```
工作	tdd	工程	先写会失败的测试	#3E7A96
```

(Page and category names are Chinese in the shipped catalog — the row above is a real line from it.)

Rebuild afterwards.

## Build from source

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Version 0.1.1
```

No .NET SDK needed: it compiles with the `csc.exe` that ships with Windows, and embeds the skill catalog into the exe as a resource. Output is `dist/SkillPeek.exe` (single file) plus `dist/SkillPeek.exe.sha256`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

[MIT](LICENSE)
