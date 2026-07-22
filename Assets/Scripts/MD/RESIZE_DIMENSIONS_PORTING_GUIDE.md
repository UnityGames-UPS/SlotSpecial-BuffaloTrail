# Resize / Dimensions Porting Guide

Feature: **Window Resize → Unity Orientation**

A JS-level listener detects browser `resize` / `orientationchange` events and pushes the
current `window.innerWidth,window.innerHeight` back into Unity as a `"width,height"` string.
A receiver GameObject uses those dimensions to rotate/scale the UI for portrait vs. landscape.

Why JS owns the detection: `window.innerWidth` / `window.innerHeight` are the real browser
viewport, which is what the host cares about. Unity's `Screen.width/height` can lag or differ
from the CSS viewport in a WebGL canvas, so we read the values in JS and send them in.

---

## Prerequisites

- Unity 6 WebGL project with a `JSFunctCalls.cs` wrapper script and a `CustomJsLib.jslib` plugin.
- A receiver GameObject in the scene (here named **`OC`**) holding a component with a
  `void SwitchDisplay(string dimensions)` method that parses a `"width,height"` string.
- DOTween (used by the sample receiver for the rotate/scale tweens — not required for the bridge itself).

---

## Step 1 — Add the JS listener to `CustomJsLib.jslib`

Add this function inside the `mergeInto(LibraryManager.library, { ... })` block:

```js
RegisterResizeListener: function(gameObjectNamePtr, methodNamePtr) {
  var gameObjectName = UTF8ToString(gameObjectNamePtr);
  var methodName     = UTF8ToString(methodNamePtr);
  console.log('[JS] RegisterResizeListener called for:', gameObjectName + '.' + methodName);

  function sendDimensionsToUnity() {
    try {
      var dimensions = window.innerWidth + ',' + window.innerHeight;
      if (typeof SendMessage === 'function') {
        SendMessage(gameObjectName, methodName, dimensions);
      } else if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
        unityInstance.SendMessage(gameObjectName, methodName, dimensions);
      } else {
        console.warn('[JS] Unity SendMessage not available for dimensions');
        return;
      }
      console.log('[JS] Sent dimensions to Unity: ' + dimensions);
    } catch (err) {
      console.error('[JS] Error sending dimensions to Unity:', err);
    }
  }

  window._unityResizeCallback = function() {
    // Debounce rapid resize events into a single trailing call
    if (window._unityResizeTimeout) clearTimeout(window._unityResizeTimeout);
    window._unityResizeTimeout = setTimeout(sendDimensionsToUnity, 100);
  };

  // Remove before re-adding to avoid duplicates
  window.removeEventListener('resize', window._unityResizeCallback);
  window.removeEventListener('orientationchange', window._unityResizeCallback);

  window.addEventListener('resize', window._unityResizeCallback);
  window.addEventListener('orientationchange', window._unityResizeCallback);

  // Send an initial value so Unity matches the current window on startup
  sendDimensionsToUnity();

  console.log('[JS] Resize/orientation listeners registered for:', gameObjectName);
},
```

Notes:
- **Debounced** at 100 ms — `resize` fires continuously while dragging; we only send the trailing value.
- **Deduped** — listeners are removed before being re-added, so registering twice is safe.
- **Initial send** — one synchronous call at registration so Unity matches the window on startup.
- Passing `gameObjectName` + `methodName` as parameters keeps the jslib game-agnostic; the target
  is chosen entirely from C#.

---

## Step 2 — Add the DllImport and wrapper to `JSFunctCalls.cs`

```csharp
[DllImport("__Internal")] private static extern void RegisterResizeListener(string gameObjectName, string methodName);

internal void RegisterDimensionsListener(string gameObjectName = "OC", string methodName = "SwitchDisplay")
{
#if UNITY_WEBGL && !UNITY_EDITOR
  Debug.Log($"[JS] Registering resize listener on '{gameObjectName}.{methodName}'");
  RegisterResizeListener(gameObjectName, methodName);
#else
  Debug.Log($"[JS] Resize listener not registered ('{gameObjectName}.{methodName}', editor mode)");
#endif
}
```

The `#if UNITY_WEBGL && !UNITY_EDITOR` guard is required — the `[DllImport("__Internal")]` symbol
only exists in a WebGL build, so calling it in the editor would throw.

Adjust the default `gameObjectName` / `methodName` per game if your receiver is named differently.

---

## Step 3 — Register the listener from `JSFunctCalls.Start()`

`JSFunctCalls` registers itself in its own `Start()`, so no per-game wiring is needed — dropping
the updated `JSFunctCalls.cs` + `CustomJsLib.jslib` into a game is enough (as long as the receiver
GameObject/method match the defaults, see Step 4).

```csharp
// Registered in Start (not Awake) so the receiver GameObject's own Awake — which caches
// its reference resolution — is guaranteed to have run before the initial dimensions callback.
void Start()
{
  RegisterDimensionsListener();     // defaults to OC.SwitchDisplay
}
```

Register from **`Start()`**, not `Awake()`. The listener's initial send synchronously invokes
`<receiver>.SwitchDisplay(...)` during registration. If the **receiver** GameObject caches anything
in its own `Awake()` (e.g. its reference resolution / aspect ratio), registering from `Start()`
guarantees every `Awake()` has already run, so the first callback sees a fully initialized
receiver. Script execution order between two `Awake()` calls is otherwise undefined.

---

## Step 4 — The receiver: parse `"width,height"` and react

The receiver method must accept a single `string` (Unity `SendMessage` passes exactly one arg)
and split on `,`. Reference implementation (`OrientationChange.SwitchDisplay`):

```csharp
void SwitchDisplay(string dimensions)
{
  if (rotationRoutine != null) StopCoroutine(rotationRoutine);
  rotationRoutine = StartCoroutine(RotationCoroutine(dimensions));
}

IEnumerator RotationCoroutine(string dimensions)
{
  yield return new WaitForSecondsRealtime(waitForRotation);
  string[] parts = dimensions.Split(',');
  if (parts.Length == 2 &&
      int.TryParse(parts[0], out int width) &&
      int.TryParse(parts[1], out int height) &&
      width > 0 && height > 0)
  {
    bool isLandscape = width > height;
    // rotate UIWrapper, adjust CanvasScaler.matchWidthOrHeight, etc.
  }
}
```

`SwitchDisplay` may be `private` — Unity's `SendMessage` reaches private methods by default.

---

## Wiring checklist

| Item | Value |
| --- | --- |
| Receiver GameObject name | Must match the string passed to `RegisterDimensionsListener` (default `"OC"`) |
| Receiver method name | Must match (default `"SwitchDisplay"`), signature `void (string)` |
| `JSFunctCalls` in scene | Must be on an active GameObject so its `Start()` runs |
| Registration site | `JSFunctCalls.Start()` — self-contained, no external call needed |

> The receiver GameObject must be **active** in the scene when the initial send fires, or Unity's
> `SendMessage` logs a "target not found" warning.

---

## Editor testing

In WebGL only does JS drive the values. For editor testing, drive `SwitchDisplay` manually — e.g.
a debug key that calls `SwitchDisplay(Screen.width + "," + Screen.height)`:

```csharp
#if UNITY_EDITOR
private void Update()
{
  if (Input.GetKeyDown(KeyCode.Space))
    SwitchDisplay(Screen.width + "," + Screen.height);
}
#endif
```
