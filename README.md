# WheelFix

[![CI](https://github.com/SKU-1zx/WheelFix/actions/workflows/ci.yml/badge.svg)](https://github.com/SKU-1zx/WheelFix/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**A lightweight mouse-wheel debounce filter for Windows.**

[Italiano](README.it.md)

![WheelFix interface preview](docs/interface-preview.svg)

Mechanical wheel encoders can wear out and start sending short pulses in the
wrong direction: you scroll down two notches and the page jumps back up one.
WheelFix removes those opposite-direction pulses before normal Windows
applications receive them.

## Gesture direction correction — 0.3.2-preview.1

The first nonzero physical wheel event selects `LOCK_UP` or `LOCK_DOWN`.
Every subsequent physical pulse retains that direction until there has been
at least **450 ms** without physical wheel input. Contrary pulses are replaced
one-for-one using `SendInput`, preserving magnitude; matching pulses pass unchanged.
No voting, reversal confirmation, adaptive extension, replay or scroll queue is used.

The reset is evaluated before processing the next physical event. This avoids
timer races and introduces no wait on that event. Diagnostics include the logical
idle deadline (`reset_at`). Injected flags and WheelFix's own marker bypass the
filter before its state or timestamp can change. Failed injection suppresses the
contrary original, logs `sendinput_failed`, and does not retry.

Presets are 400 / **450 default** / 500 ms. The new `GestureGapMs` setting starts
at 450 ms instead of inheriting the previous `BurstGapMs` value.

## Build and test

On Windows with .NET Framework 4.x, run `build.cmd --ci`, then `test.cmd --ci`.
The portable executable is `dist\WheelFix.exe`; the project also supports
Visual Studio/MSBuild targeting .NET Framework 4.8. Tests cover A–F (direction
locks, random pulses, exact idle boundaries and reentrant hook routing), timestamp
wrap, magnitude, settings resets and injection failures. No NuGet packages are needed.

## Diagnostics and real mouse check

Exit the old tray instance before running this preview. Select Balanced (450 ms).
Scroll down continuously for 5–10 seconds, pause about half a second, then scroll up.
Repeat in a normal Windows app and check for stalls or inversions.
Open the diagnostic log from the tray and return the relevant section of
`%LOCALAPPDATA%\WheelFix\WheelFix.log` if the result is wrong.
It records raw direction, state, raw gap, forwarded/injected output, correction or
bypass reason, idle reset and injection failures. Disk writes occur outside the
hook; trace storage is bounded and reports overflow. Logs stay local and rotate
at 1 MB. They do not contain cursor movement, clicks or application names.

## Limits

- The first event after idle determines direction, even if that first event is
  itself wrong. Random UP/DOWN cannot reveal the user's physical intent.
- Raw gaps at or above the configured threshold start another gesture, even if
  the user regards the physical motion as continuous.
- `SendInput` can be blocked by Windows integrity restrictions for elevated apps.
- Apps using device Raw Input may bypass a user-mode hook.
- Only vertical wheel events are filtered. Movement, buttons, horizontal scrolling
  and events injected by other software are untouched.
- This mitigates faulty output; it does not repair the encoder.

[Italian usage and settings](README.it.md) · [MIT license](LICENSE)
