# Tags

> **中文版**: [zh/CATEGORIES.md](zh/CATEGORIES.md)

HSRTimer has **no category presets**. The active rule set is simply the set of
**tags** (R3) you have enabled — toggle them on the **Category** page of the
settings panel (default key `Home`). Each tag is a rule that decides which
in-game behaviors invalidate a run.

Tags **stack**: enable any combination, and all their rules apply simultaneously.

## Built-in tags

| Tag id | Intent | Mechanism |
|--------|--------|-----------|
| `Checkpoint` (R3.3) | Pass every checkpoint, in order | Skip detection (R4.1) + final-checkpoint validation (R4.2); HUD shows the current checkpoint |
| `NoCheckpoint` (R3.4) | Trigger **no** checkpoint | Invalid if `currentCheckpointNumber > 0` |
| `Jumpless` (R3.5) | Never jump | Invalid on `Human.Localplayer.jump` false→true — and enforced: while the tag is on, the jump key is physically disabled at the game's input layer (see [ARCHITECTURE.md](ARCHITECTURE.md)) |
| `Voiceline` (R3.6) | Trigger every voiceline | Invalid if any `NarrativeBlock` is missed or the Easter voiceline is skipped (see [VOICELINE.md](VOICELINE.md)) |
| `Glitchless` | Perform no glitches | SSG (half-body): invalid when a hand keeps a phantom grab (`grabObject != null` with no `grabJoint`) after the game's reverse-wall-climb branch returns early. Prop Fly: invalid when jumping while standing on a movable object the player is also holding. Footsie: invalid when touching the Water (River) pass point inside the Footsie Spot range |
| `NoEC` | Perform no extended climbing  | When the player leaves the ground (`onGround` becomes false), the first grab height of that airborne period is the baseline; if already holding something at takeoff, the higher of the two hands is used. Any new grab more than 0.2 m above that baseline while airborne is EC, debounced to at most one trigger per 0.2 s. The `Ec` violation is a **soft flag**: it is shown on its own HUD line with a trigger count (e.g. `EC x3`) in normal text color, flashes red on each new trigger, and is cleared by one-key retry and a full timer reset, but not by pause-menu restart |

With no tags enabled (plain Any%), the run is constrained only by the generic
validity checks (R5.1: cheats, speed change, drift) — the HUD shows a red
banner when a run is flagged.

## Enabling tags

In the settings panel's **Category** page, check the tags you want. Changes
take effect immediately and are persisted to `tags.ini` (see [CONFIG.md](CONFIG.md))
on panel close / game exit. You can also edit `tags.ini` directly:

```ini
[tags]
enabled = Checkpoint, Jumpless
```

## Adding custom tags

Third-party plugins can register their own tag rules via the `ITagRule` API;
they appear in the same Category page alongside the built-ins. See
[EXTENDING.md](EXTENDING.md).
