# Extending HSRTimer (custom tags)

> **中文版**: [zh/EXTENDING.md](zh/EXTENDING.md)

HSRTimer's tag system is extensible (R3.7). Any BepInEx plugin can
register a **custom tag rule** — new validity logic that users opt into by
enabling the tag id in the settings panel (persisted to `tags.ini`). The
built-in tags (`Checkpoint`, `NoCheckpoint`, `Jumpless`, `Voiceline`) are
themselves rules registered this way, so your custom rule runs through the
exact same engine path.

## The `ITagRule` interface

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

## Minimal example: a "no drowning" tag

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

## Registering the rule

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

## Enabling the tag

Once registered, `NoFall` appears as a checkbox on the settings panel's
**Category** page alongside the built-in tags. Users simply check it. You can
also enable it directly in `tags.ini`:

```ini
[tags]
enabled = NoFall
```

## Custom invalid reasons

The built-in `InvalidReason` enum covers the standard reasons. For a fully
custom reason, you can either reuse a built-in (e.g. `Drift`) or model your own
flag separately and surface it via the HUD custom-text mechanism. (A future
revision will expose a generic reason-registry; for v1, reuse built-ins.)

See [CATEGORIES.md](CATEGORIES.md) for the built-in tags and
[ARCHITECTURE.md](ARCHITECTURE.md) for the engine lifecycle.
