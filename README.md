English | [中文](README_zh.md)

# HSRTimer

A speedrun auto-timer plugin for **Human: Fall Flat**.

HSRTimer maintains a precise **game-time** clock (stepped by the game's physics
frame, immune to lag and wall-clock manipulation), automatically detects level
start/stop from the game's authoritative state machine, validates runs against
configurable speedrun categories ("tags"), renders a configurable floating HUD,
and is fully localizable.

If the [Level Collections](https://github.com/HeyBlack233/LevelCollections) (`LevelCollections`) plugin is
installed, HSRTimer integrates with it to treat a collection's final level as
the end of a full run. HSRTimer works fine without it (declared as a soft
dependency).

## Configuration

### Config panel

All features can be configured using the in-game settings panel.
Default keybind for opening the panel is `HOME`.

### Config files

All config lives under `<BepInEx config dir>/HSRTimer/`:

| File | Contents |
|------|----------|
| `settings.ini` | Toggles, keybinds, language, drift tolerance |
| `tags.ini` | Enabled rule tags (the active rule set — no category presets) |
| `layout.ini` | HUD rows, custom texts, panel position, gradient colors |
| `lang/*.txt` | Localization files |

See [docs/CONFIG.md](docs/CONFIG.md) for every key. Config files are
human-readable text; malformed lines are skipped with a logged warning that
includes the file name and line number (the plugin never fails to start because
of a single bad line).

## Default keybinds

| Action | Default key |
|--------|-------------|
| Reset run | `Backspace` |
| Retry level | `R` |
| Open/close settings panel | `Home` |

All settings can be changed live in the settings panel (press `Home`). Rebind
keys there too — focus a keybind field and press the desired key. Changes are
saved when the panel is closed or the game exits. See [docs/CONFIG.md](docs/CONFIG.md).

## Build

Requires the .NET SDK (`dotnet`) and the game installed via Steam.

```bash
dotnet build src/HSRTimer/HSRTimer.csproj
```

The build resolves game/BepInEx DLL references from `GAME_MANAGED` /
`BEPINEX_CORE`. On this machine those paths live in the gitignored
`Directory.Build.user.props`; on another machine or a non-default Steam library,
set them via environment variables:

```bash
GAME_MANAGED="/path/to/Human_Data/Managed" \
BEPINEX_CORE="/path/to/BepInEx/core" \
dotnet build src/HSRTimer/HSRTimer.csproj
```

The output includes a versioned copy at `src/HSRTimer/bin/Debug/netstandard2.0/HSRTimer-v{version}.dll`; deploy that file. The unversioned `HSRTimer.dll` is also emitted for external plugin compatibility.

## License

MIT. See [LICENSE](LICENSE).
