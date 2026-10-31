<p align="center">
  <img src="assets/logo.png" width="96" alt="Pénombre logo">
</p>

<h1 align="center">Pénombre</h1>

<p align="center">
  A <b>night mode</b> for Dofus 3, like the one in Dofus 1.29.<br>
  One small <code>.exe</code>, nothing to install. Windows 10 and 11 (64-bit).
</p>

<p align="center">
  <a href="https://github.com/Sokenzane/Penombre/releases/latest"><img src="https://img.shields.io/github/v/release/Sokenzane/Penombre?label=download" alt="Latest version"></a>
  <a href="https://github.com/Sokenzane/Penombre/actions/workflows/build.yml"><img src="https://github.com/Sokenzane/Penombre/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-blue" alt="License: GPL-3.0"></a>
</p>

<p align="center">
  <a href="https://github.com/Sokenzane/Penombre/releases/latest/download/Penombre.exe"><b>Download Penombre.exe</b></a>
</p>

<p align="center">
  <a href="README.md">Français</a> · <b>English</b> · <a href="README.es.md">Español</a>
</p>

<p align="center">
  <img src="assets/readme/banner.jpg" alt="An Astrub map without filter on the left, and with Pénombre (Blue night) on the right">
</p>

## Contents

- [What is it?](#what-is-it)
- [Is it safe for my account?](#is-it-safe-for-my-account)
- [Download](#download)
- [Usage](#usage)
  - [Shortcuts](#shortcuts)
  - [Settings window](#settings-window)
  - [Moods](#moods)
  - [Automatic mode](#automatic-mode)
  - [Bright zones](#bright-zones)
  - [Several accounts](#several-accounts)
  - [Notification area icon](#notification-area-icon)
  - [Portable mode and backup](#portable-mode-and-backup)
  - [Updates](#updates)
- [How it works](#how-it-works)
  - [Renderers](#renderers)
  - [Multi-account](#multi-account)
  - [Performance](#performance)
- [Files](#files)
- [Diagnostics and log](#diagnostics-and-log)
- [Known limits](#known-limits)
- [Build it yourself](#build-it-yourself)
- [Contributing](#contributing)
- [License](#license)

## What is it?

Dofus 1.29 could switch the game to night mode; Dofus 3 cannot. Pénombre lays a click-through filter over the game area that darkens it, cools the colors and slightly lifts the blacks, for a night mood:

- **Real color work** (shader): every pixel goes through a color matrix, instead of just being covered by a dark layer.
- **Moods**: blue night, moonlight, dusk, pitch black, old-school night, or your own.
- **Follows the real time** if you want, with fixed times or the **real sunset and sunrise** of your city, and smooth transitions.
- **Multi-account ready**: one filter per Dofus window, no stacked darkening, and **per-account settings**.
- **Bright zones**: the spell bar or the chat stay readable.
- **Peek**: hold a shortcut to see the game without filter.
- Optional **vignette** (any tint) and **film grain**.

## Is it safe for my account?

Pénombre **never touches the Dofus client**: no injection, no memory reading, nothing is sent to the game or its servers. The filter is a separate Windows window laid over the game, like a screen tint (f.lux, Windows Night light…). It only uses public Windows functions to find the game windows and draw over them. Its only Internet connection is the [update check](#updates), to GitHub.

Ankama has neither reviewed nor approved Pénombre. Read the game's terms of use yourself, and use it at your own risk.

## Download

**[Download Penombre.exe](https://github.com/Sokenzane/Penombre/releases/latest/download/Penombre.exe)** and run it. No installation, no administrator rights. To carry it on a USB stick or across PCs, tick the [portable mode](#portable-mode-and-backup). Release notes and previous versions are on the [Releases](https://github.com/Sokenzane/Penombre/releases) page, with the SHA-256 of each exe (see [SECURITY.md](SECURITY.md#vérifier-un-téléchargement)). It needs .NET Framework 4, included in every Windows 10 and 11.

> The exe is not signed: Windows SmartScreen may show *"Windows protected your PC"*. Click **More info**, then **Run anyway**. You can also [build it yourself](#build-it-yourself) in one click.

Dofus must run **windowed** or **fullscreen windowed**: in exclusive fullscreen, no window can be shown over the game.

## Usage

Run `Penombre.exe`: a moon egg appears in the notification area (near the clock, sometimes hidden behind the `^` arrow). Left-click it to turn the filter on or off, right-click for the menu. **Pause** (in the menu) turns the filter off for 15 min, 30 min, 1 h or 2 h, then brings it back by itself; `Ctrl+Alt+N` or "Resume now" bring it back earlier. Starting `Penombre.exe` again while it runs opens the settings window.

### Shortcuts

| Default shortcut | Action |
| --- | --- |
| `Ctrl+Alt+N` | Filter on / off (with fade) |
| `Ctrl+Alt+C` | **Peek**: hold it to see the game without filter |
| `Ctrl+Alt+A` | Automatic mode on / off |
| `Ctrl+Alt+P` | Next mood |
| `Ctrl+Alt+PgUp` / `PgDn` | Intensity +5 % / -5 % |
| `Ctrl+Alt+V` | Next vignette (none, light, medium, strong) |

Every shortcut can be changed in **Settings… > Shortcuts** (click a box, press the new combination; `Backspace` removes it). A shortcut already taken by another app is marked *unavailable*. When the Dofus in the foreground has its [own settings](#several-accounts), the mood, intensity and vignette shortcuts change those.

### Settings window

Menu **Settings…**. The window follows the spirit of the Dofus 3 interface: sections on the left, green sliders and buttons. Everything applies live, with a smooth transition:

<p align="center">
  <img src="assets/readme/settings-mood.png" width="49%" alt="Mood section: preview without and with Pénombre, mood swatches, sliders">
  <img src="assets/readme/settings-display.png" width="49%" alt="Display section: icon choice, bright zones">
</p>

The screenshots show the French interface; on an English Windows, Pénombre shows in English.

| Section | Content |
| --- | --- |
| Mood | live preview (part of the game without and with the filter), mood swatches, custom mood sliders, intensity, vignette and its tint, film grain, renderer |
| Schedule | automatic mode (fixed times or real sun), dusk colours, transition length (0 to 2 s) |
| Accounts | per-account settings |
| Display | notification area icon, bright zones, "only when Dofus is in the foreground" |
| Shortcuts | every shortcut, customizable |
| General | start with Windows, portable mode, updates, settings folder, export / import, reset, log |

### Moods

Intensity (5 to 100 %) blends between the original picture and the full mood.

<p align="center">
  <img src="assets/readme/moods.jpg" alt="The same scene without filter, then with each mood, at 80 % intensity">
</p>

| Mood | Brightness | Saturation | Tint |
| --- | --- | --- | --- |
| Blue night | 55 % | 55 % | cold blue |
| Moonlight | 70 % | 30 % | pale blue, lifted blacks |
| Dusk | 72 % | 80 % | warm purple |
| Pitch black | 38 % | 50 % | light blue |
| Old-school night | 62 % | 90 % | blue, 1.29 style |
| Custom | adjustable | adjustable | cold ↔ warm, lifted blacks |

Moving a slider in the settings creates a **Custom** mood from the current one.

### Automatic mode

The filter follows your PC's clock, with two ways to set the times:

- **Fixed times** (below);
- **Real sunset and sunrise**: pick your city (or type coordinates). Night falls 45 min before sunset and is complete 45 min after; dawn works the same around sunrise. Computed offline, and the times follow the seasons.

| Moment | Default | Filter |
| --- | --- | --- |
| Dusk starts | 19:30 | rises gradually |
| Full night | 21:30 | full effect |
| Dawn starts | 06:30 | fades gradually |
| Full day | 08:30 | none (shader paused) |

**Dusk colours** (Schedule section, on by default): while night falls and while day breaks, the filter takes the warm colours of the Dusk mood, strongest halfway, then goes back to your night mood. It is the "night is falling" effect of Dofus 1.29.

### Bright zones

To keep the spell bar, the chat or the timeline readable: **Settings… > Display > Draw the zones…**. A layer appears over the Dofus window: drag to draw a zone, right-click a zone to remove it, `Del` to clear all, `Enter` to save, `Esc` to cancel. Zones apply to every account. By default they are **anchored to the nearest edge** (a spell bar at the bottom stays at the bottom) and **grow with the window height**, like the Dofus interface; untick both options in **Display** for zones proportional to the window.

### Several accounts

Each character can be filtered or not, from the **Accounts** menu or the settings. In **Settings… > Accounts**, a character can also have **its own mood, intensity and vignette** instead of the default ones: changing any of the three turns its own settings on. Characters are recognized from their window title, and their settings are kept even when they are not open.

Option **Only when Dofus is in the foreground** (Display section): the filter only shows on the Dofus window you are playing in.

### Notification area icon

Three styles to choose from in **Settings… > Display**. With **Match the icon to the time of day** ticked (default), the icon follows the sky according to the [automatic mode](#automatic-mode) times: night, dusk or dawn, day. It does so even when the automatic mode is off: the icon shows the time of day, the filter keeps its strength. Unticked, click the picture you want (night, dusk or day) to keep it. In every case, the grey picture means the filter is off.

| Style | Night | Dusk | Day | Off |
| --- | --- | --- | --- | --- |
| **Moon egg** (default) | sapphire egg, golden moon | pink and amber egg | amber egg, sun | cracked stone egg |
| **Watcher egg** | eyes open | eyes open | asleep | grey, asleep |
| **Round moon** | full moon | crescent | outline | grey crescent |

### Portable mode and backup

- **Portable mode** (General section): settings and log move to a `Penombre-data` folder next to the exe. If that folder exists at start, Pénombre uses it right away. Copy the folder with the exe to another PC and everything follows, zones included. Untick to move them back to `%APPDATA%`.
- **Export… / Import…**: save your settings to a file, or load a file saved on another PC.
- **Check for updates**: see [Updates](#updates).
- **Reset settings…**: puts everything back as on the first start (after a confirmation). The log, the settings folder and start with Windows stay as they are.

### Updates

At start, then once a day, Pénombre asks GitHub for the latest published version. If it is newer, a balloon says so, and a **New version available…** line appears at the top of the menu: one click opens the download page. Nothing is downloaded or installed by itself. GitHub only sees your IP address, as with any web page. Untick **Check for updates** (General section) to never connect; the **Check now** button stays available.

## How it works

Dofus 3 windows are detected automatically (window class `UnityWndClass` in a `Dofus` process).

### Renderers

- **Shader** (default): the Windows Magnification API redraws the game area through a 5×5 color matrix. Brightness is multiplied, colors desaturated and tinted, blacks lifted. Same principle as the Flash `ColorTransform` behind the Dofus 1.29 night.
- **Veil**: a simple semi-transparent colored layer, as a fallback when the shader is not available.
- **Vignette** (optional, with both renderers): darker edges and corners, clear center, drawn with per-pixel alpha.

### Multi-account

Each filter is clipped (`SetWindowRgn`) to the **actually visible** part of its Dofus window. Pénombre walks the Windows stack from top to bottom: anything above a Dofus window (another app, another client) hides the covered part. Two filters never overlap, and the darkening does not stack when you switch accounts.

Click-through windows (GPU, Discord, Steam overlays…), invisible, cloaked (other virtual desktops) or very thin windows (tab handles of window-grouping tools) do not count as obstacles.

### Performance

- Pénombre reacts to Windows events (window moved, shown, hidden, brought to the front…) instead of checking everything in a loop. A safety check runs every 500 ms.
- Shader: about 30 frames per second for the Dofus in the foreground (the pace of the Windows Magnification API), about 7 for those in the background.
- The shader pauses when Dofus is minimized or fully hidden, when the filter is off, or in full day (automatic mode). The program then idles (10 checks per second).

## Files

In `%APPDATA%\Penombre\` (or `Penombre-data` next to the exe in [portable mode](#portable-mode-and-backup)):

- `settings.ini`: every setting, including per-account settings and shortcuts. If your client is not called `Dofus.exe`, edit the `process=` line. To start over: **Reset settings…** (General section), or delete the file.
- `penombre.log`: the log (start, windows found, errors). Past 512 KB, it is renamed `penombre.log.old`.

The interface follows the Windows language (French, English or Spanish). To pick another one, click the small language button (`EN ▾`) at the top left of the settings window; `--lang=fr`, `--lang=en` or `--lang=es` forces it at start. Start with Windows writes a single value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; nothing else is written to your system.

## Diagnostics and log

- **Copy diagnostics**: copies the full state to the clipboard (Windows version, Dofus windows, filters, automatic mode, zones, accounts, Windows stack). It contains your window titles, so your character names: hide them before posting if you prefer.
- **Open the log**: opens `penombre.log` in Notepad.

## Known limits

- Exclusive fullscreen is not supported (see [Download](#download)).
- The game interface is darkened too, unless you set [bright zones](#bright-zones). If they shift after a resize, try the other zone options or draw them again.
- The shader is a per-pixel color transform (matrix), not a programmable GPU shader: no blur, bloom or local light.
- With the shader renderer, the filtered picture can be one frame behind the game.
- Film grain uses about 25 MB of memory per Dofus window, and takes a fraction of a second to prepare when turned on or resized.
- Sun times assume the PC's time zone is the one of the chosen city.

## Build it yourself

The code is in [`src/Penombre.cs`](src/Penombre.cs) and the interface in [`src/Interface.cs`](src/Interface.cs) (C# 5, WinForms, no dependency).

1. Clone or download the repository.
2. Double-click **`build.bat`**: it uses the C# compiler that ships with Windows.
3. Run `dist\Penombre.exe`.

Icons and logo are generated by `python tools/make_icon.py` (needs Pillow and Playwright).

## Contributing

Bug reports, ideas and pull requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) (in French).

## License

Pénombre is free software, released under the [GNU General Public License v3.0](LICENSE).
You can use, study, share and modify it; modified versions you distribute must stay open source under the same license and credit the original project.

Copyright © 2026 [Sokenzane](https://github.com/Sokenzane).

The [Nunito](https://github.com/googlefonts/nunito) font (© The Nunito Project Authors), embedded in the exe, is under the [SIL Open Font License 1.1](assets/fonts/OFL.txt).

Pénombre is an independent fan project, neither affiliated with nor endorsed by Ankama. Dofus is a trademark of Ankama. No game asset is included in this repository.
