# Iframe Host Integration — Changes for the Platform Team

The Buffalo Trail WebGL build has moved from the **AWT / React Native** template to the new
**`custom`** WebGL template, embedded in a plain **iframe** host (`GameLoader` in `Iframe.js`).
An iframe host communicates with the Unity build **only** via `window.postMessage`.

This doc describes the finalized message contract and the host-side (`Iframe.js`) changes we need.

---

## 1. Message contract

### Parent (host) → Unity iframe
The host posts `{ type, data }` into the iframe. The Unity build listens (in its jslib) and forwards
recognized types into Unity.

| `type` | `data` | Effect in Unity |
| --- | --- | --- |
| `TokenReceived` | `{ cookie, socketURL, nameSpace }` | Auth — Unity connects the socket. *(Name kept for now; may be renamed later — see note.)* |

> **Rename note:** `TokenReceived` is a slightly misleading name (it's the host's *response* to
> Unity's `authToken` request). We're keeping it for now to avoid breaking the contract; if we
> rename it later (e.g. `AuthTokenResponse`), we'll coordinate both sides.

`TokenReceived` is the **only** message the host needs to send. In particular, **do not send
`CloseGame`** — see section 3.

### Unity → Parent (host)
These already reach the parent via `window.parent.postMessage` and are unchanged:

| `type` | Meaning |
| --- | --- |
| `authToken` | Unity requests auth. Host must reply with `TokenReceived`. |
| `UnityLoaderProgress` | `{ progress: 0..100 }` — WebGL loader progress for the host's loading bar. |
| `OnEnter` | Game is ready. **Host should hide its loading screen on this.** |
| `OnExit` | Unity is exiting / socket closed. |
| `session_expired` | Auth/session expired. |
| `error` | Generic socket error. |

Loading "complete" is **not** a separate message — treat `OnEnter` as load-complete.

---

## 2. Remove the OC (orientation) sync from `Iframe.js`

Orientation/resize is now **self-contained inside the Unity build** — the build listens to its own
iframe viewport (`window.resize` / `orientationchange`) and drives its `OC.SwitchDisplay` directly.
The host no longer needs to push any of this.

Please **remove** from `GameLoader`:
- `_syncOCToUnity()`, `_setupOCListeners()`, `_teardownOCListeners()`
- the `_sendMessageToUnity` calls for `DiviceCheck`, `SwitchOrientation`, `SwitchDisplay`
- `SetDevicePixelRatio`

None of these have Unity receivers in this build (they were no-ops), and orientation now works
without them.

---

## 3. Closing / cleanup — remove `CloseGame`

**Remove the `CloseGame` message entirely** (the `_sendMessageToUnity("CloseGame", …)` calls in
`destroy()` / error handling). It isn't needed: removing the iframe from the DOM already closes the
socket — the browser tears down the WebSocket on iframe removal (visible in the Network tab). So just
tear down the iframe when you're done.

- **Memory:** `Application.Quit()` in WebGL does **not** free the WASM heap, so we don't call it on
  the C# side. Removing the iframe reclaims the heap. If you ever need to tear Unity down *without*
  removing the iframe element, call `unityInstance.Quit()` on the JS side instead.

---

## 4. Progress bar

The build reports `UnityLoaderProgress { progress: 0..100 }` during the WebGL loader phase. Drive the
host progress bar off that, and hide the loader on `OnEnter` (not on `progress === 100`, since Unity
still needs to connect the socket and receive `initData` after the engine finishes loading).
