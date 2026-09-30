# Contributing to HSRTimer

Thanks for your interest in contributing! HSRTimer is a BepInEx 5 plugin
(C#, `netstandard2.0`) that adds a speedrun auto-timer to *Human: Fall Flat*.
Translations, code, documentation, and bug reports are all welcome.

Three things are worth knowing before you start:

- **`REQUIREMENTS.md` (Chinese) is the source of truth.** Every behavior maps to
  a requirement ID (**R1**–**R13**, plus Appendices A/B), and those IDs show up
  in code comments and throughout the docs. Before changing
  timing/segment/reset/checkpoint/validity/retry/localization behavior, find the
  governing R-number. If your change alters specified behavior, update
  `REQUIREMENTS.md` too — a maintainer can help with the Chinese wording.
- **Docs are bilingual.** `docs/*.md` is English; `docs/zh/*.md` is its mirror.
  If you edit a doc under `docs/`, update the Chinese counterpart in the same
  pull request (or say so in the PR and a maintainer will handle it).
- **Never bump the version.** On `dev` the version is always `0.0.0` in both
  `src/HSRTimer/HSRTimer.csproj` and `src/HSRTimer/PluginInfo.cs`. Version bumps
  happen only on maintainer release branches.

## 1. Translations

The UI is fully localizable. English (`src/HSRTimer/lang/en.txt`) is the shipped
base and the authoritative key set; a Simplified Chinese example
(`zh-Hans.txt`) is included.

The full contributor guide is
**[docs/LOCALIZATION.md](docs/LOCALIZATION.md)** (中文: [docs/zh/LOCALIZATION.md](docs/zh/LOCALIZATION.md)) —
it covers BCP 47 file naming, the `key:translation` format, `#` comments, `\n`
escapes, UTF-8 (no BOM), and the fallback chain.

Quick start:

1. Copy `src/HSRTimer/lang/en.txt` to `src/HSRTimer/lang/<your-code>.txt`.
2. Translate the right-hand side of each line. Keep the keys unchanged.
3. Set `__LANG_NAME__:` to the display name of your language.
4. Copy the file into `<BepInEx config dir>/HSRTimer/lang/` and hot-reload it in
   game with `hsr lang set <code>` (or the **Reload Language** key, default
   `F10`) to check it.
5. Open a pull request against **`dev`**.

## 2. Code — custom tags and settings tabs

HSRTimer's category system is extensible: any BepInEx plugin can register a
custom validity rule (a new "tag"), and any plugin can add its own tab to
HSRTimer's settings panel. See
**[docs/EXTENDING.md](docs/EXTENDING.md)** (中文: [docs/zh/EXTENDING.md](docs/zh/EXTENDING.md))
for the `ITagRule` / `ISettingsPanelTab` APIs and worked examples.

The six built-in tags (`Checkpoint`, `NoCheckpoint`, `Jumpless`, `Voiceline`,
`Glitchless`, `NoEC`) are registered through the same path as an external rule,
so a custom rule gets no special treatment and needs none.

## 3. Development setup

Requirements:

- the .NET SDK (`dotnet`);
- *Human: Fall Flat* installed (the build references the game's managed DLLs);
- BepInEx 5.x (HarmonyX) installed in the game.

```bash
git clone https://github.com/TwilightCup/HSRTimer.git
cd HSRTimer
git switch dev
dotnet build src/HSRTimer/HSRTimer.csproj
```

The build resolves game/BepInEx references from `GAME_MANAGED` (the game's
`*_Data/Managed` directory) and `BEPINEX_CORE` (the `BepInEx/core` directory).
Set them either in a gitignored `Directory.Build.user.props` at the repository
root:

```xml
<Project>
  <PropertyGroup>
    <GAME_MANAGED>/path/to/Human Fall Flat/Human_Data/Managed</GAME_MANAGED>
    <BEPINEX_CORE>/path/to/Human Fall Flat/BepInEx/core</BEPINEX_CORE>
  </PropertyGroup>
</Project>
```

or export them for the build:

```bash
GAME_MANAGED="/path/to/Human_Data/Managed" \
BEPINEX_CORE="/path/to/BepInEx/core" \
dotnet build src/HSRTimer/HSRTimer.csproj
```

Output lands in `src/HSRTimer/bin/Debug/netstandard2.0/`: the unversioned
`HSRTimer.dll` (kept for external-plugin compatibility) plus the versioned
`HSRTimer-v0.0.0.dll` you deploy to `BepInEx/plugins/`. The plugin self-seeds
its config and `lang/` files under the BepInEx config dir on first run.

There is no test suite and no linter — **the compiler is the only automated
check**, so make sure `dotnet build` is clean before opening a PR.

### Testing in game

Every feature must be testable from the game's dev console. HSRTimer registers
an `hsr ...` command family (open the console with `~` or `F1`), and output is
mirrored to `BepInEx/LogOutput.log`. **A new feature must also add the matching
`hsr` commands and document them in [docs/TESTS.md](docs/TESTS.md)**, which is
the English command reference and manual test checklist.

### Design rules

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before touching anything under
`src/HSRTimer/Core/`. The key rules:

- **Poll, don't patch.** Almost every signal is a public field on a game class
  (`Game.state`, `App.state`, `NetGame.isLocal`, `Game.currentCheckpointNumber`,
  …); all timing/segment/reset rules are computed from transitions by one
  polling loop. Add a Harmony patch only where no pollable field exists — the
  existing exceptions are the voiceline hooks, the pause-menu restart/checkpoint
  hooks, the timing-boundary tick hooks, and the same-frame Jumpless input
  suppression.
- Keep spec comments: reference the governing R-number when you implement a
  rule, as the surrounding code does.
- The game's own enum is spelled `AppSate` (sic) — don't "fix" it.

## 4. Branch, commits, and changelog

### Branching

- Branch off **`dev`** and open your pull request against **`dev`**.
- Don't target `main`/`master`: those only receive release merges.
- Keep each PR focused; one feature or fix per PR.

### Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/) in English,
e.g. `feat(hud): add draggable panel`, `fix(engine): guard pause accumulation`.
Add `!` for a breaking change (`refactor(subsegment)!: ...`).

### CHANGELOG.md

`CHANGELOG.md` at the repository root is the source for GitHub Release notes,
written **for plugin users, in English**. Include only user-visible changes —
no internal refactors, build details, or implementation notes.

On `dev`, the current entry is the top `## 0.0.0` placeholder with
`Release Date`, `Highlights`, `Details`, and `Contributors` sections. If your PR
changes something a player can observe, add one bullet describing it under that entry's **`Details`** section.

Don't edit, reword, or reorder already-released version entries, and don't fill
in `Highlights` and `Contributors` — that is done by a maintainer while preparing a release. If
your change isn't user-visible, it needs no changelog entry.

## 5. Documentation

- Behavior docs live in `docs/` (English) and `docs/zh/` (Chinese mirror).
  Update both when your change alters documented behavior.
- `REQUIREMENTS.md` is the Chinese specification and the source of truth for
  every behavior; keep it in sync when behavior changes.
- The `README.md` / `README_zh.md` pair is user-facing only — put
  implementation detail in `docs/` instead.

The doc map:

| Doc | Covers |
|---|---|
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Module layout, "poll don't patch", timing/pure-tick clock |
| [CATEGORIES.md](docs/CATEGORIES.md) | Category/tag system |
| [CHECKPOINTS.md](docs/CHECKPOINTS.md) | Checkpoint compliance rules |
| [CONFIG.md](docs/CONFIG.md) | Every `settings.ini` / `tags.ini` / `layout.ini` key |
| [HUD.md](docs/HUD.md) | Timer HUD layout, columns, template variables |
| [PANEL.md](docs/PANEL.md) | In-game settings panel |
| [LOCALIZATION.md](docs/LOCALIZATION.md) | Translation files and format |
| [MARKERS.md](docs/MARKERS.md) | Manual trigger points and marker feed |
| [VOICELINE.md](docs/VOICELINE.md) | Voiceline detection |
| [EXTENDING.md](docs/EXTENDING.md) | Custom tag rules and settings-panel tabs |
| [TESTS.md](docs/TESTS.md) | `hsr` console command reference and test checklist |

## 6. Reporting bugs and requesting features

Open a GitHub issue with enough context to reproduce the problem:

- the HSRTimer version (`hsr about` or the settings panel's About tab);
- how the game is set up: BepInEx version, whether
  [Level Collections](https://github.com/HeyBlack233/LevelCollections) is
  installed, single-player or co-op;
- the enabled tags, the timing standard, and any non-default settings;
- steps to reproduce, expected vs. actual behavior;
- the relevant `BepInEx/LogOutput.log` excerpt — for timing/segment issues,
  include the output of `hsr clock history`.

For a feature request, describe the problem you want solved rather than only the
implementation; if it maps to a new behavior, check whether `REQUIREMENTS.md`
already covers it.

## License

By contributing, you agree that your contributions are licensed under the
project's [MIT License](LICENSE).
