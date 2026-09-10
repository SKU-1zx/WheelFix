# Contributing to WheelFix

WheelFix deliberately stays small, portable and dependency-free. Changes should
preserve those properties unless there is a strong technical reason not to.

## Report a bug

Include:

- mouse make and model;
- Windows version;
- application where the problem appears;
- WheelFix reversal-confirmation value;
- a short input sequence, for example `down, down, false up`;
- whether the blocked-pulse counter increases.

## Develop

1. Fork and clone the repository on Windows.
2. Run `test.cmd --ci`.
3. Run `build.cmd --ci`.
4. Test the resulting `dist\WheelFix.exe` with the filter both enabled and
   paused.

`WheelFix.csproj` can also be opened in Visual Studio. The filter algorithm is
kept independent from WinForms in `src\WheelFilterCore.cs`; behavioural changes
should include a matching test in `tests\WheelFilterCoreTests.cs`.

Please keep pull requests focused and explain the user-visible effect of the
change.
