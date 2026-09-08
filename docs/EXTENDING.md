# Extending HSRTimer

> **中文版**: [zh/EXTENDING.md](zh/EXTENDING.md)

HSRTimer exposes two extension points for other BepInEx plugins:

- **Custom tag rules** (R3.7) — new validity logic that users opt into by
  enabling the tag id in the settings panel (persisted to `tags.ini`).
- **Settings panel tabs** — register your own IMGUI configuration page as an
  extra tab in HSRTimer's settings panel (one tab per plugin). Tabs can
  optionally follow HSRTimer's language selection (R9.2) and save their own
  config on HSRTimer's `SettingsSaved` event (R9.3).

Both are covered below.

## Custom tag rules

The tag system is extensible (R3.7). Any BepInEx plugin can register a
**custom tag rule** — new validity logic that users opt into by enabling the
tag id in the settings panel (persisted to `tags.ini`). The built-in tags
(`Checkpoint`, `NoCheckpoint`, `Jumpless`, `Voiceline`) are themselves rules
registered this way, so your custom rule runs through the exact same engine
path.

### The `ITagRule` interface

```csharp
public interface ITagRule
{
    string Id { get; }                  // stable tag id, e.g. "NoFall"
    string DisplayNameKey { get; }      // localization key (optional)

    void OnLevelEnter(ValidationContext ctx);  // once when a level starts
    void OnTick(ValidationContext ctx);        // every physics frame while playing
    void OnLevelExit(ValidationContext ctx);   // once when a level ends
}
```

`ValidationContext` carries everything a rule needs: the `RunState`, the current
`Game`, the current and previous checkpoint numbers, the `ValidityFlags` (raise
reasons here), and the `LocalizationService`.

The engine only invokes your rule when the **tag id is enabled** (checked in the
settings panel's Category page).

### Minimal example: a "no drowning" tag

```csharp
using HSRTimer;
using UnityEngine;

public class NoFallRule : ITagRule
{
    public string Id => "NoFall";
    public string DisplayNameKey => "TAG_NO_FALL";

    private bool _fell;

    public void OnLevelEnter(ValidationContext ctx) => _fell = false;

    public void OnTick(ValidationContext ctx)
    {
        // (Illustrative) flag if the local player is in a fall state.
        var me = Human.Localplayer;
        if (me != null && (me.state == HumanState.Fall || me.state == HumanState.FreeFall))
            _fell = true;
    }

    public void OnLevelExit(ValidationContext ctx)
    {
        if (_fell)
            ctx.Flags.Raise((InvalidReason)100); // a custom reason, or reuse a built-in
    }
}
```

### Registering the rule

In your plugin's `Awake` (after HSRTimer has loaded — declare a dependency):

```csharp
[BepInDependency("HSRTimer")]
public class MyPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        TagRuleRegistry.Instance.Register(new NoFallRule());
    }
}
```

Duplicate ids are rejected (logged + ignored) to avoid double-penalizing.

### Enabling the tag

Once registered, `NoFall` appears as a checkbox on the settings panel's
**Category** page alongside the built-in tags. Users simply check it. You can
also enable it directly in `tags.ini`:

```ini
[tags]
enabled = NoFall
```

### Custom invalid reasons

The built-in `InvalidReason` enum covers the standard reasons. For a fully
custom reason, you can either reuse a built-in (e.g. `CheatCode`) or model your own
flag separately and surface it via the HUD custom-text mechanism. (A future
revision will expose a generic reason-registry; for v1, reuse built-ins.)

## Registering a settings panel tab

Any BepInEx plugin that builds its configuration UI with Unity IMGUI can add
that UI as a **new tab** in HSRTimer's settings panel (R9). A plugin may
register **at most one** tab — the registry keys tabs by the owning plugin's
BepInEx GUID and rejects (and logs) a second registration for the same plugin.

### The `ISettingsPanelTab` interface

```csharp
public interface ISettingsPanelTab
{
    string Title { get; }   // tab title shown in the settings panel navigation
    void Draw();            // draw IMGUI content inside HSRTimer's settings panel
}
```

### Minimal example

```csharp
using HSRTimer;
using UnityEngine;

public class MyConfigTab : ISettingsPanelTab
{
    private bool _someOption;

    public string Title => "My Plugin";

    public void Draw()
    {
        GUILayout.Label("Options for My Plugin");
        _someOption = GUILayout.Toggle(_someOption, "Enable something");
    }
}
```

### Registering the tab

In your plugin's `Awake` (after HSRTimer has loaded — declare a dependency):

```csharp
[BepInDependency("HSRTimer")]
public class MyPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        SettingsPanelTabRegistry.Instance.Register(this, new MyConfigTab());
    }
}
```

`Draw` runs inside HSRTimer's settings window and scroll view, so you can use
`GUILayout.*` / `GUI.*` exactly like in any other Unity IMGUI panel. The tab
title may return a localized string that updates live with your plugin's own
language settings. A second registration for the same plugin GUID is rejected
and logged. To have HSRTimer's language selector drive the tab instead, see the
next section.

## Localizing an external tab

A tab can opt into HSRTimer's language selection by implementing
`ILocalizableSettingsPanelTab` instead of `ISettingsPanelTab` (R9.2). HSRTimer
then calls `SetLanguage` once when the tab is registered and again whenever the
user changes the language in **General → Language**. The tab's `Title` and
`Draw` are re-read every frame, so the change is visible immediately without
restarting the game or reopening the panel.

```csharp
using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// Optional extension of ISettingsPanelTab for tabs that follow HSRTimer's
    /// language selection.
    /// </summary>
    public interface ILocalizableSettingsPanelTab : ISettingsPanelTab
    {
        // Every BCP 47 code this tab ships. Must contain "en" (English base).
        IEnumerable<string> SupportedLanguages { get; }

        // Called on registration and on every HSRTimer language change.
        // Receives the active HSRTimer code when supported, otherwise "en".
        void SetLanguage(string languageCode);
    }
}
```

### Contract

- **English is mandatory.** `SupportedLanguages` must contain `"en"`. HSRTimer
  logs a warning if it is missing and still uses `"en"` as the fallback for any
  active language the tab does not support, so a tab must ship an English base
  no matter what other languages it provides.
- **Fallback is automatic.** HSRTimer compares its active language with
  `SupportedLanguages`. If the active code is present it is passed through;
  otherwise HSRTimer passes `"en"`. Your tab therefore never receives a language
  it did not declare.
- **Follow the HSRTimer format (R7).** External tab translations use the same
  rules as HSRTimer: one file per language named `<code>.txt` (BCP 47),
  `KEY:Value` lines, `#` comments, `__LANG_NAME__`, UTF-8 without BOM, and the
  lookup order **active language → English → key itself**. Keep the files in
  your own plugin's config directory (for example
  `<BepInEx config dir>/<YourPluginGUID>/lang/`) or embed them as resources in
  your DLL.
- **Resolve every string through your lookup.** `Title` and every label drawn
  by `Draw` must call the same getter so the whole tab switches together.
- **HSRTimer's picker stays authoritative.** Your tab does not add entries to
  HSRTimer's language list; it follows whichever language HSRTimer has selected.

`LanguageFile.Parse(path, out displayName)` and
`LanguageFile.ParseLines(lines, sourceName, out displayName)` are public, so you
can reuse HSRTimer's tolerant parser for your own files instead of writing a
second parser.

### Localizable example

```csharp
using System.Collections.Generic;
using HSRTimer;
using UnityEngine;

public class MyConfigTab : ILocalizableSettingsPanelTab
{
    // In a real plugin, load these from your own lang/*.txt files or embedded
    // resources. English must always be present.
    private static readonly Dictionary<string, Dictionary<string, string>> Texts =
        new Dictionary<string, Dictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string>
            {
                ["TAB_MY_PLUGIN"] = "My Plugin",
                ["OPT_ENABLE"] = "Enable something",
            },
            ["zh-Hans"] = new Dictionary<string, string>
            {
                ["TAB_MY_PLUGIN"] = "我的插件",
                ["OPT_ENABLE"] = "启用某功能",
            },
        };

    private string _lang = "en";
    private bool _someOption;

    public IEnumerable<string> SupportedLanguages => Texts.Keys;

    public void SetLanguage(string languageCode)
    {
        // HSRTimer normally passes a supported code, but keep the fallback
        // here too so the tab is robust if called directly.
        _lang = Texts.ContainsKey(languageCode) ? languageCode : "en";
    }

    public string Title => Get("TAB_MY_PLUGIN");

    public void Draw()
    {
        GUILayout.Label(Get("OPT_ENABLE"));
        _someOption = GUILayout.Toggle(_someOption, Get("OPT_ENABLE"));
    }

    private string Get(string key)
    {
        string value;
        if (Texts[_lang].TryGetValue(key, out value)) return value;
        if (Texts["en"].TryGetValue(key, out value)) return value;
        return key;
    }
}
```

Register the tab exactly as before; `SettingsPanelTabRegistry.Register` accepts
the localizable interface because it extends `ISettingsPanelTab`:

```csharp
[BepInDependency("HSRTimer")]
public class MyPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        SettingsPanelTabRegistry.Instance.Register(this, new MyConfigTab());
    }
}
```

## Saving your tab's configuration

Your tab may have its own config file that is separate from HSRTimer's
`settings.ini`/`tags.ini`/`layout.ini`. To save it at the same moments HSRTimer
saves its own config, subscribe to `SettingsPanelTabRegistry.SettingsSaved`
(R9.3):

```csharp
[BepInDependency("HSRTimer")]
public class MyPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        SettingsPanelTabRegistry.Instance.SettingsSaved += SaveMyConfig;
        SettingsPanelTabRegistry.Instance.Register(this, new MyConfigTab());
    }

    private void OnDestroy()
    {
        // The registry is process-wide; unsubscribe when your plugin unloads.
        SettingsPanelTabRegistry.Instance.SettingsSaved -= SaveMyConfig;
    }

    private void SaveMyConfig()
    {
        // Write your plugin's own config here. This is the same moment
        // HSRTimer writes settings.ini / tags.ini / layout.ini.
    }
}
```

Contract:

- The event fires after HSRTimer's `ConfigService.SaveSettings()` writes its
  config. That covers the settings panel's **Save** and **Close** buttons,
  closing the panel with the settings key, game exit, and HSRTimer's own
  internal auto-saves (for example after a run reset).
- Handlers run one at a time. If your handler throws, HSRTimer logs the
  exception and continues saving/notifying other plugins; a broken handler
  cannot block HSRTimer's own persistence.
- The event is for persistence only. Do not use it to draw UI or to assume a
  particular panel tab is currently visible.
- Unsubscribe in your plugin's `OnDestroy` if your plugin can be unloaded, so a
  stale delegate does not keep your object alive.

See [LOCALIZATION.md](LOCALIZATION.md) for the full file format,
[CATEGORIES.md](CATEGORIES.md) for the built-in tags and
[ARCHITECTURE.md](ARCHITECTURE.md) for the engine lifecycle.
