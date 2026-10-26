# Changelog

## Contents

- [0.3.0](#030)
- [0.2.0](#020)
- [0.1.0](#010)

## 0.3.0

- New **automatic mode**: the filter follows the real time (dusk, night, dawn, day), with adjustable times; shortcut `Ctrl+Alt+A`
- New **smooth fade** when turning the filter on or off and at start (adjustable duration)
- New **settings window**, applied live
- New **Custom** mood: brightness, saturation, temperature, lifted blacks
- New **per-account setting**: each character can be filtered or not
- New **bright zones**: parts of the game interface (spells, chat…) stay unfiltered, with an editor drawn over the Dofus window
- New **start with Windows** option
- New **error log** (`%APPDATA%\Penombre\penombre.log`) and "Open the log" menu
- Performance: reacts to Windows events instead of checking in a loop, shader at 20 frames per second for background Dofus windows, idles when no filter is shown

## 0.2.0

- New **shader** renderer: the game area is redrawn through a color matrix (brightness, saturation, tint, bluish blacks); the veil stays available as a fallback
- New **vignette** (none, light, medium, strong); shortcut `Ctrl+Alt+V`
- New mood: Old-school night
- Fixed: with several accounts, switching between Dofus windows made the game darker and darker. Each filter is now clipped to the visible part of its window, so filters never overlap
- Fixed: simpler foreground handling (filters always on top, but cut around the windows that cover Dofus)
- New **Copy diagnostics** menu
- 64-bit executable

## 0.1.0

- Tinted, click-through filter fitted to the game area of each Dofus 3 window
- Four moods, adjustable intensity, global shortcuts, notification area icon and menu
- Settings saved, interface in French and English
