<p align="center">
  <img src="assets/logo.png" width="96" alt="Pénombre logo">
</p>

<h1 align="center">Pénombre</h1>

<p align="center">
  A <b>night mode</b> for Dofus 3, like the one Dofus 1.29 had.<br>
  One small <code>.exe</code>, nothing to install. Windows 10 and 11 (64-bit).
</p>

<p align="center">
  <a href="https://github.com/Sokenzane/Penombre/releases/latest"><img src="https://img.shields.io/github/v/release/Sokenzane/Penombre?label=download" alt="Latest release"></a>
  <a href="https://github.com/Sokenzane/Penombre/actions/workflows/build.yml"><img src="https://github.com/Sokenzane/Penombre/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-blue" alt="License: GPL-3.0"></a>
</p>

<p align="center">
  <a href="https://github.com/Sokenzane/Penombre/releases/latest/download/Penombre.exe"><b>Download Penombre.exe</b></a>
  &nbsp;·&nbsp; <a href="README.fr.md">Lire en français</a>
</p>

## Contents

- [What is it?](#what-is-it)
- [Is it safe for my account?](#is-it-safe-for-my-account)
- [Download](#download)
- [How to use](#how-to-use)
  - [Keyboard shortcuts](#keyboard-shortcuts)
  - [Settings window](#settings-window)
  - [Moods](#moods)
  - [Automatic mode](#automatic-mode)
  - [Bright zones](#bright-zones)
  - [Several accounts](#several-accounts)
  - [Notification area icon](#notification-area-icon)
  - [Portable mode and backup](#portable-mode-and-backup)
- [How it works](#how-it-works)
  - [Renderers](#renderers)
  - [Multi-account](#multi-account)
  - [Performance](#performance)
- [Files](#files)
- [Diagnostics and log](#diagnostics-and-log)
- [Known limitations](#known-limitations)
- [Build from source](#build-from-source)
- [Contributing](#contributing)
- [License](#license)

## What is it?

*"Pénombre"* is French for *"half-light"*. Dofus 1.29 could turn the game to night; Dofus 3 cannot. Pénombre lays a click-through filter over the game area that darkens it, cools the colors down and lifts the blacks a little, for a nocturnal mood:

- **Real color rework** (shader): every pixel is redrawn through a color matrix, not just covered with a dark layer.
- **Moods**: blue night, moonlight, dusk, pitch black, old-school night, or your own.
- **Follows the real time** if you want, with fixed times or the **real sunset and sunrise** of your city, and smooth transitions.
- **Multi-account ready**: one filter per Dofus window, which never stacks up, and **per-account settings**.
- **Bright zones**: keep the spell bar or the chat readable.
- **Peek**: hold a shortcut to see the game without filter.
- Optional **vignette** (with your own tint) and **film grain**.

## Is it safe for my account?

Pénombre **never touches the Dofus client**: no injection, no memory reading, nothing is sent to the game or to the servers. The filter is a separate Windows window placed over the game, like a screen tint (f.lux, Windows night light…). It only uses public Windows functions to find the game windows and draw over them.

Ankama has not reviewed or approved Pénombre. Read the game's terms of use yourself and use it at your own risk.

## Download

**[Download Penombre.exe](https://github.com/Sokenzane/Penombre/releases/latest/download/Penombre.exe)** and run it. No installer, no admin rights. Release notes and previous versions are on the [Releases](https://github.com/Sokenzane/Penombre/releases) page, with the SHA-256 of each exe (see [SECURITY.md](SECURITY.md#verifying-a-download)). It needs .NET Framework 4, which comes with every Windows 10 and 11.

> The exe is not code-signed, so Windows SmartScreen may show *"Windows protected your PC"*. Click **More info**, then **Run anyway**. You can also [build it yourself](#build-from-source) in one click.

Dofus must run **windowed** or **fullscreen windowed**: in exclusive fullscreen, no window can be drawn over the game.

## How to use

Run `Penombre.exe`: a moon egg appears in the notification area (next to the clock, sometimes hidden behind the `^` arrow). Left-click it to turn the filter on or off, right-click it for the menu.

### Keyboard shortcuts

| Default shortcut | Action |
| --- | --- |
| `Ctrl+Alt+N` | Filter on / off (with a fade) |
| `Ctrl+Alt+C` | **Peek**: hold it to see the game without filter |
| `Ctrl+Alt+A` | Automatic mode on / off |
| `Ctrl+Alt+P` | Next mood |
| `Ctrl+Alt+PgUp` / `PgDn` | Intensity +5 % / -5 % |
| `Ctrl+Alt+V` | Next vignette (none, light, medium, strong) |

Every shortcut can be changed in **Settings… > Shortcuts** (click a box, press the new combination; `Backspace` removes it). A shortcut already taken by another app is marked *unavailable*. When the Dofus in the foreground has its [own settings](#several-accounts), the mood, intensity and vignette shortcuts change those.

### Settings window

Menu **Settings…**. The window follows the spirit of the Dofus 3 interface: sections on the left, green sliders and buttons. Everything applies live, with a smooth transition:

| Tab | Content |
| --- | --- |
| Mood | live preview (a part of the game without and with the filter), moods as swatches, custom mood sliders, intensity, vignette and its tint, film grain, renderer |
| Schedule | automatic mode (fixed times or real sun), transition duration (0 to 2 s) |
| Accounts | per-account settings |
| Display | notification area icon, bright zones, "only when Dofus is in the foreground" |
| Shortcuts | every shortcut, customizable |
| General | start with Windows, portable mode, settings folder, export / import, log |

### Moods

The intensity (5 to 100 %) blends between the original picture and the full mood.

| Mood | Brightness | Saturation | Tint |
| --- | --- | --- | --- |
| Blue night | 55 % | 55 % | cold blue |
| Moonlight | 70 % | 30 % | pale blue, lifted blacks |
| Dusk | 72 % | 80 % | warm purple |
| Pitch black | 38 % | 50 % | light blue |
| Old-school night | 62 % | 90 % | blue, 1.29 style |
| Custom | your choice | your choice | cold ↔ warm, lifted blacks |

Moving a slider in the settings creates a **Custom** mood from the current one.

### Automatic mode

The filter follows your PC's clock, with two ways to set the times:

- **Fixed times** (below);
- **Real sunset and sunrise**: pick your city (or enter coordinates). Night falls 45 min before sunset and is complete 45 min after; dawn works the same way around sunrise. Computed offline, so the times follow the seasons.

| Moment | Default | Filter |
| --- | --- | --- |
| Dusk starts | 19:30 | rises gradually |
| Full night | 21:30 | full effect |
| Dawn starts | 06:30 | fades out gradually |
| Full day | 08:30 | none (shader paused) |

### Bright zones

To keep the spell bar, the chat or the timeline readable: **Settings… > Draw the zones…**. A layer appears over the Dofus window: drag to draw a zone, right-click a zone to remove it, `Del` to clear all, `Enter` to save, `Esc` to cancel. Zones apply to every account. By default they are **anchored to their nearest edge** (a spell bar at the bottom stays at the bottom) and **grow with the window height**, like the Dofus interface; untick both options in **Display** for zones proportional to the window.

### Several accounts

Each character can be filtered or not, from the **Accounts** menu or the settings. In **Settings… > Accounts**, a character can also get **its own mood, intensity and vignette** instead of the default ones. Characters are recognized from their window title, and their settings are kept even when they are not open.

Option **Only when Dofus is in the foreground** (Display tab): the filter only shows on the Dofus window you are playing.

### Notification area icon

The icon shows the state at a glance: night, dusk or dawn, day (automatic mode, no filter), filter off. Three styles to choose from in **Settings… > Display**:

| Style | Night | Dusk | Day | Off |
| --- | --- | --- | --- | --- |
| **Moon egg** (default) | sapphire egg, golden moon | pink and amber egg | amber egg, sun | cracked stone egg |
| **Watcher egg** | eyes open | eyes open | asleep | grey, asleep |
| **Round moon** | full moon | crescent | outline | grey crescent |

### Portable mode and backup

- **Portable mode** (General tab): settings and log move to a `Penombre-data` folder next to the exe. Copy the folder with the exe to another PC and everything comes along, zones included. Untick it to move them back to `%APPDATA%`.
- **Export… / Import…**: save your settings to a file, or load a file saved on another PC.

## How it works

The Dofus 3 windows are found automatically (window class `UnityWndClass` in a `Dofus` process).

### Renderers

- **Shader** (default): the Windows Magnification API redraws the game area through a 5×5 color matrix. Brightness is multiplied, colors are desaturated, tinted, and the blacks lifted. It is the same idea as the Flash `ColorTransform` behind the night of Dofus 1.29.
- **Veil**: a plain semi-transparent colored layer, used as a fallback when the shader is not available.
- **Vignette** (optional, with both renderers): darker edges and corners, clear center, drawn with per-pixel alpha.

### Multi-account

Each filter is clipped (`SetWindowRgn`) to the part of its Dofus window that is **actually visible**. Pénombre walks the windows from the top of the Windows Z-order down: whatever lies above a Dofus window (another app, another client) hides the part it covers. Two filters never overlap, so the darkening never stacks up when you switch between accounts.

Click-through windows (GPU, Discord or Steam overlays…), invisible and cloaked windows (other virtual desktops) are not treated as obstacles.

### Performance

- Pénombre reacts to Windows events (a window moved, shown, hidden, brought to the front…) instead of checking everything in a loop. A safety check runs every 500 ms.
- Shader: 60 frames per second for the Dofus window in the foreground, about 20 for the ones in the background.
- The shader pauses when Dofus is minimized or fully hidden, when the filter is off, or in full day (automatic mode). The app then idles at 10 checks per second.

## Files

In `%APPDATA%\Penombre\` (or `Penombre-data` next to the exe in [portable mode](#portable-mode-and-backup)):

- `settings.ini`: all the settings, per-account settings and shortcuts included. If your client is not called `Dofus.exe`, edit the `process=` line. Delete the file to reset everything.
- `penombre.log`: the log (start, game windows found, errors). Past 512 KB it is renamed to `penombre.log.old`.

The interface follows the Windows display language (French or English); `--lang=fr` or `--lang=en` forces it. Start with Windows writes one value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; nothing else is written to your system.

## Diagnostics and log

- **Copy diagnostics**: copies the full state to the clipboard (Windows version, Dofus windows, filters, automatic mode, zones, accounts, Windows Z-order). It contains your window titles, so your character names: hide them before posting it publicly if you prefer.
- **Open the log**: opens `penombre.log` in Notepad.

## Known limitations

- Exclusive fullscreen is not supported (see [Download](#download)).
- The game interface is darkened too, unless you set [bright zones](#bright-zones). If they drift after a resize, try the other zone options or redraw them.
- The shader is a per-pixel color transform (matrix), not a programmable GPU shader: no blur, bloom or local lighting.
- With the shader, the filtered picture can be one frame behind the game.
- Film grain uses about 25 MB of memory per Dofus window, and takes a fraction of a second to prepare when turned on or when the window is resized.
- Sun times assume your PC's time zone is the one of the chosen city.

## Build from source

The code is in [`src/Penombre.cs`](src/Penombre.cs) and the interface in [`src/Interface.cs`](src/Interface.cs) (C# 5, WinForms, no dependency).

1. Clone or download the repository.
2. Double-click **`build.bat`**: it uses the C# compiler that ships with Windows.
3. Run `dist\Penombre.exe`.

The icons and the logo are generated by `python tools/make_icon.py` (needs Pillow and Playwright).

## Contributing

Bug reports, ideas and pull requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Pénombre is free software, released under the [GNU General Public License v3.0](LICENSE).
You can use, study, share and modify it; modified versions you distribute must stay open source under the same license and credit the original project.

Copyright © 2026 [Sokenzane](https://github.com/Sokenzane).

The [Nunito](https://github.com/googlefonts/nunito) font (© The Nunito Project Authors), embedded in the exe, is under the [SIL Open Font License 1.1](assets/fonts/OFL.txt).

Pénombre is an independent fan project, not affiliated with or endorsed by Ankama. Dofus is a trademark of Ankama. No game asset is included in this repository.
