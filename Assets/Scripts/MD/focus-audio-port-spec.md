# Browser focus + audio mute — port spec

How Age of Gods detects browser tab focus/blur and keeps audio mute state in sync — with the
player's own settings toggle — written so the behavior can be verified/rebuilt against a target
game's differently-named equivalents.

This document is self-contained: you should not need access to the Age of Gods repo to implement
against it. Where it cites source, that is for the curious, not as a dependency.

The target game already has `CustomJsLib.jslib`, `JSFunctCalls.cs`, a socket manager equivalent to
`SocketController`, and an audio manager equivalent to `AudioController`. This is not an install
guide for those pieces — it's a contract/behavior spec so you can confirm the port matches, plus a
list of things in the source that are worth a deliberate decision rather than a blind copy.

---

## 0. What the feature is

Two sub-features share one JS event, so they're covered together:

1. **Background kill-switch.** When the browser tab is hidden or the window loses focus for too
   long, the socket connection is force-closed and a disconnection popup is shown — so a player who
   alt-tabs away for minutes doesn't sit on a stale, silently-desynced connection.
2. **Audio mute sync.** Audio mutes automatically when the tab loses focus and unmutes when it
   regains focus. Independently, the player can mute/unmute manually via a sound-toggle button in
   settings. Both paths ultimately flip the same `AudioSource.mute` flags.

---

## 1. The JS ↔ Unity focus contract

`CustomJsLib.jslib` exposes `RegisterVisibilityChangeListener(gameObjectNamePtr)`. Calling it once
from Unity wires up four browser listeners: `visibilitychange`, `webkitvisibilitychange`, `blur`,
`focus`. All four funnel into one `sendFocusToUnity(focused)` helper that calls:

```js
SendMessage(gameObjectName, 'OnFocusChanged', focused ? '1' : '0');
```

Listeners are removed before being re-added (`removeEventListener` then `addEventListener`), so
calling `RegisterVisibilityChangeListener` more than once is safe and doesn't stack duplicate
callbacks.

**The contract is fixed and must not be renamed:**
- `gameObjectName` — the Unity GameObject `SendMessage` targets. Whatever name you pass in is the
  name it sends to; there is no separate registration for the method name.
- `'OnFocusChanged'` — the method name. Must exist as a `public` method (Unity's `SendMessage`
  cannot reach `private`/`internal` methods) with signature `void OnFocusChanged(string value)`.
- `'1'` / `'0'` — string, not bool. Convert at the receiving end (`value == "1"`).

Source: `Assets/Plugins/Demigiant/WebGL/CustomJsLib.jslib:2-50`.

---

## 2. Unity-side wrapper and receiver

`JSFunctCalls.cs` only wraps the `DllImport`:

```csharp
internal void RegisterVisibilityListener(string gameObjectName)
{
#if UNITY_WEBGL && !UNITY_EDITOR
    RegisterVisibilityChangeListener(gameObjectName);
#else
    Debug.Log("[JS] Visibility listener not registered (editor mode)");
#endif
}
```

(`Assets/Scripts/Base/JSFunctCalls.cs:20-28`.) It does **not** define `OnFocusChanged` itself — the
receiver lives on whichever GameObject calls `RegisterVisibilityListener` and passes its own name.

In this repo that's `UIManager`, not `SocketController` or `JSFunctCalls`:

- **Registration**, in `UIManager.Awake()`:
  ```csharp
  if (jsFunctCalls != null)
      jsFunctCalls.RegisterVisibilityListener(gameObject.name);
  ```
  (`Assets/Scripts/Base/UIManager.cs:135-136`.)

- **Receiver**, also on `UIManager` (same GameObject — required, see §1):
  ```csharp
  public void OnFocusChanged(string value)
  {
      bool focused = value == "1";
      audioController?.SetMuteAll(!focused);
      socketController?.HandleFocusChange(focused);
  }
  ```
  (`Assets/Scripts/Base/UIManager.cs:595-601`.)

So one incoming JS event does two things: mutes/unmutes audio directly, and forwards a `bool` to the
socket manager for the kill-switch timer. **Porting note:** put the receiver on whatever GameObject
you register from — if your target game registers from a different manager than the one that owns
the audio/socket references, either move the registration call or forward the event, but don't split
the registered name from the method's GameObject.

---

## 3. Background-timeout kill switch (`SocketController.cs`)

Fields (`Assets/Scripts/Base/SocketController.cs:40-45`):

```csharp
private bool hasFocus = true;
private float focusLostTime = 0f;
private Coroutine focusCheckRoutine;
private float maxBackgroundTime = 60f;
private bool isExiting = false;
private bool isBeingDestroyed = false;
```

`HandleFocusChange(bool focus)` (lines 271-289) takes the **bool** already converted by `UIManager`
— it never sees the raw `"1"/"0"` string:

- On blur (`focus == false`): records `focusLostTime = Time.time` and starts `FocusTimeoutCheck()`,
  guarded so it won't double-start if already running, and won't start at all if the socket is
  already exiting (`isExiting`) or the component is being destroyed (`isBeingDestroyed`).
- On refocus (`focus == true`): stops the coroutine if running.

`FocusTimeoutCheck()` (lines 291-316) polls once per second using **`WaitForSecondsRealtime`** —
important because it keeps counting real time even while the tab is backgrounded and Unity's
scaled/paused time isn't advancing normally. Once elapsed background time reaches
`maxBackgroundTime` (60s default):

1. Marks the socket as disconnected.
2. Stops the ping routine.
3. Force-closes the socket manager (wrapped in try/catch — closing an already-torn-down manager
   shouldn't throw uncaught).
4. Shows the disconnection popup.
5. Clears `focusCheckRoutine` and exits the loop (`yield break`).

If focus returns before the timeout, the routine is cancelled cleanly and none of the above fires —
the timer is *time-away*, not a hard countdown that survives refocus.

**Porting note:** `isExiting`/`isBeingDestroyed` guards exist so a focus-loss event received during
intentional shutdown (player-initiated exit, or component teardown) doesn't spin up a pointless
timeout coroutine that outlives its socket. Preserve the equivalent guards in your target socket
manager, or you risk a coroutine referencing a manager that's already null.

---

## 4. Audio mute — two independent triggers

There are **two separate call paths** that both end at `AudioController.SetMuteAll`, and they don't
know about each other. Decide in the target game whether you want both, or want to consolidate to
one — carrying both over unexamined just means two places to keep in sync later.

### 4a. Automatic — tab focus loss

Two paths reach the same effect, independently:

1. `UIManager.OnFocusChanged` (§2) calls `audioController?.SetMuteAll(!focused)` directly, driven by
   the JS bridge event.
2. `AudioController` also implements Unity's own `OnApplicationFocus(bool focus)` (lines 75-82),
   which iterates every registered `AudioEntry` and sets `source.mute = !focus` — **independently**
   of the JS-bridge path:
   ```csharp
   private void OnApplicationFocus(bool focus)
   {
       foreach (var entry in entries)
           entry.source.mute = !focus;
   }
   ```
   (`Assets/Scripts/Base/AudioController.cs:75-82`.)

Both fire on the same real-world event (tab blur), so in practice they agree — but they're redundant
plumbing, not a deliberate two-tier design. On WebGL builds `OnApplicationFocus` may or may not fire
reliably on tab visibility change depending on the browser (it's driven by the canvas/window focus,
not the Page Visibility API) — that's *why* the JS bridge path in §1–§3 was added in the first place.
**Porting note:** if your target game's `OnApplicationFocus` is unreliable in WebGL (common), keep
only the JS-bridge path and drop (or don't add) the `OnApplicationFocus` override, rather than
carrying both.

### 4b. Manual — settings sound-toggle button

`UIManager.ToggleSound()` (lines 616-629), wired to `SoundToggle_button.onClick` in `Start()`
(`SetButton(SoundToggle_button, ToggleSound);`, line 154):

```csharp
private bool isSound = true; // field, line 35

private void ToggleSound()
{
    isSound = !isSound;
    if (isSound)
    {
        SoundToggle_button.image.sprite = soundOFF;
        ToggleAudio?.Invoke(false);
    }
    else
    {
        SoundToggle_button.image.sprite = soundON;
        ToggleAudio?.Invoke(true);
    }
}
```

Read this carefully before porting — the naming is easy to misread as backwards. `isSound` is
flipped **first**, then branched on its *new* value. Trace it from initial state
(`isSound = true`, sound on):

| Click | `isSound` after flip | Branch taken | Sprite shown | `ToggleAudio` arg | Effect |
|---|---|---|---|---|---|
| 1 | `false` | `else` | `soundON` | `true` | audio muted |
| 2 | `true` | `if` | `soundOFF` | `false` | audio unmuted |

So the *sprite* shown is actually the icon for the state you'd switch to on the **next** click (not
the current state), and `isSound` ends up meaning "is muted" rather than what its name suggests.
It's internally consistent, not a bug in practice — but it's the kind of thing that reads as a bug
when you're porting from memory. Either replicate the exact flip-then-branch order, or rewrite it
cleanly as "flip a single `isMuted` bool, then derive both the sprite and the mute call from it" —
just don't half-copy it, since the sprite/mute pairing only stays correct if the flip-then-branch
order is preserved exactly.

`ToggleAudio` is declared as `internal Action<bool> ToggleAudio;` (`UIManager.cs:124`) and wired in
`GameManager`, not inside `UIManager` itself:

```csharp
uIManager.ToggleAudio = audioController.SetMuteAll;
```

(`Assets/Scripts/Base/GameManager.cs:89`.) This assignment must run before the player can click the
sound toggle, or `ToggleAudio?.Invoke(...)` silently no-ops (null-conditional). In this repo that's
guaranteed because `GameManager` wires it during its own init before gameplay is interactive — check
the equivalent ordering in your target game.

### `AudioController.SetMuteAll` / `SetMute`

```csharp
internal void SetMute(string type, bool mute)
{
    if (map.TryGetValue(type, out var entry)) entry.source.mute = mute;
}

internal void SetMuteAll(bool mute)
{
    foreach (var entry in entries) entry.source.mute = mute;
}
```

(`Assets/Scripts/Base/AudioController.cs:65-73`.) `mute` is applied to every `AudioSource` the
controller knows about (bg music, SFX, everything) — there is no separate bg-vs-sfx mute mode in
this game. If the target game needs independent bg/SFX mute toggles, `SetMute(type, mute)` already
exists per-channel; `SetMuteAll` just doesn't use it.

### No persistence

There is **no `PlayerPrefs` read or write anywhere for mute state**, in either `AudioController.cs`
or `UIManager.cs`. `isSound` is an in-memory bool that resets to `true` (unmuted) every session —
closing and reopening the game always starts unmuted regardless of what the player last chose. If
the target game is expected to remember the player's mute preference across sessions, that's a gap
to fill during the port, not something to carry over — there's nothing here to copy.

---

## 5. Call-chain summaries

**Kill switch:**
```
browser blur/visibilitychange
  → CustomJsLib.jslib sendFocusToUnity(false)
  → SendMessage(gameObjectName, 'OnFocusChanged', '0')
  → UIManager.OnFocusChanged("0")                          [UIManager.cs:595]
  → socketController.HandleFocusChange(false)               [SocketController.cs:271]
  → starts FocusTimeoutCheck() coroutine                    [SocketController.cs:291]
  → (60s later, if still unfocused) manager.Close() + UiManager.DisconnectionPopup()
```

**Manual mute (settings button):**
```
player clicks SoundToggle_button
  → UIManager.ToggleSound()                                 [UIManager.cs:616]
  → ToggleAudio?.Invoke(bool)                                [delegate, UIManager.cs:124]
  → (bound in GameManager.cs:89) AudioController.SetMuteAll(bool)
  → AudioSource.mute set on every registered channel         [AudioController.cs:70-73]
```

**Auto mute (focus loss) — two redundant paths:**
```
browser blur/visibilitychange
  → ... → UIManager.OnFocusChanged(value)                    [UIManager.cs:595]
  → audioController.SetMuteAll(!focused)                     [AudioController.cs:70]

-- independently --

Unity window/canvas loses OS focus
  → AudioController.OnApplicationFocus(focus)                [AudioController.cs:75]
  → AudioSource.mute set on every channel directly
```

---

## 6. Editor wiring checklist

| Field | Where to assign | Notes |
|---|---|---|
| `jsFunctCalls` (on the manager that registers) | Drag the GameObject holding your `JSFunctCalls` script | Must be `Awake()`, before any focus event can arrive |
| Registration call target | `gameObject.name` of the **same** component that defines `OnFocusChanged` | See §2 — registration name and receiver GameObject must match |
| `audioController` (on the receiver of `OnFocusChanged`) | Drag the audio manager | Used both for auto-mute and to wire `ToggleAudio` |
| `socketController` (on the receiver of `OnFocusChanged`) | Drag your socket manager equivalent | Receives the converted `bool`, not the raw JS string |
| `ToggleAudio` delegate | Assigned in your GameManager-equivalent's init, pointing at `audioController.SetMuteAll` | Must be wired before the sound-toggle button becomes clickable |
| `SoundToggle_button` + `soundON`/`soundOFF` sprites | Drag the button and its two icon sprites | See §4b table before porting the flip-then-branch order |
| `maxBackgroundTime` | Tune per game; 60s in Age of Gods | Real-time seconds backgrounded before kill-switch fires |
