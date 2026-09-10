# Changelog

All notable changes to WheelFix are documented in this file.

## [Unreleased]

### Changed

- Replace the fixed debounce window with 2–4 pulse reversal confirmation.
- Replay confirmed reversal pulses through native `SendInput`, preserving the
  complete scroll distance instead of discarding the first notches.
- Calibrate the default 3-pulse mode against the captured 127-event hardware
  trace: all 33 false reversals are removed without dropping any of the 94
  correctly directed pulses.

### Diagnostic

- Keep the temporary raw wheel trace and report `HOLD`, `REPLAY`, or
  `REPLAY_FAILED` for this preview.

## [0.2.0] - 2026-08-18

### Added

- Local diagnostic logging for lifecycle, hook status, settings, errors and
  grouped blocked-pulse counts.
- Notification-area command to open the diagnostic log.

### Privacy

- The log stays under `%LOCALAPPDATA%`, never records individual mouse events
  or application names, and resets automatically at 1 MB.

## [0.1.1] - 2026-08-08

### Changed

- Simplified the filter state without changing its behaviour.
- Releases are now created only from version tags.

## [0.1.0] - 2026-08-07

### Added

- Global vertical-wheel debounce filter for Windows.
- Adjustable 10–150 ms debounce window and three presets.
- Notification-area controls, blocked-pulse counter and startup option.
- Automatic English and Italian interface.
- Portable local build with no external dependencies.
- Dependency-free tests for the filtering algorithm.
- Windows CI and automated release packaging.
