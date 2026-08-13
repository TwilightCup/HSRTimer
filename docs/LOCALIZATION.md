# Localization

> **中文版**: [zh/LOCALIZATION.md](zh/LOCALIZATION.md)

HSRTimer's UI is fully localizable. English (`en.txt`) is the shipped base; a
Simplified Chinese example (`zh-Hans.txt`) is included. This page is also the
**contributor guide** for translators (R7.4).

## File location & naming

All translation files live in the plugin runtime directory:

```
<BepInEx config dir>/HSRTimer/lang/<code>.txt
```

`<code>` is a **BCP 47 / IETF language tag**: `en`, `zh-Hans`, `ja`, `pt-BR`,
etc. `en.txt` is the English base. The English base is the only authoritative
key set — translators copy it and translate the values.

## File format

One translation per line, `key:value` (English colon):

```
# ===== Timer panel labels =====
TIMER_GAME_TIME:Game Time
TIMER_SEGMENT_TIME:Segment Time
```

Rules (R7.3.3):

- `#` at the start of a line → comment (ignored). Use these to group/annotate.
- Blank lines are ignored.
- Trim leading/trailing whitespace.
- Everything after the **first** `:` is the translation (values may contain `:`
  or spaces; keys may not).
- A translation must fit on one line. For a literal newline inside a value, use
  the escape `\n` (also `\t`, `\\`, `\:`).
- **Encoding**: UTF-8, no BOM. Both `\n` and `\r\n` line endings are accepted.
- Key naming: `UPPER_UNDERSCORE`, grouped by prefix (`TIMER_*`, `SETTINGS_*`,
  `CATEGORY_*`, `TAG_*`, …).

A special key `__LANG_NAME__` sets the display name shown in the language
picker, e.g. `__LANG_NAME__:简体中文`.

## Robust parsing (R7.5.2)

Lines are parsed one at a time. A malformed line (missing colon, bad key
characters, etc.) is skipped with a warning naming the file and line number;
the rest of the file still loads.

## Fallback chain (R7.5.3)

When looking up a key, the order is:

1. The **current** language's translation.
2. The **English** base translation.
3. The **key itself**.

So an incomplete translation never breaks the UI — missing keys fall back to
English, and an entirely missing English value falls back to the key name.

## Switching language

- Press the **Reload Language** key (default `F10`) to re-read all files
  (hot-reload, R7.5.4) — useful while translating.
- Set `language = <code>` in `settings.ini`; restored on next launch (R7.2.2).
- The language picker lists each language by its **display name**, not its code.

Switching to a non-existent language is safe: the current language is kept and a
warning is logged (R7.2.4).

## How to contribute a translation

1. Copy `src/HSRTimer/lang/en.txt` to `src/HSRTimer/lang/<your-code>.txt`
   (and into `<config>/HSRTimer/lang/` to test in-game).
2. Translate the right-hand side of every line. **Do not change the keys.**
3. Set `__LANG_NAME__:` to your language's display name.
4. Keep section comments if you like (they help maintainers).
5. Submit a pull request (or open an issue with the file if no remote).

See [../CONTRIBUTING.md](../CONTRIBUTING.md).
