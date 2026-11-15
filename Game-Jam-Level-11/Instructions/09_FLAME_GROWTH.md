# Flame growth stages — Last Ember

## Progress source and session lifetime

Flame growth is driven by the number of **successful fuel pickups in the current gameplay session**, not by the current FlameMeter reserve.

The existing FuelPickup flow remains authoritative:
1. FuelPickup calls FlameMeter.AddFuel().
2. Only if Current actually increases, FuelPickup calls FlameMeter.RegisterSuccessfulFuelPickup().
3. FuelPickup then raises the existing FuelCollected event. StoryMemoryPickup and other listeners continue to receive that same event.

A touch while the meter is full does not advance growth because no fuel was restored. A respawned pickup can count again after a later successful collection. Fuel drain never reduces the pickup count or the growth stage.

The count resets in FlameMeter.ResetMeter(), which is already called by GameManager during StartGame, Retry and ReturnToMainMenu. No PlayerController movement, Input System binding, protection speed, cover rule, story memory data or lighthouse ending flow is changed.

## Growth thresholds

| Stage | Successful pickups this session | Wind resistance |
|---|---:|---:|
| 1 — Маленькая искра | 0–1 | 0% bonus |
| 2 — Устойчивая искра | 2–3 | 10% less exposed gust damage |
| 3 — Последнее пламя | 4+ | 18% less exposed gust damage |

The thresholds and multipliers are serialized on the existing Player prefab and can be balanced in the Inspector. Stage 3 requires four successful collections; repeated collections after pickup respawn count normally.

Wind bonuses affect only the regular unprotected gust penalty. Passive drain, the existing Space-protected damage value and full cover protection remain unchanged. The Grey Wind can still consume the flame quickly.

## Visual response

- Stage 1 keeps the original small flame, baseline light and existing ambient sparks.
- Stage 2 smoothly increases flame scale, blends in warm gold, slightly widens/brightens the existing Light2D and emits extra orange/gold embers.
- Stage 3 creates a taller, larger orange-gold flame, a wider soft Light2D halo and more frequent/additional warm sparks to distinguish the special ember that can wake the ancient beacon.

The existing Player prefab and its pre-authored ParticleSystem/Light2D are reused. The visual size and color blend use the existing smoothing speed; no new runtime GameObjects or particle systems are created. Particle colors stay in orange, gold, yellow and red.

## Manual Unity verification

1. Start a fresh match: stage must be Small Spark and the pickup counter must be zero.
2. Collect one pickup: stage remains Small Spark.
3. Collect a second pickup after it has restored fuel: stage changes to Steady Spark.
4. Allow fuel to drain: the visual stage and pickup count must not regress.
5. Collect a fourth successful pickup: stage changes to Last Flame and remains there until the session resets.
6. Stand in an exposed gust without Space: compare damage against stage 1; expect 10% less at stage 2 and 18% less at stage 3.
7. Hold Space and enter cover: their existing protection behavior should remain unchanged.
8. Retry or return to the main menu and start again: growth resets to Small Spark.
9. With Story Memory present, verify that FuelCollected still opens the associated memory only after fuel increases.

Unity Editor compilation, prefab import and Play Mode are not executed by the GitHub-only environment used to prepare this change. Run this checklist locally in Unity 6000.0.83f1 before merging.
