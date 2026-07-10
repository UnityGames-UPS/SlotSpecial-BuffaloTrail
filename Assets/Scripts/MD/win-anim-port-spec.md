# Win-celebration banner — port spec

How the big-win banner is presented in **Age of Gods**, written so the feature can be rebuilt in
another slot game with four win tiers and no background art.

This document is self-contained: you should not need access to the Age of Gods repo to implement
against it. Where it cites source, that is for the curious, not as a dependency.

Age of Gods ships **three** tiers — text-only, Big Win, Super Win. This spec describes **four** —
text-only, Big Win, Huge Win, Mega Win. Super Win maps onto Mega Win; Huge Win is new. The spec also
strips the background art (frame-sequence animations, coin rain) that Age of Gods plays behind the
label, and replaces it with three named attach points so art can be added back later without
re-plumbing the coroutine.

---

## 0. What the feature is

When a spin resolves with a win, a single label — the **banner** — counts the amount up from zero:

1. **It scales in** from nothing, `OutBack`.
2. **It counts up, linearly**, from `0` to the win amount, over a tier-dependent duration.
3. **On the bigger tiers it slides up** partway through the count, at a tier-dependent fraction of the
   count-up, and a tier sound plays. That is the moment tier art would appear behind it.
4. **It holds** at the final amount.
5. **It scales out**, `InBack`.

Which of those steps run, how long the count takes, and when the slide happens are all decided by a
single **tier**, resolved once from `winAmount / totalBet`:

| tier | meaning |
|---|---|
| **None** | win below the floor. Nothing plays at all. |
| **Text** | the label counts up and scales out. It never slides, no sound, no art. |
| **Big Win** | counts up, slides, plays the Big sound. |
| **Huge Win** | same shape, longer count, earlier slide, its own sound. |
| **Mega Win** | same shape, longest count, earliest slide, its own sound. |

The count-up is **linear — no easing.** `displayed = t * winAmount` where `t` is elapsed/duration.
That is the feel. Do not "improve" it with an ease curve; it changes how the number reads.

### The inverted-lifetime rule — read this first

The banner is fired **and not waited on**, except in chained modes.

- On a **manual spin**, `TriggerWinBanner` is called and nobody awaits it. The spin state machine
  finishes immediately. The banner counts up, holds, and scales out on its own timeline, detached. If
  the player spins again before it finishes, the *next spin's reset* kills it.
- In a **chained mode** — any mode where the next spin is issued automatically (auto-spin, free spins,
  a feature chain) — the loop must not advance while the banner is on screen. There, and only there,
  the spin loop awaits the banner via `WaitBannerOrSkip()`, which also implements *press-spin-to-skip*.

| | manual spin | chained mode |
|---|---|---|
| win banner | plays out **detached**; killed by the next spin's reset | **awaited**, skippable |

A target game with no auto-spin and no free spins never enters the right-hand column, and can ignore
`WaitBannerOrSkip` entirely.

---

## 1. Data contract

The banner needs exactly two numbers:

```csharp
internal void TriggerWinBanner(double winAmount, double betAmount);
```

- `winAmount` — the spin's total win.
- `betAmount` — the **staked total bet** for that spin. This is the tier denominator. It is *not* the
  per-line bet. In Age of Gods it is `bets[betCounter] * lines.Count`; whatever your game stakes per
  spin is what belongs here.

Nothing else. No grid, no line data, no symbol ids. Beyond those two doubles the banner needs one TMP
label and the tier table from §2.

Fire it once, at the end of the spin, when `winAmount > 0`.

---

## 2. The tier model

Age of Gods passes tiers around as **parallel bools** (`isBigWin`, `isSuperWin`). Do not port that.
Two bools already admit the illegal state `isBigWin && isSuperWin` — nothing enforces exclusivity
except the order the caller derives them in — and the coroutine has to re-derive precedence three
times over:

```csharp
// The source, for contrast. Every per-tier value is a nested ternary.
float moveThreshold = isSuperWin ? superWinTextMoveThreshold : isBigWin ? bigWinTextMoveThreshold : float.MaxValue;
float duration      = isSuperWin ? superWinLerpDuration      : isBigWin ? bigWinLerpDuration      : normalLerpDuration;
```

At four tiers that becomes three bools, eight states for four legal ones, and a third ternary. Use one
enum and one ordered, serialized table instead. Copy verbatim:

```csharp
// Resolved tier for a single win. `None` => the win is too small to animate at all.
internal enum WinTier { None, Text, Big, Huge, Mega }

[System.Serializable]
internal struct WinTierSpec
{
    [SerializeField] internal WinTier tier;              // identity, for art hooks + debug
    [SerializeField] internal string  label;             // inspector readability, e.g. "Huge Win"
    [SerializeField] internal float   betMultiplier;     // entry threshold: win >= bet * this
    [SerializeField] internal float   lerpDuration;      // count-up length, seconds
    [Range(0.1f, 1f)]
    [SerializeField] internal float   textMoveThreshold; // t at which the label slides. 1f = never.
    [SerializeField] internal string  moveAudioKey;      // SFX at the slide cue. "" = silent.
}
```

```csharp
[Header("Win Banner - Tiers (ascending by betMultiplier)")]
[SerializeField] private WinTierSpec[] winTiers;   // 0 = Text, 1 = Big, 2 = Huge, 3 = Mega

[Header("Win Banner - Timings")]
[SerializeField] private float scaleUpDuration      = 0.8f;
[SerializeField] private float textMoveDuration     = 0.4f;
[SerializeField] private float postLerpHoldDuration = 2.0f;
[SerializeField] private float winTextTargetY       = -131f;   // shared slide target
```

The resolver picks the **highest** tier whose threshold is met. Because the array is ascending,
precedence lives in the data, and is expressed exactly once:

```csharp
private int ResolveTierIndex(double winAmount, double betAmount)
{
    int resolved = -1;                              // -1 => WinTier.None, no banner
    for (int i = 0; i < winTiers.Length; i++)
        if (winAmount >= betAmount * winTiers[i].betMultiplier) resolved = i;
    return resolved;
}
```

The coroutine signature collapses from `(double, bool, bool)` to `(double, WinTierSpec)`, and all
three ternaries above become plain field reads: `spec.lerpDuration`, `spec.textMoveThreshold`,
`spec.moveAudioKey`.

> **The `1f = never` sentinel has a sharp edge.** The source disables the slide for the Text tier with
> `moveThreshold = float.MaxValue`. If you replace that with `textMoveThreshold = 1f` and test
> `t >= spec.textMoveThreshold`, the slide **fires on the final frame**, because `t` is clamped to
> exactly `1`. The cue must therefore be guarded with a strict `spec.textMoveThreshold < 1f`. See §3.

---

## 3. The count-up coroutine

```
WinBannerCoroutine(winAmount, spec):

    decimalPlaces = GetSignificantDecimals(winAmount, minDecimals: 2)
    remember winAmount + decimalPlaces        # so a skip can snap to the final value
    isWinAnimating = true

    scale the label in                        # OutBack, tracked tween

    elapsed = 0
    while elapsed < spec.lerpDuration:
        elapsed += deltaTime
        t = clamp01(elapsed / spec.lerpDuration)

        label.text = FormatSprite(t * winAmount, decimalPlaces)     # LINEAR

        if not moved and spec.textMoveThreshold < 1 and t >= spec.textMoveThreshold:
            moved = true
            play spec.moveAudioKey (if any)
            StartMoveWinTextDown(spec.tier)                          # slide + art cue

        yield null

    label.text = FormatSprite(winAmount, decimalPlaces)              # snap to exact final value
    wait postLerpHoldDuration
    scale the label out                                              # InBack
    StopTierArt()
    isWinAnimating = false
```

The load-bearing lines, verbatim:

```csharp
int decimalPlaces = TextFormatter.GetSignificantDecimals(winAmount, 2);
```

```csharp
_winTweens.Add(Win_Text.transform.DOScale(Vector3.one, scaleUpDuration).SetEase(Ease.OutBack));
```

```csharp
float elapsed = 0f;
while (elapsed < spec.lerpDuration)
{
    elapsed += Time.deltaTime;
    float t = Mathf.Clamp01(elapsed / spec.lerpDuration);

    Win_Text.text = TextFormatter.FormatSprite(t * winAmount, decimalPlaces);

    if (!_winTextMoved && spec.textMoveThreshold < 1f && t >= spec.textMoveThreshold)
    {
        _winTextMoved = true;
        if (!string.IsNullOrEmpty(spec.moveAudioKey)) audioController.Play(spec.moveAudioKey);
        StartMoveWinTextDown(spec.tier);
    }

    yield return null;
}

Win_Text.text = TextFormatter.FormatSprite(winAmount, decimalPlaces);
```

Three things to get right:

**The count-up is linear.** `t * winAmount`, no easing, no `DOTween.To`. The number climbs at a
constant rate and lands on the exact final value at `t == 1`. The scale-in and the slide are eased;
the number is not.

**Snap to the exact value after the loop.** The final loop iteration leaves `t` at `1`, but only if
`elapsed` landed exactly on `lerpDuration` — with a variable frame time it usually overshoots and the
loop exits with the last rendered text one frame stale. The explicit re-format after the loop is not
redundant.

**`minDecimals: 2`.** `GetSignificantDecimals(winAmount, 2)` floors the count-up at two decimals, so
the digit count does not jitter as the number climbs. Compute it **once**, from the *final* amount,
before the loop — never per-frame from the current value, or the label will visibly change width.

### The time base — a deliberate divergence

The banner uses **scaled time** throughout: `Time.deltaTime` in the loop, `WaitForSeconds` for the
hold. The sibling win-*line* system in Age of Gods mandates `WaitForSecondsRealtime` so its cycle
keeps running while a modal popup pauses the game with `Time.timeScale = 0`.

That rule does not transfer. A win amount racing upward behind an open settings dialog looks broken.
Keep the banner on scaled time so it freezes with the game, and keep it internally consistent — do not
mix a realtime hold with a scaled count-up. This is a decision, not an oversight; it is recorded here
so the porter does not "fix" it.

---

## 4. The slide, the scale helpers, and the art attach points

The slide is a **pure tween**. In the source its `OnComplete` also flips a tier flag and starts the
Big-Win art — even when the tier is Super Win (see §8, defect 3). Extract all of that:

```csharp
private void StartMoveWinTextDown(WinTier tier)
{
    RectTransform rt = Win_Text.GetComponent<RectTransform>();
    Tween move = rt.DOAnchorPosY(winTextTargetY, textMoveDuration).SetEase(Ease.Linear)
                   .OnComplete(() => PlayTierArt(tier));
    _winTweens.Add(move);
}
```

Note the tween is **tracked** in `_winTweens`. Every tween the banner creates must be, so both reset
paths can kill it. See §5.

The two scale helpers, verbatim:

```csharp
private void ScaleInObject(RectTransform rt)
{
    if (rt == null) return;
    rt.localScale = Vector3.zero;
    _winTweens.Add(rt.DOScale(Vector3.one, scaleUpDuration).SetEase(Ease.OutBack));
}

private void ScaleOutObject(Transform tr)
{
    if (tr == null || tr.localScale == Vector3.zero) return;
    _winTweens.Add(tr.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack));
}
```

`ScaleOutObject`'s `localScale == Vector3.zero` early-out is what makes teardown **idempotent** — both
reset paths and the coroutine's own tail can all call it, in any order, and only the first does work.
Keep it.

### Where background art reattaches

This port has no background art. Rather than delete the hooks, stub them. These three methods are the
*only* places art belongs — nothing else in the banner should know that art exists:

```csharp
// Tier-art attach points. Empty in this port; the shape is what matters.
private void PlayTierArt(WinTier tier)          { /* primary art: the tier word/banner graphic */ }
private void PlayTierArtSecondary(WinTier tier) { /* second-stage art: a later cue, mid-count */ }
private void StopTierArt()                      { /* tear art down; called from BOTH reset paths */ }
```

- **`PlayTierArt`** fires from the slide's `OnComplete` — the label has reached its raised position and
  the space below it is now clear. In Age of Gods this scales in the Big-Win rects and starts a frame
  sequence.
- **`PlayTierArtSecondary`** has no caller in this port. Age of Gods fires a *second* art cue partway
  through the count (`superWinExtraThreshold = 0.65`, later than the `0.4` slide) so a Super Win escalates
  visually mid-count-up. If you want that, add an `artCue2Threshold` to `WinTierSpec` and gate a second
  `if` in the loop exactly like the slide cue. The hook is preserved so that change is local.
- **`StopTierArt`** must be called from the coroutine's tail *and* from both reset paths, or art
  outlives the label that summoned it.

---

## 5. Lifecycle & skip contract

### State

```csharp
internal bool isWinAnimating;                                 // the one flag every waiter polls
private Coroutine _winCoroutine;
private List<Tween> _winTweens = new List<Tween>();
private bool _winTextMoved;
private float _winTextOriginalY;                              // captured once, in Awake
private double _currentWinAmount;                             // so a skip can snap to the final value
private int _currentDecimalPlaces;
```

`isWinAnimating` is true from tier resolution until the final scale-out begins. It is the **single
source of truth**; nothing else may be polled to decide whether the banner is up.

`_winTextOriginalY` is captured once, at init, before anything moves the label:

```csharp
if (Win_Text != null)
{
    Win_Text.transform.localScale = Vector3.zero;
    _winTextOriginalY = Win_Text.GetComponent<RectTransform>().anchoredPosition.y;
}
```

Capture it in `Awake`, not `Start`, and not lazily on first use — by the time a banner has slid, the
original Y is gone.

### Three operations

**`TriggerWinBanner(win, bet)`** — fire-and-forget.

```csharp
internal void TriggerWinBanner(double winAmount, double betAmount)
{
    int i = ResolveTierIndex(winAmount, betAmount);
    if (i < 0) return;                       // WinTier.None — see the invariant below

    SnapResetWinBanner();
    _winCoroutine = StartCoroutine(WinBannerCoroutine(winAmount, winTiers[i]));
}
```

**`SnapResetWinBanner()`** — instant, no tweens. The pre-arm, so a new banner starts from a clean
slate even if the previous one is mid-flight.

```csharp
private void SnapResetWinBanner()
{
    if (_winCoroutine != null) { StopCoroutine(_winCoroutine); _winCoroutine = null; }
    foreach (var t in _winTweens) t?.Kill();
    _winTweens.Clear();
    isWinAnimating = false;

    if (Win_Text != null)
    {
        Win_Text.transform.localScale = Vector3.zero;
        RectTransform winRT = Win_Text.GetComponent<RectTransform>();
        winRT.anchoredPosition = new Vector2(winRT.anchoredPosition.x, _winTextOriginalY);
    }

    StopTierArt();

    _winTextMoved = false;
    _currentWinAmount = 0;          // fix — see §8, defect 6
    _currentDecimalPlaces = 0;
}
```

**`ResetWinBanner()`** — the graceful skip. Shows the player the full win, then clears.

```csharp
internal void ResetWinBanner()
{
    bool wasLerping = _winCoroutine != null;

    if (_winCoroutine != null) { StopCoroutine(_winCoroutine); _winCoroutine = null; }
    foreach (var t in _winTweens) t?.Kill();
    _winTweens.Clear();

    if (Win_Text != null)                                     // fix — see §8, defect 1
    {
        if (wasLerping && _currentWinAmount > 0)
        {
            Win_Text.text = TextFormatter.FormatSprite(_currentWinAmount, _currentDecimalPlaces);
            Win_Text.transform.localScale = Vector3.one;
        }

        ScaleOutObject(Win_Text.transform);

        RectTransform winRT = Win_Text.GetComponent<RectTransform>();
        _winTweens.Add(                                        // fix — see §8, defect 2
            winRT.DOAnchorPosY(_winTextOriginalY, 0.4f).SetEase(Ease.InBack));
    }

    StopTierArt();

    _winTextMoved = false;
    _currentWinAmount = 0;
    isWinAnimating = false;
}
```

The `wasLerping` gate is what makes a skip feel like a skip and not a cancel: the count-up jumps to the
final amount and the label scales out from there, so the player always sees what they won.

### The invariant the target must uphold

**The banner must be cleared at the start of every spin, and at the top of every chained-loop
iteration.**

`TriggerWinBanner` early-returns on `WinTier.None` *without clearing anything*. So a spin that wins
below the floor does not clean up the previous spin's banner. Age of Gods survives this only because
two other call sites already reset first — one at spin start, one at the top of each free-spin
iteration. Reproduce both. Without them, a banner from a big win survives, frozen, through the next
several small wins.

### Who waits

Manual spins do not wait. Chained modes do:

```csharp
IEnumerator WaitBannerOrSkip()
{
    if (!isWinAnimating) { CheckSkip(); yield break; }   // consume a stale skip click
    if (CheckSkip()) { ResetWinBanner(); yield break; }

    ArmSkipControl();
    while (isWinAnimating)
    {
        if (CheckSkip()) { ResetWinBanner(); break; }
        yield return null;
    }
    DisarmSkipControl();
}
```

`CheckSkip()` consumes a one-shot "player pressed spin/skip" signal and returns whether it fired. The
early `CheckSkip()` on the not-animating path matters: a skip click aimed at an earlier phase must be
*consumed*, or it leaks forward and instantly dismisses the next thing that checks for it.

---

## 6. The payout label (sprite font)

Age of Gods renders win amounts in a custom sprite font, via TMP rich-text sprite tags. Digits `0`–`9`
map to sprite indices 0–9, `.` to 10, `,` to 11. These two static methods are self-contained and can be
copied verbatim:

```csharp
public static int GetSignificantDecimals(double value, int minDecimals = 0)
{
  string raw = value.ToString("F3");
  int dotIndex = raw.IndexOf('.');
  int places = 0;
  if (dotIndex >= 0)
  {
    string decimals = raw.Substring(dotIndex + 1).TrimEnd('0');
    places = decimals.Length;
  }
  return Mathf.Max(minDecimals, places);
}

public static string FormatSprite(double value, int decimalPlaces)
{
  string formatted = value.ToString($"F{decimalPlaces}");
  var sb = new StringBuilder();
  foreach (char c in formatted)
  {
    if (c >= '0' && c <= '9') sb.Append($"<sprite={c - '0'}>");
    else if (c == '.') sb.Append("<sprite=10>");
    else if (c == ',') sb.Append("<sprite=11>");
  }
  return sb.ToString();
}
```

The banner calls them with a **two-decimal floor** — `GetSignificantDecimals(winAmount, 2)` — unlike
the per-symbol payout label, which passes no minimum. See §3 for why.

If the target game's banner is plain TMP text, ignore this section entirely and use
`value.ToString("F2")`. It is orthogonal to the tier logic.

---

## 7. Where it hooks into the spin loop

There is exactly **one** fire point, at the end of the spin, after the balance/HUD update:

```csharp
if (result.payload.winAmount > 0)
  uIManager.TriggerWinBanner(result.payload.winAmount, currentTotalBet);
```

No `yield return`. It is fire-and-forget. If you await it, manual spins stall for the full banner
duration and the game feels broken.

Then, at the very end of the spin, the chained-mode wait:

```csharp
bool chainedMode = isFreeSpin || isAutoSpin || LastSpinWasFeatureTrigger();
if (chainedMode)
  yield return WaitBannerOrSkip();
```

And the two mandatory resets from §5:

```csharp
// at the top of OnSpinStart, before the reels move
uIManager.ResetWinBanner();

// at the top of each chained-loop iteration
if (isFreeSpin) uIManager.ResetWinBanner();
```

If the target has no auto-spin and no free spins, `chainedMode` is always false, `WaitBannerOrSkip` is
dead code, and only the spin-start reset is required.

---

## 8. Defects to fix while porting, not copy

All seven are live in the Age of Gods source. Do not reproduce them.

### 1. `Win_Text` dereferenced outside its own null guard

```csharp
if (wasLerping && Win_Text != null && _currentWinAmount > 0) { /* ... */ }   // guarded

ScaleOutObject(Win_Text.transform);                                          // NOT guarded  <-- NRE

if (Win_Text != null) { /* ... */ }                                          // guarded again
```

`ScaleOutObject` null-checks its *parameter*, but evaluating the argument `Win_Text.transform`
dereferences `Win_Text` before the call is ever made. The helper's guard cannot save it. Move the call
inside the null check, as §5 does.

### 2. The position-restore tween is untracked, and fights the next slide

`ResetWinAnimation` clears `_winTweens`, then creates a `DOAnchorPosY(_winTextOriginalY, 0.4f)` that it
**never adds back**. `SnapResetWinAnimation` kills only *tracked* tweens, then writes
`anchoredPosition` directly — and a direct property write is not something DOTween treats as a
conflict. So the stale 0.4s tween keeps writing `y` every frame. If the next spin's banner is a tier
that slides, two live tweens now fight over the same property.

This is the nastiest of the seven, because it is timing-dependent: it only bites when the next spin
lands inside a 0.4-second window. Fix: track it, as §5 does.

### 3. The slide's `OnComplete` starts the wrong tier's art

```csharp
.OnComplete(() =>
{
  _bigWinActive = true;              // unconditional
  bigWinAnim.StartAnimation();       // Big-Win art, even on a Super Win
  ScaleInObject(bigWinBgRect);
  ScaleInObject(bigWinAnimRect);
});
```

A Super Win crosses its slide threshold (`0.4`) first, so this runs and starts the **Big-Win** art;
then at `0.65` the Super art comes in on top. Both `_bigWinActive` and `_superWinActive` end up true.
The slide is doing three jobs: geometry, art, and flag mutation. Make it a pure tween whose only
side-effect is the `PlayTierArt(tier)` hook, parameterized by the resolved tier.

### 4. Parallel tier bools

`isBigWin` / `isSuperWin` admit illegal states and force precedence to be re-derived inside the
coroutine. Replace with the `WinTier` enum and the ordered table of §2. At four tiers this stops being
a style preference.

### 5. The tier sound is hard-coded

```csharp
audioController.Play("bigwin");     // fires for Big Win *and* Super Win
```

Sitting in the slide cue, it plays `"bigwin"` for every tier that slides. Move it onto the tier:
`spec.moveAudioKey`, empty string for the Text tier.

### 6. `SnapReset` never zeroes `_currentWinAmount`

`ResetWinAnimation` zeroes it; `SnapResetWinAnimation` does not. Latent rather than live — every reader
is gated behind `wasLerping`, which `SnapReset` has just falsified — but it is a loaded gun. Zero it
in both paths.

### 7. Skipping the banner blanks the HUD win label

`ResetWinAnimation` opens with `ResetWinUIText()`, which sets the HUD's *current winning* label to
`0`. That is correct at spin start. It is wrong on a **skip**: the player presses skip, the banner
obediently snaps to the full win amount — and the HUD win readout next to it drops to zero at the same
instant. Two labels, same spin, contradicting each other until the next spin refreshes the HUD.

The banner reset is conflating "clear the banner" with "clear the last spin's HUD". Hoist
`ResetWinUIText()` out of the reset and call it from the spin-start site only.

### And one deliberate divergence

The scaled-vs-realtime time base (§3) is **not** a defect. The banner intentionally pauses with the
game, unlike the win-line cycle. Do not port the line-win realtime rule into the banner.

Huge Win is likewise not a defect. It is the new tier.

---

## 9. Tuning values

Starting points, so you are not guessing at feel. Every value is serialized and tunable.

| # | tier | `betMultiplier` | `lerpDuration` | `textMoveThreshold` | `moveAudioKey` |
|---|---|---|---|---|---|
| — | None | `< 5×` | — | — | — |
| 0 | Text | `5×` | `3.0s` | `1.0` (never) | `""` |
| 1 | Big | `10×` | `3.5s` | `0.6` | `"bigwin"` |
| 2 | Huge | `25×` | `4.0s` | `0.5` | `"hugewin"` |
| 3 | Mega | `50×` | `4.5s` | `0.4` | `"megawin"` |

| value | setting |
|---|---|
| `scaleUpDuration` | `0.8s`, `OutBack` |
| `textMoveDuration` | `0.4s`, `Linear` |
| `postLerpHoldDuration` | `2.0s` |
| `winTextTargetY` | `-131` (anchored Y) |
| scale-out | `0.4s`, `InBack` |

**The threshold ladder is the one genuine invention here.** Age of Gods uses `5 / 10 / 15`, which
leaves no room to insert a fourth tier between Big and Mega — so the ladder is re-spaced
multiplicatively to `5 / 10 / 25 / 50`, keeping the 5× banner floor and giving Huge real headroom.
Each tier is roughly 2–2.5× the one below. Treat these as placeholders until the maths team weighs in;
they must be monotonically ascending, and they must match the hit frequencies your paytable actually
produces, or Mega Win will either never fire or fire on every other spin.

Durations climb with tier — a bigger win deserves a longer count. Move thresholds *fall* with tier — a
bigger win lifts the number earlier, leaving more runway for the art that plays beneath it.

---

## 10. Debug harness

The tiers are otherwise only reachable via real server wins, which makes them near-impossible to iterate
on. Age of Gods gates a key handler behind a serialized bool; generalize it over the table:

```csharp
[Header("Win Banner - Debug")]
[SerializeField] private bool enableDebugKeys = false;
[SerializeField] private float debugBetAmount = 1.0f;

private void Update()
{
    if (!enableDebugKeys) return;

    for (int i = 0; i < winTiers.Length && i < 9; i++)
        if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            TriggerWinBanner(debugBetAmount * (winTiers[i].betMultiplier + 1f), debugBetAmount);

    if (Input.GetKeyDown(KeyCode.Alpha0)) ResetWinBanner();
}
```

Keys `1`–`4` fire Text / Big / Huge / Mega; `0` exercises the graceful skip path. Ship it disabled.

---

## 11. Implementation checklist

- [ ] Tier is a **single resolved enum**, not parallel bools.
- [ ] Tier table is an **ascending** serialized array; the resolver picks the **highest** match.
- [ ] `-1` from the resolver means `None` — return without touching state.
- [ ] Slide cue is guarded with **`textMoveThreshold < 1f`**, so `1f` truly means never.
- [ ] Count-up is **linear**, un-eased: `t * winAmount`.
- [ ] `decimalPlaces` computed **once** from the final amount, with `minDecimals: 2`.
- [ ] Exact final value re-formatted **after** the loop, not left to the last frame.
- [ ] `StartMoveWinTextDown` is a **pure tween**; its only side-effect is `PlayTierArt(tier)`.
- [ ] Art exists only behind `PlayTierArt` / `PlayTierArtSecondary` / `StopTierArt`.
- [ ] `StopTierArt()` called from the coroutine tail **and both** reset paths.
- [ ] **Every** tween is added to `_winTweens`, including the position-restore in `ResetWinBanner`.
- [ ] `Win_Text` dereferenced only inside its null guard, `ScaleOutObject` call included.
- [ ] Both reset paths zero `_currentWinAmount`.
- [ ] `ResetWinUIText()` (HUD win label) is **not** called from the banner reset.
- [ ] Per-tier `moveAudioKey`; Text tier is silent.
- [ ] `_winTextOriginalY` captured in `Awake`, before anything moves the label.
- [ ] Fired **fire-and-forget** from spin end, once, when `winAmount > 0`.
- [ ] Chained modes — and only chained modes — `yield return WaitBannerOrSkip()`.
- [ ] `WaitBannerOrSkip` consumes a stale skip signal on its not-animating path.
- [ ] Reset called at **spin start** *and* at the **top of each chained-loop iteration**.
- [ ] Banner runs on **scaled** time, deliberately; hold and count-up agree.

---

## Appendix: source map

For reference against the Age of Gods implementation. Note that Age of Gods has three tiers, its slide
carries art side-effects, and its reset paths contain the defects of §8 — these citations show what the
port is derived *from*, not what it should look like.

| what | where |
|---|---|
| serialized timings, thresholds, tier art refs | [UIManager.cs:40-77](../Assets/Scripts/Base/UIManager.cs#L40-L77) |
| banner state fields | [UIManager.cs:105-113](../Assets/Scripts/Base/UIManager.cs#L105-L113) |
| `_winTextOriginalY` capture, in `Awake` | [UIManager.cs:138-142](../Assets/Scripts/Base/UIManager.cs#L138-L142) |
| debug keys | [UIManager.cs:177-188](../Assets/Scripts/Base/UIManager.cs#L177-L188) |
| `ResetWinUIText` — the HUD label, defect 7 | [UIManager.cs:214-217](../Assets/Scripts/Base/UIManager.cs#L214-L217) |
| `TriggerWinAnimation` — tier resolution | [UIManager.cs:381-392](../Assets/Scripts/Base/UIManager.cs#L381-L392) |
| `ResetWinAnimation` — graceful skip; defects 1, 2, 7 | [UIManager.cs:394-432](../Assets/Scripts/Base/UIManager.cs#L394-L432) |
| `SnapResetWinAnimation` — instant; defect 6 | [UIManager.cs:434-457](../Assets/Scripts/Base/UIManager.cs#L434-L457) |
| `WinAnimationCoroutine` — the count-up; defects 4, 5 | [UIManager.cs:459-530](../Assets/Scripts/Base/UIManager.cs#L459-L530) |
| `StartMoveWinTextDown` — the slide; defect 3 | [UIManager.cs:558-572](../Assets/Scripts/Base/UIManager.cs#L558-L572) |
| `ScaleInObject`, `ScaleOutObject` | [UIManager.cs:574-585](../Assets/Scripts/Base/UIManager.cs#L574-L585) |
| `FormatSprite`, `GetSignificantDecimals` | [TextFormatter.cs:6-36](../Assets/Scripts/Base/TextFormatter.cs#L6-L36) |
| the fire point, in `OnSpinEnd` | [GameManager.cs:481-485](../Assets/Scripts/Base/GameManager.cs#L481-L485) |
| `WaitWinAnimOrSkip` | [GameManager.cs:500-531](../Assets/Scripts/Base/GameManager.cs#L500-L531) |
| reset call sites — chained loop, spin start | [GameManager.cs:327](../Assets/Scripts/Base/GameManager.cs#L327), [:380](../Assets/Scripts/Base/GameManager.cs#L380) |
