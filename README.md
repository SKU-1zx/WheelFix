# WheelFix

[![CI](https://github.com/SKU-1zx/WheelFix/actions/workflows/ci.yml/badge.svg)](https://github.com/SKU-1zx/WheelFix/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**A lightweight mouse-wheel debounce filter for Windows.**

> Recovery preview: `0.3.0-preview.3` is calibrated against a real trace from
> the failing mouse. It holds a possible reversal and, once confirmed, replays
> every held notch together so the filter can be more aggressive without
> losing scroll distance.

[Italiano](README.it.md)

![WheelFix interface preview](docs/interface-preview.svg)

Mechanical wheel encoders can wear out and start sending short pulses in the
wrong direction: you scroll down two notches and the page jumps back up one.
WheelFix removes those opposite-direction pulses before normal Windows
applications receive them.

## Features

- Global vertical-wheel filtering with no input delay in the normal direction.
- Adjustable reversal confirmation from 2 to 4 consecutive pulses.
- Light, Balanced and Strong presets; 3 pulses is recommended.
- Counter showing how many bad pulses were blocked.
- Notification-area controls and optional startup with Windows.
- Local diagnostic log, available directly from the notification-area menu.
- Automatic English/Italian interface based on the Windows display language.
- No installer, driver, telemetry, network access or administrator privileges.
- Held notches from a real reversal are replayed through native `SendInput`;
  mouse movement, clicks and keyboard input are never generated.

## Download and run

1. Download the latest Windows ZIP from
   [Releases](https://github.com/SKU-1zx/WheelFix/releases/latest).
2. Extract it to a stable folder.
3. Run `WheelFix.exe`.
4. Start with **Balanced (3 pulses)** and use the wheel normally.

Closing the window keeps WheelFix active in the notification area. Right-click
its icon to pause the filter, change strength, enable startup, open the
diagnostic log or exit.

## Tuning

| Preset | Confirmation | Suggested use |
| --- | ---: | --- |
| Light | 2 pulses | Rare faults and faster reversals |
| Balanced | 3 pulses | Value calibrated against the real trace |
| Strong | 4 pulses | An extremely damaged encoder |

Opposite pulses are held until they reach the selected confirmation count. If
the current direction returns, they are discarded as bounce; if the reversal
is confirmed, WheelFix replays them together. The first notches are therefore
not lost as they were in the two rejected aggressive previews.

The **Bad pulses blocked** counter confirms whether the filter is actually
intervening. If the counter increases while the unwanted jump disappears, the
setting is doing its job.

## How it works

WheelFix installs a standard `WH_MOUSE_LL` user-mode hook and observes only
vertical wheel messages. The current direction passes immediately. Opposite
pulses are held until confirmation; one `SendInput` event then restores the
complete accumulated delta and the new direction passes normally.

Mouse movement, buttons, horizontal scrolling and injected events from other
software are left untouched.

## Diagnostic log

Choose **Open diagnostic log** from the notification-area menu to open:

`%LOCALAPPDATA%\WheelFix\WheelFix.log`

The public release records WheelFix startup and shutdown, hook status, setting
changes, errors and grouped counts of blocked pulses. This diagnostic preview
also records every vertical wheel delta and filter decision. It still does
**not** record mouse movement, clicks or application names, and nothing is sent
over the network. The file is reset automatically before it exceeds 1 MB.

## Limitations

- Applications and games that consume the device directly through Raw Input
  may bypass a user-mode Windows hook.
- Windows may prevent `SendInput` from reaching an application running at a
  higher integrity level than WheelFix; the log then shows `REPLAY_FAILED`.
- One or two isolated notches in a new direction necessarily remain pending:
  from Windows wheel events alone they are indistinguishable from the measured
  bounce.
- WheelFix reduces symptoms caused by encoder bounce; it does not repair the
  physical encoder.
- The current release filters the vertical wheel only.
- The executable is not code-signed, so Windows may show a reputation warning
  on first launch.

A kernel filter driver could cover more input paths, but it would require
installation, elevation and driver signing. That is intentionally outside the
scope of this small portable utility.

## Build from source

Windows 10/11 with .NET Framework 4.x:

```bat
build-and-run.cmd
```

This uses the C# compiler already included with .NET Framework and writes
`dist\WheelFix.exe`. No NuGet packages or downloads are required.

The repository also contains `WheelFix.csproj`, which targets .NET Framework
4.8 and opens directly in Visual Studio or MSBuild.

Run the dependency-free core tests with:

```bat
test.cmd
```

## Startup and removal

The **Start automatically with Windows** option writes one value under the
current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` key.
Preferences are stored under `HKCU\Software\WheelFix`.

To remove WheelFix, disable startup, choose **Exit** from the notification-area
menu and delete its folder. Delete `%LOCALAPPDATA%\WheelFix` too if you also
want to remove the diagnostic log.

## Contributing

Bug reports are especially useful when they include the mouse model, Windows
version, affected application and the smallest confirmation value that works. See
[CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow.

## License

[MIT](LICENSE) © 2026 SKU-1zx.
