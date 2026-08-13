# Voiceline Detection

> **中文版**: [zh/VOICELINE.md](zh/VOICELINE.md)

The **Voiceline** tag (R3.6) requires the player to trigger every story
voiceline in a level. This page explains how HSRTimer detects compliance — it is
the subtlest tag, because a common skip trick leaves a detectable trace.

## What the game does

A level's voicelines are `NarrativeBlock` components placed in the scene. When
the player enters a block's trigger and lingers past its `triggerDelay`, the
block calls `NarrativeBlock.Play()`, which shows a subtitle and plays a voiceover
clip via `SubtitleManager.PlayNarrative(AudioClip)`. Once played, the block sets
its private `wasPlayed` flag and never replays.

There is also a special narrative audio source, conventionally named **"Easter"**,
that plays a particular story clip.

## How HSRTimer tracks it (`VoicelineTracker`)

1. **On level enter** — scan the scene:
   - `Object.FindObjectsOfType<NarrativeBlock>()` records every block's instance
     id as "pending" (must fire).
   - Search for an `AudioSource` whose GameObject is named **"Easter"**
     (case-insensitive). If one exists, the run starts in a **suspicious** state
     (`satisfied = false`): the Easter clip is present but has not yet played.

2. **During play** — two Harmony postfixes feed the tracker:
   - `NarrativeBlock.Play()` postfix → mark that block "triggered" (remove from
     pending). Idempotent (deduped by instance id).
   - `SubtitleManager.PlayNarrative(AudioClip)` postfix → if the clip is the
     Easter clip, mark Easter as **played** and flip the run back to satisfied.

3. **On level exit** — the run is **invalid** (reason `Voiceline`) if:
   - the Easter source was present but never played, **or**
   - any `NarrativeBlock` was never triggered.

When all voicelines are satisfied, the HUD shows a green "all voicelines
triggered" hint (R3.6.3).

## Why two signals

The common voiceline-skip trick leaves the **Easter AudioSource present in the
scene but never plays it**. Merely checking "did each NarrativeBlock fire" would
miss this, because the skip bypasses the normal trigger path. By treating
"Easter present" as suspicious until we observe the actual `PlayNarrative` call
for that clip, the detection defeats the trick (R5.5).

The `NarrativeBlock.Play` postfix is the primary per-block signal; the
`SubtitleManager.PlayNarrative` postfix is the robust signal for the Easter clip
specifically (it fires for the actual audio playback, which the skip avoids).

## Caveats

- The "Easter" name is a scene/inspector convention (there is no literal in the
  decompiled code). Detection matches case-insensitively by GameObject name; if
  a custom level uses a different name, the Voiceline tag may mis-fire. Verify
  in-game before treating the Voiceline tag as authoritative on custom levels.
- All detection is wrapped so any failure logs a warning instead of crashing
  the game (N7).
- The tracker resets per level; it does not carry state across levels.
