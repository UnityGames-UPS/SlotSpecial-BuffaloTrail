# Coin Fountain — Port Guide

Ported from **SlotSpecial-Superstars**, where it is the big-win celebration effect.

## What this is

A burst of flipping coins that arc up out of a win panel, drift left/right, and fall
off-screen. It is **uGUI-based**: `RectTransform.anchoredPosition` tweens driven by DOTween,
with each coin's spin being a 29-frame sprite sequence on an `Image`. It is **not** a Unity
Particle System, has no `Animator`/`AnimationClip`, no world-space camera, no sorting layers,
and no audio of its own. Coins are recycled through a simple object pool.

The effect knows nothing about wins or slots. Something external calls `StartFountain()`.

---

## Scope rule for Claude

**Claude may only create or modify C# source files under `Assets/Scripts/`.**

Claude must never create, edit, move, or delete Unity-managed files — these are owned
exclusively by the Unity Editor and hand-editing them corrupts the project:

- `*.prefab`, `*.unity`, `*.asset`, `*.mat`, `*.anim`, `*.controller`
- `*.meta` (never, under any circumstance)
- anything under `Assets/Graphics/`, `Assets/Fonts/`, `Assets/Audio/`
- `ProjectSettings/`, `Packages/`, `UserSettings/`, `Library/`

If a step below requires one of those, **stop and tell the human to do it in the Editor.**
Every asset-import and scene-wiring step in this guide is a human step.

---

## File manifest

### Scripts — always required (Claude may write these)

| File | Role |
|---|---|
| `Assets/Scripts/UI/CoinFountainPool.cs` | Wave spawning + per-coin randomization. `: GenericObjectPool<CoinFountainItem>` |
| `Assets/Scripts/UI/CoinFountainItem.cs` | One coin's parabolic flight + fade |

### Scripts — conditional (check before copying)

| File | Role |
|---|---|
| `Assets/Scripts/UI/GenericObjectPool.cs` | Generic MonoBehaviour pool |
| `Assets/Scripts/Base/ImageAnimation.cs` | Sprite-sequence animator for `Image` |

Both are generic, template-derived classes. **If this project already has them, do not
overwrite.** Diff instead, and confirm:

- `ImageAnimation` exposes `StartAnimation()` and `StopAnimation()` — the only two members
  the fountain touches.
- `GenericObjectPool<T>` exposes `GetFromPool()`, `ReturnToPool(T)`, `ReturnAllItemsToPool()`,
  and a `protected List<T> ItemsInUse`.

If either has diverged, adapt `CoinFountainPool`/`CoinFountainItem` to the local API rather
than clobbering a class other systems depend on.

### Assets — the human moves these via the Unity Editor

- `Assets/Prefabs/CoinPrefab.prefab` — source guid `86592076f6b8e49c2820369dd513aa40`
- `Assets/Graphics/Animations/CoinAnim/coin_animation0001.png` … `coin_animation0029.png`
  — **29** sprites, plus their `.meta` files
- **DOTween** — in the source project it lives at `Assets/Plugins/Demigiant/DOTween/` as a
  vendored DLL, *not* a UPM package. It will **not** come across via `manifest.json`.

---

## Import steps

1. **Install DOTween first** in the target project and run its setup utility
   (`Tools → Demigiant → DOTween Utility Panel → Setup DOTween…`). Do this before importing
   anything else, or the scripts won't compile and Unity may drop the prefab's script
   references.

2. **Transfer via package export, not manual file copy.** In the *source* project, right-click
   `CoinPrefab.prefab` → **Export Package…** with *Include dependencies* checked. This pulls
   the prefab, all 29 sprites, and the four scripts with their GUIDs intact, so nothing needs
   re-wiring on import.

   If the target already has `ImageAnimation`/`GenericObjectPool`, **uncheck those two** in the
   export dialog so you don't overwrite the local copies.

3. **Import** the package into the target project.

4. **Verify compilation** — the four scripts should resolve `DG.Tweening` and `UnityEngine.UI`
   with no errors.

---

## Prefab shape

If the export path worked, the prefab arrives wired and you can skip this. Reference for
rebuilding by hand:

```
CoinPrefab              RectTransform 300×300, anchors (0.5, 0.5)
├─ CanvasGroup
├─ CoinFountainItem     canvasGroup → self
│                       rect        → self
│                       flipAnim    → child CoinAnim
└─ CoinAnim             RectTransform 300×300
   ├─ Image
   └─ ImageAnimation    textureArray     = the 29 coin sprites, in order
                        AnimationSpeed   = 50
                        doLoopAnimation  = ✔
                        useSharedMaterial= ✔
                        StartOnAwake     = ✘   ← must stay off; the pool starts it
```

The parent/child split is deliberate: the parent owns the arc and a random spawn rotation,
the child owns the flip animation. Merging them makes the random rotation fight the flip.

---

## Scene setup

Put `CoinFountainPool` on any GameObject under your Canvas, then set:

| Field | Value | Note |
|---|---|---|
| `InitialCount` | 20 | pool pre-warm count |
| `PrefabToPool` | `CoinPrefab` | |
| `ParentTransform` | your win panel | coins are parented here |
| `spawnOrigin` | win panel background | coins launch from its `anchoredPosition` |
| `coinsPerBatch` | 2 | coins per wave |
| `batchInterval` | 0.2 | seconds between waves |
| `coinFadeInDuration` | 0.4 | |
| `fallToY` | -800 | anchored Y coins fall to; put it off-screen |
| `riseRange` | (400, 700) | random rise height per coin |
| `driftXRange` | (400, 1000) | magnitude; L/R sign randomized per coin |
| `upDurationRange` | (0.45, 0.7) | |
| `downDurationRange` | (0.5, 0.8) | |
| `upEase` | `OutQuad` | |
| `downEase` | `InQuad` | |

In the source project `ParentTransform` was `WinPanel` and `spawnOrigin` was `WinPanelBG`.
They may be the same object; `spawnOrigin` only needs to be wherever you want coins to erupt
from.

---

## Trigger API

The fountain has no knowledge of wins. Wire it to whatever your trigger is:

```csharp
[SerializeField] private CoinFountainPool coinPool;

coinPool.StartFountain();        // begin continuous waves; no-op if already running
coinPool.StopFountain();         // stop new waves, let in-flight coins finish their arc
coinPool.FadeOutAllActive(0.4f); // fade active coins out, but keep spawning + flipping
coinPool.ClearAll();             // stop + reclaim everything (use for hard resets too)
```

A typical celebration is `StartFountain()` when the panel appears, then
`FadeOutAllActive(d)` as the panel leaves, then `ClearAll()` once the fade completes.

---

## Gotchas

1. **`internal` accessibility.** `StartFountain`, `StopFountain`, `FadeOutAllActive`, and
   `ClearAll` are `internal` — as are `CoinFountainItem.Launch/FadeOut/ResetState`. That's
   invisible in a default Assembly-CSharp project. **If this project uses asmdefs** and your
   trigger code lives in a different assembly, these won't be callable. Promote them to
   `public`, or add `[assembly: InternalsVisibleTo(...)]`.

2. **Canvas-only.** Everything is `anchoredPosition` + `CanvasGroup`. There is no particle
   system, no `Animator`, no sorting layer, no world-space camera, and no audio on the coin.
   Celebration SFX in the source project was played by the caller, not the fountain.

3. **DOTween is required and vendored.** `DG.Tweening` supplies `Sequence`, `DOAnchorPosY/X`,
   `DOFade`, and `Ease`. There is no fallback path — the scripts do not compile without it.

4. **`GenericObjectPool.Start()` is `internal virtual` and calls `InitializePool`.** If a
   subclass defines its own `Start()` without calling `base.Start()`, the pool silently never
   pre-warms and every coin is instantiated on demand. `CoinFountainPool` deliberately does
   not override `Start`.

5. **Return-to-pool is guarded.** `Launch`'s `onComplete` re-checks `ItemsInUse.Contains(coin)`
   before returning the coin, because `ClearAll()` may have already reclaimed it mid-arc.
   `ResetState()` kills the tweens so the callback can't double-fire. **Preserve both halves
   of this** if you refactor the pool — dropping either produces the
   `[GenericObjectPool] Trying to return an item that is not in use!` error.

6. **`ImageAnimation.OnDisable()` calls `StopAnimation()`.** This is what makes pooling coins
   safe — a returned coin stops its `Invoke` chain. Don't "optimize" the disable away.

---

## Tuning for a different canvas

`fallToY`, `riseRange`, and `driftXRange` are in **canvas anchored units** and were tuned for
the source project's reference resolution. They are the first things to retune, and wrong
values read as broken rather than subtly off:

- **Coins vanish instantly** → `fallToY` isn't far enough below the visible area. Push it more
  negative.
- **Coins barely leave the panel** → `riseRange` is too small for your canvas height.
- **Coins fly off the sides** → `driftXRange` is too large for your canvas width.
- **Too sparse / too dense** → adjust `coinsPerBatch` and `batchInterval` together. Raise
  `InitialCount` to roughly `coinsPerBatch × (avg flight time ÷ batchInterval)` so the pool
  stops instantiating mid-celebration.

---

## Verifying the port

1. **Compiles** — four scripts, no errors.
2. **Pool pre-warms** — enter Play mode; `InitialCount` inactive `CoinPrefab` instances appear
   under `ParentTransform` in the hierarchy.
3. **Fountain runs** — call `StartFountain()` from a temporary debug key. Coins fade in at
   `spawnOrigin`, spin, arc up with random left/right drift, and fall past `fallToY`.
4. **Coins recycle** — watch `ItemsInUse` in the inspector. It should oscillate, not grow
   without bound. Unbounded growth means `onComplete` isn't firing.
5. **Clean teardown** — `FadeOutAllActive(0.4f)` then `ClearAll()`. All coins vanish,
   `ItemsInUse` empties, and no coins keep drifting after the panel is gone.
6. **Re-entrancy** — `StartFountain()` → `ClearAll()` → `StartFountain()`, twice in a row, must
   not log the `Trying to return an item that is not in use!` error.
