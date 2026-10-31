# Changelog

## Contents

- [0.5.0](#050)
- [0.4.0](#040)
- [0.3.0](#030)
- [0.2.0](#020)
- [0.1.0](#010)

## 0.5.0

- New **look** for the settings window, in the spirit of the Dofus 3 interface: sections on the left, slate panels in relief, green sliders with a gem handle, glowing buttons, embedded Nunito font
- New **live preview** in the Mood section: a part of the game without and with the filter
- Moods shown as **swatches**, each one seen through its own filter
- New **notification area icons**: a moon egg that changes with the sky (default), a watcher egg that sleeps by day, or a round moon; chosen in Display
- New icon and logo (the moon egg)
- Times are typed in a dark time field; the tray menu uses the same colors
- The self-test checks the new pages, the fonts and the 36 notification icon pictures, and that two accounts with the same settings get the same tint
- Starting Pénombre again while it is running opens its settings window
- Accounts section: changing the mood, intensity or vignette of a character turns on its own settings right away (the controls were greyed out until the box was ticked)
- Fixed: a very thin window above Dofus (a tab handle of a window grouping tool, for example) cut a band in the filter

## 0.4.0

- New **per-account settings**: a character can use its own mood, intensity and vignette instead of the default ones; settings are kept for characters that are not open
- New **smooth transition** between moods, intensities and vignettes
- New **peek** shortcut (`Ctrl+Alt+C` by default): hold it to see the game without filter
- New **customizable shortcuts** (Shortcuts tab), with unavailable ones flagged
- New **real sunset and sunrise** schedule: pick a city or coordinates, computed offline
- New **notification area moon** that reflects the state: full, crescent, outline, grey
- New **only when Dofus is in the foreground** option
- New **vignette tint** and optional animated **film grain**
- New **portable mode**: settings and log in a `Penombre-data` folder next to the exe
- New **export / import** of the settings
- Bright zones can be **anchored to their nearest edge** and **grow with the window height** (default)
- Settings window reorganized in tabs
- Fixed: the diagnostics and the log showed Windows 6.2 instead of the real Windows version
- Fixed: the settings tabs could be too small for their content and show scroll bars; they are now sized on the largest tab
- A filter hidden or minimized by another program is shown again automatically
- Fixed: hovering a taskbar button (window previews) or Alt+Tab cut a band across the whole width of the filter
- Fixed: a filter just created, shown again or cut around a bright zone could stay invisible for a moment; it is now drawn right away
- Performance: shader frames are drawn by a render pump when the app is idle, instead of on the timer; they never delay shortcuts or window tracking. The shader runs at about 30 frames per second, the pace of the Windows Magnification API; Dofus windows that are not in front are refreshed at a quarter of that rate
- The peek shortcut stops by itself after one minute, in case a key stays stuck
- New self-test (`tools\selftest.bat`, test build only) that drives the app over running Dofus windows (one or several accounts) and checks 116 points

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
