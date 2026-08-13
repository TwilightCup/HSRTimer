# Contributing to HSRTimer

Thanks for your interest in contributing! There are two main ways to help:

## 1. Translations

The UI is fully localizable. English (`en.txt`) is the shipped base; a
Simplified Chinese example is included. To add or improve a translation, read
**[docs/LOCALIZATION.md](docs/LOCALIZATION.md)** (中文: [docs/zh/LOCALIZATION.md](docs/zh/LOCALIZATION.md))
— it covers file naming (BCP 47), the `key:translation` format, `#` comments,
`\n` line escapes, UTF-8 (no BOM), and the submission steps.

Quick start:
1. Copy `src/HSRTimer/lang/en.txt` to `src/HSRTimer/lang/<your-code>.txt`.
2. Translate the right-hand side of each line. Keep the keys unchanged.
3. Set `__LANG_NAME__:` to the display name of your language.
4. Submit a pull request (or, if no remote yet, open an issue with the file).

## 2. Code — custom tags / rules

HSRTimer's category system is extensible: any BepInEx plugin can register a
custom validity rule (a new "tag"). See **[docs/EXTENDING.md](docs/EXTENDING.md)**
(中文: [docs/zh/EXTENDING.md](docs/zh/EXTENDING.md)) for the `ITagRule` API and
a worked example.

## Development setup

```bash
git clone <this repo>
cd HSRTimer
dotnet build src/HSRTimer/HSRTimer.csproj
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the module layout and the
"poll, don't patch" design rationale.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/) in English,
e.g. `feat(hud): add draggable panel`, `fix(engine): guard pause accumulation`.
