# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Scope of work (IMPORTANT)

Only read, edit, and reason about **C# script files under `Assets/Scripts/`** (and, where necessary, related package scripts under `Assets/com.unity.uiextensions/`).

Everything Unity Editor–related is handled by the human developer, do **not** touch these:
- Scenes (`.unity`), prefabs (`.prefab`), materials, shaders, `.asset` files
- `.meta` files, `ProjectSettings/`, `Packages/manifest.json` / `packages-lock.json`
- Graphics, Spine, TextMesh Pro assets, sprites, audio
- Serialized inspector wiring — `[SerializeField]` references are assigned in the Editor by the developer, not in code

When a change would require an Editor action (assigning a reference, adding a component, wiring a button, importing an asset), describe what is needed and let the developer do it rather than editing serialized/asset files yourself.

## Project overview

Unity **6000.3.9f1** WebGL slot-machine game ("Buffalo Trail"). The Unity client is a thin rendering/animation layer over a server-authoritative backend: all game logic (RNG, payouts, balance) lives on a Socket.IO server, and the client emits spin requests and renders whatever result the server returns. The build is embedded in a React Native / React web platform that supplies the auth token and receives lifecycle messages.

## Architecture

Data flow for a spin: `SlotBehaviour` (UI button) → `SocketIOManager.AccumulateResult()` emits a `request`/`SPIN` → server → `ParseResponse()` deserializes `ResultData` into `resultData` and sets `isResultdone` → `SlotBehaviour` coroutine reads the result matrix and animates reels → `PayoutCalculation` draws winning lines → `UIManager`/`AudioController` update balance and play feedback.

- **`APIs/SocketIOManager.cs`** — central hub and the only networking layer. Uses the **Best.SocketIO** (Best HTTP/2) plugin. Connects to a namespace (default `playground`, overridden by data from the host platform), handles reconnection + ping/pong keepalive, and serializes/deserializes all messages. Incoming JSON is deserialized (Newtonsoft) into the data model classes defined at the bottom of this file (`Root`, `GameData`, `UiData`, `Player`, `Payload`, `WinningCombination`, etc.). Message dispatch is a `switch` on `id`: `initData` (initial game/UI/player state) and `ResultData` (spin outcome). Holds references to `SlotBehaviour`, `UIManager`, `JSHandler`, and `JSFunctCalls`.

- **`Functionality/SlotBehaviour.cs`** — largest script (~1000 lines); reel spin/stop animation (DOTween), bet controls, autospin, and rendering the server result matrix onto the reels. Reads results from `SocketIOManager`.

- **`Functionality/PayoutCalculation.cs`** — generates winning payline visuals at runtime using `UILineRenderer` (UI Extensions), from server-provided line indices.

- **`UI/UIManager.cs`** — menus, settings, paytable, info popups, balance/win text; consumes `UiData`/`Player` from the server.

- **`Functionality/AnimationController.cs`, `ImageAnimation.cs`, `AudioController.cs`, `DOTweenUIManager.cs`** — win-symbol highlighting, sprite-sheet animation, sound, and tween helpers.

- **`UI/ManageLineButtons.cs`** — pointer hover/click handlers that preview a static payline (calls into `PayoutCalculation`).

- **`Functionality/OrientationChange.cs`** — responsive layout for portrait/landscape.

### Host platform bridge (WebGL only)

The game runs embedded in a React Native/web host. All bridge code is guarded by `#if UNITY_WEBGL && !UNITY_EDITOR`, so it is inert in the Editor.

- **`JS/JSHandler.cs`** — `[DllImport("__Internal")]` calls to retrieve the auth token from a cookie (`GetAuthToken`) and free the native pointer. This token is what `SocketIOManager` uses to authenticate.
- **`JSFunctCalls.cs`** — outbound messages to the host: forwards Unity logs (`SendLogToReactNative`) and posts lifecycle events via `SendCustomMessage` (`OnEnter` on load, `OnExit` on quit).

## Key dependencies

- **Best.SocketIO / Best HTTP** — Socket.IO transport (not the Unity package manager; vendored plugin).
- **DOTween** (`DG.Tweening`) — all animation/tweening.
- **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`) — deserializing server messages. Note the client mixes `JsonConvert` (Newtonsoft) for inbound and `JsonUtility` (Unity) for outbound serialization.
- **UI Extensions** (`Assets/com.unity.uiextensions/`) — `UILineRenderer` for paylines.
- **Spine** — skeletal animations (assets handled in-Editor).

## Conventions

- Cross-script references are wired via `[SerializeField]` in the Editor and accessed with `internal` fields/methods; there is no DI or service locator. Scripts assume their references are already assigned.
- Server is authoritative: never compute payouts/balance client-side, render what the server sends.
- Comments tagged `//BackendChanges` mark logic tied to the backend/namespace contract, edit with care.

## Build & test

There is no CLI build or automated test suite in the repo. Building (WebGL) and all play/verify testing are done through the Unity Editor by the developer.
