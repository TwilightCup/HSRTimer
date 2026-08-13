# Checkpoint Rules

> **中文版**: [zh/CHECKPOINTS.md](zh/CHECKPOINTS.md)

Applies when the active category carries the **Checkpoint** tag (R3.3 / R4).
The engine performs two checks: **skip detection** (R4.1) and
**final-checkpoint validation** (R4.2). Both raise the corresponding invalid
reason if violated.

## Skip detection (R4.1)

If `Game.currentCheckpointNumber` advances by more than +1 in a single step
(new > old + 1), that is a **checkpoint skip** → invalid — unless the jump is a
known legal span in the exception table below.

### BuiltIn level 9 (Dark / Halloween) — allowed spans

| From | Allowed to |
|:----:|:----------:|
| 6 | 11 |
| 17 | 19, 20, 21, 22, 23 |
| 18 | 21, 22, 23, 24 |
| 19 | 21, 22, 23, 24 |
| 20 | 21, 22, 23, 24 |
| 21 | 24 |
| 22 | 24 |

### EditorPick level 9 — allowed spans

| From | Allowed to |
|:----:|:----------:|
| 7 | 9 |
| 8 | 10 |

## Final-checkpoint validation (R4.2)

On leaving a level, `currentCheckpointNumber` must equal the level's required
final checkpoint.

### BuiltIn required final checkpoints (index = level number)

| # | Level (common name) | Final CP |
|:-:|---------------------|:--------:|
| 0 | Mansion (Intro) | 3 |
| 1 | Train | 4 |
| 2 | Carry | 3 |
| 3 | Mountain (Climb) | 3 |
| 4 | Demolition (Break) | 7 |
| 5 | Castle (Siege) | 12 |
| 6 | Water | 10 |
| 7 | Power Plant | 10 |
| 8 | Aztec | 13 |
| 9 | Dark (Halloween) | 24 |
| 10 | Steam | 11 |
| 11 | Ice | 13 |

### EditorPick

- Level 5: required final checkpoint is **4**.
- All other EditorPick levels: the required final checkpoint is the **maximum
  checkpoint number that exists in that level** (the highest one observed during
  the run).

### Non-linear levels (safety)

Some levels set `Game.currentLevel.nonLinearCheckpoints`, meaning checkpoints
can be reached out of strict numerical order. For such levels the strict
final-checkpoint requirement is **relaxed** (a pass-zone exit counts as valid)
to avoid false positives. This applies automatically.

## Notes

- These tables are transcribed from the spec; the authoritative game-side data
  is `Game.levels` and the in-level checkpoint layout. If a game update changes
  a level, re-verify against the game.
- Workshop levels have no enforced checkpoint rules in v1.
