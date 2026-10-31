# Contributing to Pénombre

Thanks for your interest! Pénombre is a small project: one source file, no dependency. Keeping it that way is part of the goal.

## Contents

- [Report a bug or suggest an idea](#report-a-bug-or-suggest-an-idea)
- [Build](#build)
- [Self-test](#self-test)
- [Code guidelines](#code-guidelines)
- [Ground rules for game-related changes](#ground-rules-for-game-related-changes)
- [Add a language](#add-a-language)
- [Pull requests](#pull-requests)
- [Release (maintainer)](#release-maintainer)

## Report a bug or suggest an idea

Open an [issue](https://github.com/Sokenzane/Penombre/issues/new/choose). For a bug, paste the output of **Copy diagnostics** (tray menu) and, if relevant, the end of the log (**Open the log**). Both contain your window titles, so your character names: replace them with `XXX` if you prefer.

## Build

Double-click `build.bat` (or run `build.bat ci` to skip the final pause). It compiles `src/Penombre.cs` and `src/Interface.cs` into `dist/Penombre.exe` (64-bit) with the C# compiler that ships with .NET Framework 4, so there is nothing to install.

`--lang=en|fr` forces the language, `--data=<folder>` keeps the settings and the log in another folder.

## Self-test

With Dofus 3 open (one character logged in, windowed or fullscreen windowed), double-click `tools\selftest.bat`. It stops Pénombre, builds `dist\Penombre-test.exe` (the app plus [`src/SelfTest.cs`](src/SelfTest.cs)), runs it on a separate settings folder (`dist\selftest-data`, your settings are not touched), then rebuilds and restarts `dist\Penombre.exe`.

The test drives the real app over the game and measures the darkening on screen: renderers, fade, mood transitions, per-account settings, bright zones, vignette, grain, automatic mode, sun times, shortcuts registration, export / import, portable mode, start with Windows (restored as it was), settings window, performance. Bring Dofus to the front when asked, then follow the `MANUAL` lines of `dist\selftest-data\selftest-progress.txt` (shortcuts, peek, a window over Dofus, only-in-foreground).

Results: `dist\selftest-data\selftest-report.txt` (with screen captures `snap-*.png` and `settings-tab*.png`). Every line must be `PASS`.

## Code guidelines

- **C# 5 only.** The Windows built-in compiler (`csc.exe` v4) stops at C# 5. Do not use string interpolation (`$"…"`), `=>` members, `?.`, `nameof`, `out var` or auto-property initializers. `build.bat` will tell you if something slipped through.
- **No dependency.** The app is in `src/Penombre.cs`, its hand-drawn controls and colors in `src/Interface.cs`. Use WinForms, GDI+ and Win32 (P/Invoke) only. Embedded resources (icons, Nunito font) are listed in `build.bat`.
- **Icons.** Change `tools/make_icon.py` and run it (Pillow and Playwright): it rewrites `assets/penombre.ico`, `assets/logo.png` and `assets/tray-icons.png`, without metadata.
- **Physical pixels.** The process is per-monitor DPI aware: every coordinate is in physical pixels.
- **No hard-coded UI text**: every string lives in `T` (English and French).
- **Settings** go through the `Settings` class (`%APPDATA%\Penombre\settings.ini`); keep it a small key=value file.
- **Errors** go to the log (`Log.Error`), never to a message box in a loop.
- Code and comments in English, 4-space indentation, CRLF line endings (handled by `.editorconfig` and `.gitattributes`).

## Ground rules for game-related changes

Pénombre must stay a pure overlay. Pull requests that do any of the following will not be accepted:

- inject code into, or read or write the memory of, the Dofus client;
- send input to the game, automate any game action, or read game data;
- capture, store or send screenshots or any player data;
- add game assets (images, sounds, fonts) from Ankama.

## Add a language

1. In `src/Penombre.cs`, find `static class T`.
2. Add a column to every entry of `texts` and pick it in `S()` for your two-letter language code (e.g. `"es"`).
3. Keep the `{0}` placeholders.
4. Test with `dist\Penombre.exe --lang=es`, open the settings and the zone editor, and check that nothing is cut off.
5. Mention the language in both READMEs.

## Pull requests

1. Fork, create a branch, make your change.
2. Check that `build.bat` compiles, run the [self-test](#self-test) and test on Windows with Dofus 3 (ideally with two clients for multi-account changes).
3. Add a line to `CHANGELOG.md` under **Unreleased**.
4. Open the pull request and fill in the checklist.

By contributing, you agree that your contribution is licensed under the [GPL-3.0](LICENSE), like the rest of the project.

## Release (maintainer)

1. Update `Program.Version` and the two `AssemblyVersion` / `AssemblyFileVersion` lines in `src/Penombre.cs`.
2. Rename the **Unreleased** section of `CHANGELOG.md` to the new version (e.g. `## 0.6.0`).
3. Commit, then tag: `git tag v0.6.0` and `git push --tags`.
4. GitHub Actions checks that the tag matches `Program.Version`, builds `Penombre.exe` and publishes the release with the CHANGELOG notes and the exe's SHA-256. The README download link always points to the latest release.
