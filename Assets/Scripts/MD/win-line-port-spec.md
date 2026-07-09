# Win-line representation — port spec

How win lines are presented in **Age of Gods**, written so the feature can be rebuilt in another
slot game that has its own symbol classes.

This document is self-contained: you should not need access to the Age of Gods repo to implement
against it. Where it cites source, that is for the curious, not as a dependency.

---

## 0. What the feature is

When a spin resolves with one or more winning lines:

1. **One synchronized pass.** Every winning symbol on the grid — the union of all lines — lights up
   at the same time, once. Symbols that sit on more than one line light up exactly once, not once
   per line.
2. **Then a cycle.** The lines replay one at a time, indefinitely, until the player spins again.
   Exactly one line is lit at any moment.
3. **The payout label.** Each line's win amount is shown on that line's **last symbol**, and nowhere
   else.

If there is exactly **one** winning line, step 2 has nothing to cycle between, so that line's
animation simply loops in place: its symbols stay lit and re-pulse on an interval, with the payout
label visible throughout.

If the spin is part of an auto-spin, free-spin, or feature-triggered chain, only step 1 runs — then
the lines are cleared and control returns immediately, so the chain can advance.

---

## 1. Data contract

Per winning line, the animator needs exactly two things:

- an **ordered** list of `(column, row)` grid positions, ordered along the line (left to right)
- a **payout** value (`double`)

Nothing else. Symbol id, line index, match count and so on are irrelevant to presentation.

**The last symbol of a line is `positions[positions.Count - 1]`.** There is no separate field for
it, and no need to compute anything — the server sends positions in line order.

### The `[row, col]` trap

In Age of Gods the wire model is:

```csharp
public class LineWin
{
  public List<Position> positions;   // ordered along the line
  public double payout;
  // ...lineIndex, pattern, symbolId, symbolName, matchCount — unused by presentation
}

public class Position
{
  public List<int> position { get; set; }   // position[0] = ROW, position[1] = COL
}
```

`position` is `[row, col]`, but the grid is indexed **column-first**:

```csharp
int col = lineWin.positions[i].position[1];
int row = lineWin.positions[i].position[0];
var symbol = slotMatrix[col].slotImages[row];
```

Every single loop in the source repeats this swap. It is the most common place to introduce a bug.
If your target game's line model already gives you `(col, row)` directly, delete this concern; if it
gives you a flat index, normalize once at the boundary rather than swapping at each use site.

---

## 2. The per-symbol contract

The animator is deliberately ignorant of what a symbol looks like or how it animates. It requires
three members on whatever your symbol class is:

```csharp
IEnumerator PlayWinIteration(bool showWinText, double payout);
void ResetLineAnim();
void Reset();
```

### `PlayWinIteration(showWinText, payout)`

Plays **one cycle** of the symbol's win highlight and returns when that cycle finishes.

Responsibilities:

- Bring the symbol visually **above the dim layer**. In Age of Gods this is a reparent to a shared
  overlay `RectTransform` (`Lift`), remembering the original parent and sibling index so it can be
  restored later (`Drop`). If your renderer uses sorting orders or a canvas per symbol, do the
  equivalent — the point is that the winning symbol must not be dimmed by the overlay that dims
  everything else.
- **Undim** this symbol.
- If `showWinText`, reveal the payout label on this symbol with `payout` rendered into it.
  Otherwise leave the label hidden. (Do not hide it here if it is already shown — see §3, the
  single-line loop re-calls this every iteration and the label must not flicker.)
- Play the highlight: scale pulse, sprite sequence, `Animator` state, whatever the game has.
- Return when the highlight has completed one cycle.

**Hard requirement: there must be no statement after the final `yield`.** The coroutine's last
action is its wait. This is not stylistic — see §6.

#### Satisfying "return when the cycle ends"

This is left open. Three valid strategies:

**(a) Poll every sub-animation** — what Age of Gods does. It runs a scale pulse, a background sprite
sequence, and (for special symbols) a border and an overlay sequence, all concurrently, and waits
for all of them:

```csharp
yield return new WaitUntil(() =>
{
  bool pulseDone   = pulseSeq == null || !pulseSeq.IsActive() || !pulseSeq.IsPlaying();
  bool bgDone      = isGold || !bgImage.isplaying;
  bool specialDone = !IsSpecialSymbol || specialAnim == null || !specialAnim.isplaying;
  bool borderDone  = !IsSpecialSymbol || !borderAnimation.isplaying;
  return pulseDone && bgDone && specialDone && borderDone;
});
```

**(b) Fixed duration** — `yield return new WaitForSecondsRealtime(iterationDuration);`. Perfectly
legitimate when every symbol shares a highlight length. Simpler, and it makes the cycle timing
predictable. Prefer this unless symbols genuinely differ.

**(c) Await the animation system directly** — e.g. `yield return tween.WaitForCompletion()`, or poll
an `Animator` normalized time.

Whichever you pick, note that (a) has a subtlety: a `WaitUntil` that polls "is it still playing"
will return **immediately** if the animation has not started yet on the frame it is first evaluated.
Age of Gods avoids this because `StartAnimation()` sets `isplaying = true` synchronously before the
wait. If yours sets it on the next frame, insert a `yield return null` first.

### `ResetLineAnim()`

The **"off" half of the cycle.** Called between lines, while the win presentation is still running.

Restore the symbol's parent/sibling index, kill the pulse tween, restore scale, **re-dim the symbol**,
stop the highlight animations and reset them to their first frame, and hide the payout label.

The re-dim is the important part and is what distinguishes this from `Reset()`: the symbol returns to
the dimmed background state so the *next* line stands out against it.

### `Reset()`

**Full teardown**, called on every symbol at the start of the next spin.

Everything `ResetLineAnim` does, except it **clears the dim instead of applying it**, and it also
clears any feature state (gold overlays, etc.) and restores default sizes and colors.

Critically, the dim must be cleared **instantly**, not faded:

```csharp
// Instant dark clear: the dim fade is a 0.5s DOFade, which would otherwise
// overlap the next reel-spin tween and visually black out the reels.
Dark.DOKill();
var dc = Dark.color;
Dark.color = new Color(dc.r, dc.g, dc.b, 0f);
```

This is a real bug that was fixed, not a hypothetical. The dim uses an animated fade everywhere else;
if the reset also fades, the fade is still in flight when the reels start spinning and the whole grid
goes dark for half a second. Carry the fix over.

---

## 3. Phase structure

### Entry point

```
AnimateLineWins(lineWins):                       # a coroutine

    dim every symbol on the grid                 # ToggleDarkFG(true)
    singleLine = (lineWins.Count == 1)

    yield PlaySyncedPass(lineWins, showPayouts: singleLine)     # AWAITED

    if autoContinued:                            # auto / free / feature-triggered spin
        for each line: ResetLine(line)
        return                                   # no loop; the chain must advance

    if singleLine:
        loopHandle = StartCoroutine(SingleLineLoop(lineWins[0]))   # DETACHED
    else:
        for each line: ResetLine(line)           # clear the synced pass first
        loopHandle = StartCoroutine(PerLineLoop(lineWins))         # DETACHED
```

`autoContinued` is `isAutoSpin || isFreeSpin || lastSpinWasFeatureTrigger()`. See §4.

`ResetLine(line)` just calls `ResetLineAnim()` on each of the line's symbols.

### `PlaySyncedPass(lineWins, showPayouts)`

```
lastPerLine = {}                                 # (col,row) -> payout
if showPayouts:
    for each line in lineWins:
        if line.positions is empty: continue
        last = line.positions[^1]
        lastPerLine[(last.col, last.row)] = line.payout

seen = {}                                        # HashSet<(int,int)>
running = []
for each line in lineWins:
    for each pos in line.positions:
        if pos out of grid bounds: continue
        if not seen.Add((pos.col, pos.row)): continue     # dedupe
        show = lastPerLine.TryGetValue((pos.col, pos.row), out payout)
        running.Add(StartCoroutine(symbolAt(pos).PlayWinIteration(show, show ? payout : 0)))

for each co in running:                          # await them ALL, after starting them ALL
    yield co
```

Three things to get right:

**Dedupe.** A symbol on three lines animates once. Without the `seen` set you start three concurrent
coroutines on the same symbol, and the scale pulse of one kills the pulse of the next
(`iconAnim?.Kill()` at the top of `PlayWinIteration`), leaving the symbol at whatever scale it was
mid-tween.

**Collect, then await.** Start every coroutine into a list, *then* loop over the list waiting. If you
`yield return StartCoroutine(...)` inside the position loop, the pass silently becomes **sequential**
— symbols light up one after another instead of together — and it will look almost right, which is
worse than looking obviously wrong.

**Payouts only when `showPayouts`.** With several winning lines the labels would overlap and be
unreadable, so during a multi-line synced pass no labels are shown at all. They appear once the
per-line cycle starts, one at a time.

### `SingleLineLoop(line)` — the one-line case

```
while true:
    wait betweenLineDelay
    running = []
    play "blink" once
    for i, pos in line.positions:
        if pos out of bounds: continue
        show = (i == line.positions.Count - 1)
        running.Add(StartCoroutine(symbolAt(pos).PlayWinIteration(show, show ? line.payout : 0)))
    for each co in running: yield co
```

Note what it **does not** do: it never calls `ResetLineAnim`. The symbols stay lifted, undimmed, and
the payout label stays visible. Each iteration just re-triggers the highlight on symbols that are
already lit. That is the "loop its animation, turn it on and off" behaviour — the on/off is the
highlight cycle itself, separated by `betweenLineDelay`, not a dim/undim.

Because the label is already visible from the synced pass (which ran with `showPayouts: true`), and
`PlayWinIteration` re-asserts it every iteration, it never flickers.

### `PerLineLoop(lines)` — the multi-line case

```
while true:
    for each line in lines:
        running = []
        play "blink" once
        for i, pos in line.positions:
            if pos out of bounds: continue
            show = (i == line.positions.Count - 1)
            running.Add(StartCoroutine(symbolAt(pos).PlayWinIteration(show, show ? line.payout : 0)))
        for each co in running: yield co

        wait betweenLineDelay
        ResetLine(line)              # re-dim this line before the next one lights up
```

The lines were already reset once before the loop started (see the entry point), so the first
iteration begins from a fully dimmed grid. Each subsequent line is preceded by the previous line's
reset. The invariant holds: exactly one line lit at a time.

### Timing

All waits use `WaitForSecondsRealtime`, not `WaitForSeconds`, so the cycle keeps running when the
game pauses via `Time.timeScale = 0` (settings popup, paytable, etc.).

---

## 4. Where it hooks into the spin loop

The call site, at the end of the spin:

```csharp
if (result.payload.lineWins.Count > 0)
{
  audioController.Play("win");
  yield return slotManager.AnimateLineWins(result.payload.lineWins);
}

bool autoContinued = isFreeSpin || isAutoSpin || LastSpinWasWildTrigger() || LastSpinWasFreeSpinTrigger();
if (autoContinued)
  yield return WaitWinAnimOrSkip();
```

### The awaited call does not wait for the loop

`yield return slotManager.AnimateLineWins(...)` reads like it blocks until the win presentation is
finished. **It does not.** It blocks only until the synchronized pass completes; the cycle is started
with a bare `StartCoroutine` and is therefore *detached* from the caller. `AnimateLineWins` returns,
`OnSpinEnd` returns, the spin state machine finishes, and the loop keeps cycling in the background
until the player spins again.

This is the crux of the design. An awaited `while(true)` would deadlock the spin. If you port this
and find your spin never completes, this is why.

### Auto-spin, free-spin, and feature chains

When the spin is going to be followed automatically by another spin, the infinite loop must not start
at all — the chain would never advance. `autoContinued` short-circuits `AnimateLineWins` after one
synced pass, resets the lines, and returns.

In those modes the thing that governs how long the win stays on screen is the **big-win banner**, a
separate system, awaited via `WaitWinAnimOrSkip()` — which also implements "press spin to skip".
The line cycle has no skip of its own; it doesn't need one, because it only ever runs on manual spins
where the next spin is what ends it.

So the two win presentations have inverted lifetimes, and it is worth being explicit about it:

| | manual spin | auto / free / feature chain |
|---|---|---|
| line cycle | loops forever, killed by next spin | one pass, then cleared |
| big-win banner | plays out, no wait | awaited, skippable |

---

## 5. Teardown

The loop is infinite, so something must kill it. That something is the **start of the next spin** —
not the end of the current one. The win stays on screen for as long as the player looks at it.

```csharp
internal IEnumerator StartSpin()
{
  StopWinLoop();        // StopCoroutine(loopHandle); loopHandle = null;
  KillAllTweens();      // reel tweens
  ResetAllIcons();      // Reset() on every symbol in the grid
  ResetWildMatrix();    // ...and any overlay layers
  StopIconAnimation();

  // ...only now start the reels
}
```

```csharp
void StopWinLoop()
{
  if (WinLoopCorutine != null)
  {
    StopCoroutine(WinLoopCorutine);
    WinLoopCorutine = null;
  }
}
```

**The order is load-bearing.** `StopWinLoop()` must come before `ResetAllIcons()`. If you reset first,
the still-running loop can spawn one more iteration on the frame between the two calls and re-light
symbols that you just cleared.

**Do not use `StopAllCoroutines()`.** There is none anywhere in the Age of Gods codebase, and adding
one here would kill the spin state machine along with the win loop. Cleanup is: one tracked
`Coroutine` handle for the loop, plus an explicit per-symbol `Reset()` sweep. Track exactly one
handle — a single field, nulled on stop — and there is nothing to leak.

---

## 6. The subtle part: child coroutines outlive the loop

Read this section before you write the loop.

`StartCoroutine(symbol.PlayWinIteration(...))` is called **on the controller**, not on the symbol. The
controller therefore owns those coroutine objects. `StopCoroutine(loopHandle)` stops the loop and
**only** the loop. Every per-symbol iteration coroutine the loop already spawned keeps running to
completion, on a controller that now believes the win presentation is over.

Age of Gods survives this for exactly one reason: `PlayWinIteration` ends on its wait. There is no
statement after the final `yield`. A stranded child resumes, evaluates its condition, finds the pulse
tween has been killed by `Reset()`, falls out of the `WaitUntil`, and terminates without touching a
thing.

If your symbol's iteration does *anything* after its wait — restores a scale, hides a label, re-dims,
drops the parent — that work executes **after** `ResetAllIcons()` has run. The symbol ends up in a
win-state, lifted above the overlay, sitting on top of a freshly spinning reel. This failure is
intermittent (it depends on which frame the spin button was pressed relative to the iteration) and
therefore miserable to debug.

Two ways to be safe:

- **Keep the post-wait section empty.** What Age of Gods does. All cleanup lives in `ResetLineAnim()`
  and `Reset()`, which the *controller* calls, at times the controller chooses. Recommended.
- **Track the children too** — collect every iteration coroutine in a list and stop them in
  `StopWinLoop()` alongside the parent. This works, but it is one more list to remember to clear, and
  forgetting it on a single code path reintroduces the bug silently.

Prefer the first. It makes the invariant structural rather than something you have to maintain.

---

## 7. Two defects to fix while porting, not copy

Both are live in the Age of Gods source. Do not reproduce them.

### `StopAnimateLineWin` iterates the wrong count

```csharp
void StopAnimateLineWin(LineWin lineWins)
{
  int count = Mathf.Min(lineWins.positions.Count, lineWins.pattern.Count);   // <-- wrong
  // ...
}
```

Every play loop iterates `positions.Count`. The reset iterates `Mathf.Min(positions.Count,
pattern.Count)`. If `pattern` is ever shorter than `positions`, the tail symbols of a line are lit but
never reset, and they stay lit underneath the next line in the cycle. Iterate `positions.Count` in
both. `pattern` has no business being consulted here at all.

### The blink sound plays once per symbol

In both loops, `audioController.Play("blink")` sits **inside** the position loop, so a five-symbol
line fires it five times on the same frame. Hoist it out: once per line, before the position loop.
(The pseudocode in §3 already shows it hoisted.)

---

## 8. The payout label

Age of Gods renders win amounts in a custom sprite font, via TMP rich-text sprite tags. Digits `0`–`9`
map to sprite indices 0–9, `.` to 10, `,` to 11.

These two static methods are self-contained and can be copied verbatim:

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

Used together, so `12.50` renders as `12.5` and `3.00` as `3`:

```csharp
WinAmountText.text = TextFormatter.FormatSprite(winAmount, TextFormatter.GetSignificantDecimals(winAmount));
```

If the target game's labels are plain TMP text, ignore this section entirely — it is orthogonal to the
line logic.

---

## 9. Tuning values

Known-good starting points, so you are not guessing at feel:

| value | setting |
|---|---|
| `betweenLineDelay` | `0.25s` |
| win pulse peak scale | `1.05` |
| win pulse duration | `0.7s` (split into `0.35s` up `OutSine`, `0.35s` down `InSine`) |
| dim alpha | `215/255` |
| dim fade duration | `0.5s` (but **instant** on `Reset()` — see §2) |

---

## 10. Implementation checklist

- [ ] Line model exposes ordered `(col, row)` positions + `payout`. Normalize any `[row, col]` swap once.
- [ ] Symbol class implements `PlayWinIteration`, `ResetLineAnim`, `Reset`.
- [ ] `PlayWinIteration` has **no statement after its final yield**.
- [ ] `Reset()` clears the dim **instantly**, not with a fade.
- [ ] Synced pass dedupes positions with a `HashSet`.
- [ ] Synced pass starts all coroutines, *then* awaits them.
- [ ] Payouts shown in the synced pass only when `lineWins.Count == 1`.
- [ ] `singleLine` branch loops without ever calling `ResetLineAnim`.
- [ ] Multi-line branch resets each line after its turn.
- [ ] Loop started **detached**; only the synced pass is awaited.
- [ ] `autoContinued` short-circuits before the loop starts.
- [ ] Loop stored in **one** `Coroutine` handle; `StopWinLoop()` before `ResetAllIcons()` at spin start.
- [ ] No `StopAllCoroutines()`.
- [ ] Blink sound fires once per line, not once per symbol.
- [ ] Reset loops iterate `positions.Count`, matching the play loops.

---

## Appendix: source map

For reference against the Age of Gods implementation.

| what | where |
|---|---|
| `AnimateLineWins`, `PlaySyncedPass`, `SingleLineLoop`, `PerLineLoop`, `StopAnimateLineWin` | [SlotController.cs:320-450](../Assets/Scripts/Base/SlotController.cs#L320-L450) |
| `StopWinLoop`, `ResetAllIcons` | [SlotController.cs:179-213](../Assets/Scripts/Base/SlotController.cs#L179-L213) |
| `StartSpin` — the teardown site | [SlotController.cs:61-77](../Assets/Scripts/Base/SlotController.cs#L61-L77) |
| `ToggleDarkFG` — grid dim | [SlotController.cs:467-486](../Assets/Scripts/Base/SlotController.cs#L467-L486) |
| `PlayWinIteration`, `ResetLineAnim`, `Reset`, `Lift`/`Drop` | [SlotIconView.cs:95-361](../Assets/Scripts/Base/SlotIconView.cs#L95-L361) |
| `OnSpinEnd` — the call site | [GameManager.cs:441-498](../Assets/Scripts/Base/GameManager.cs#L441-L498) |
| `LineWin`, `Position` | [SocketController.cs:518-551](../Assets/Scripts/Base/SocketController.cs#L518-L551) |
| `FormatSprite`, `GetSignificantDecimals` | [TextFormatter.cs:6-36](../Assets/Scripts/Base/TextFormatter.cs#L6-L36) |
