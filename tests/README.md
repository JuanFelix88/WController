# PositionalManager tests

Requires Windows and MSBuild with the .NET Framework 4.8 Developer Pack. No test packages are required; the test project links the production `PositionalManager.cs`.

```bat
msbuild tests\PositionalManager.Tests.csproj /p:Configuration=Debug /v:minimal
tests\bin\Debug\PositionalManager.Tests.exe
```

The default run checks 82 cases: spatial navigation/alignment, nearest-window replacement after close, prefix matching (case, diacritics, decomposed Unicode, native-title-only lookup and Z-order), character/dead-key editing, antialiased bitmap alpha (opaque ring, transparent center, fractional-alpha curves), query-tab layout, DPI scaling, and WCAG text contrast.

Optional desktop integration:

```bat
tests\bin\Debug\PositionalManager.Tests.exe --desktop
```

This briefly creates native test windows, moves focus, and sends keyboard input through a real global hook. Do not type during the run. It restores the previous foreground window and removes the hook/windows on exit. It checks activation after releasing Alt, the click-through per-pixel overlay, accented title search, Shift/Caps Lock, Backspace, Ctrl+Backspace with either Ctrl key and pending accents, no matches, modal key suppression, Enter autorepeat/release, Esc, query reset, closing/minimizing outside the mode, normal Del closure/refusal/retry, modifier protection, Del autorepeat protection, continuation after closing background/foreground selections, and external focus changes.

Set `WCPOS_TEST_SCREENSHOT` to an absolute PNG path to capture the selected test window and query tab during the desktop run.

Manual checks still needed: mixed-DPI monitors, virtual-desktop switching, elevated applications, and accent-color contrast on different backgrounds.
