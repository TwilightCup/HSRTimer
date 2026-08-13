# HSRTimer

A speedrun auto-timer plugin for *Human: Fall Flat* (`Human.exe` / `Human.app`).

HSRTimer maintains a precise **game-time** clock (stepped by the game's physics
frame, immune to lag and wall-clock manipulation), automatically detects level
start/stop from the game's authoritative state machine, validates runs against
configurable speedrun categories ("tags"), renders a configurable floating HUD,
and is fully localizable.

> **中文文档**: [README_zh.md](README_zh.md)

---

## Features (v1)

- **Game-time engine** — discrete `Time.fixedDeltaTime` accumulation; auto
  start/stop per level; segment and full-run tracking; pause/menu counting
  toggles; auto-reset on menu/lobby transitions.
- **Standard-style HUD** — configurable ordered rows, per-character two-color
  gradient with alpha, arbitrary custom texts at any position, draggable panel.
- **Categories & tags** — define rule sets via tags. Built-in: `Checkpoint`,
  `NoCheckpoint`, `Jumpless`, `Voiceline`. Extensible via an `ITagRule` API.
- **Checkpoint compliance** — skip detection (with built-in exception tables)
  and final-checkpoint validation.
- **Validity detection** — cheat codes, game-speed change, game-clock
  tampering; unforgivable vs forgivable flags.
- **One-key retry** — instantly reload the current level.
- **Localization** — community-translatable `key:translation` files; English is
  the shipped base; a Simplified Chinese example is included.

See [REQUIREMENTS.md](REQUIREMENTS.md) for the full specification.

## Install

1. Install **BepInEx** for *Human: Fall Flat* (tested with BepInEx 5.x / HarmonyX).
2. Build (see below) or obtain `HSRTimer.dll`.
3. Copy `HSRTimer.dll` into the game's `BepInEx/plugins/` folder.
4. Copy the `lang/*.txt` files into the plugin runtime dir
   `<BepInEx config dir>/HSRTimer/lang/`
   (the plugin creates this dir and ships defaults on first run if absent).
5. Launch the game; confirm `HSRTimer is loaded!` in the BepInEx console.

### Optional: Level Collections

If the [Level Collections](https://github.com/) (`LevelCollections`) plugin is
installed, HSRTimer integrates with it to treat a collection's final level as
the end of a full run. HSRTimer works fine without it (declared as a soft
dependency).

## Build

Requires the .NET SDK (`dotnet`) and the game installed via Steam.

```bash
dotnet build src/HSRTimer/HSRTimer.csproj
```

The build resolves game/BepInEx DLL references from the default Steam install
path (see `Directory.Build.props`). On a non-default Steam library or another
platform, override the paths via environment variables:

```bash
GAME_MANAGED="/path/to/Human_Data/Managed" \
BEPINEX_CORE="/path/to/BepInEx/core" \
dotnet build src/HSRTimer/HSRTimer.csproj
```

The output is `src/HSRTimer/bin/Debug/netstandard2.0/HSRTimer.dll`.

## Configuration

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

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Categories & tags](docs/CATEGORIES.md) · [Checkpoint rules](docs/CHECKPOINTS.md)
- [Configuration](docs/CONFIG.md) · [HUD](docs/HUD.md)
- [Settings panel](docs/PANEL.md)
- [Localization](docs/LOCALIZATION.md) · [Extending (custom tags)](docs/EXTENDING.md)
- [Voiceline detection](docs/VOICELINE.md)

## License

MIT. See [LICENSE](LICENSE).
