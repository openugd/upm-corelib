# Bootstrap

A `ContextBehaviour` that boots two services, logs to the Unity console through a log rooted at the play
session, and tears everything down when play mode ends.

## How to run it

1. Create an empty scene.
2. Add an empty GameObject at the root and put `BootstrapContext` on it.
3. Press Play and watch the Console; select the GameObject to see the boot status in its inspector.

Expected output, in order: `ScoreService` awake, then initialized, then `started`; then, once a second, a debug
line `+1 -> N` from the service and `score N` from the subscription. Exiting play mode prints
`quitting with score N` and then `context disposed`. Each line carries its tag path, such as
`Game.BootstrapContext.ScoreService->awake`.

## What to look at

| File | What it shows |
| --- | --- |
| `BootstrapContext.cs` | **Start here.** `CreateContextAsync` registers the services and builds the context under the behaviour's `Lifetime`; `OnStarted` subscribes with the context's lifetime, so nothing needs unsubscribing; `OnStartFailed` is where a failed boot surfaces. |
| `GameLog.cs` | A `LogRoot` rooted at `PlaySession.Lifetime`: created at the start of every session, attached to the console with `UseUnityConsole`, disposed when the session ends. Correct with domain reload disabled. |
| `ScoreService.cs` | A service is a plain class: constructor injection, `IAwakeService` then `IInitializeService`, and an `ISignal<int>` property backed by a signal created in the constructor. |
| `AutoScorer.cs` | A service that needs the frame loop gets an `ICoroutineProvider` — here the `ContextBehaviour` itself — and stops its coroutine on the context's `Lifetime`. |

## Notes

- `BootstrapContext` keeps its GameObject across scene loads (`PersistAcrossScenes` is `true` by default).
  Override it to return `false` for a context that belongs to one scene.
- Nothing here waits on `Startup`. An integration test can: `await behaviour.Startup` rethrows the real boot
  failure.
- This is sample code: once imported it is yours to change, and nothing in the package depends on it.
