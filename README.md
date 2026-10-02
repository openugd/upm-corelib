# CoreLib

The Unity boundary of the OpenUGD family: a `MonoBehaviour` that boots a `com.openugd.context` `Context`,
plus presenters, logging, commands and the coroutine and thread seams that go with them.

`ContextBehaviour` owns a `Lifetime` that ends with its GameObject, builds a `Context` under it — the
container from [`com.openugd.context`](https://github.com/openugd/upm-context), with its async
`AwakeAsync` / `InitializeAsync` boot — and republishes Unity's frame and application callbacks as signals.
On top of that CoreLib adds a presenter tree for view composition, a command mapper for message handling,
tagged logging with a Unity console sink, and interfaces over coroutines and `SynchronizationContext` that a
test can replace.

## Install

### openupm-cli

```bash
openupm add com.openugd.corelib
```

### Scoped registry

Add the registry and the package to `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.openugd"
      ]
    }
  ],
  "dependencies": {
    "com.openugd.corelib": "2.0.0"
  }
}
```

### Git URL

In `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.openugd.corelib": "https://github.com/openugd/upm-corelib.git"
  }
}
```

Installing by git URL does not resolve the transitive OpenUGD dependencies — add
`com.openugd.lifetime`, `com.openugd.signal` and `com.openugd.context` the same way.

## Quick start

Define a service, register it with the context builder, and resolve it from a context.

```csharp
using System.Threading;
using System.Threading.Tasks;
using OpenUGD;
using OpenUGD.Core;
using OpenUGD.Core.Logging;
using UnityEngine;

public interface IProfileService
{
    string Name { get; }
}

// A service is a plain class. The container constructs it, injects its constructor, and drives it
// through the two boot phases it opts into. There is no base class to derive from.
public class ProfileService : IProfileService, IAwakeService, IInitializeService
{
    private readonly ILog _log;

    public ProfileService(ILog log) => _log = log;

    public string Name { get; private set; }

    public Task AwakeAsync(CancellationToken cancellationToken)
    {
        Name = "player";
        return Task.CompletedTask;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _log.I($"profile ready: {Name}");
        return Task.CompletedTask;
    }
}

// ContextBehaviour owns the Lifetime, builds the Context, and exposes the boot as an awaitable
// Task rather than discarding it. A failed startup surfaces in OnStartFailed instead of vanishing.
public class Bootstrap : ContextBehaviour
{
    protected override async Task<Context> CreateContextAsync(CancellationToken cancellationToken)
    {
        var log = new LogRoot(nameof(Bootstrap));
        log.UseUnityConsole(Lifetime);

        var builder = Context.CreateBuilder(Lifetime);
        builder.Services.AddInstance<ILog>(log);
        builder.Services.Add<ProfileService>().As<IProfileService>();

        return await builder.BuildAsync(cancellationToken);
    }

    protected override void OnStarted(Context context)
    {
        Debug.Log(context.Resolve<IProfileService>().Name);
    }
}
```

`BuildAsync` validates the whole graph before constructing anything, constructs in dependency order,
then runs `AwakeAsync` on every service that implements `IAwakeService` before any `InitializeAsync`.
Both phases go by dependency rank, not registration order. A failure disposes everything already built
and no `Context` escapes — see [`com.openugd.context`](https://github.com/openugd/upm-context).

`ContextBehaviour` is the MonoBehaviour entry point. It owns a `Lifetime` that ends with the
GameObject, exposes `OnUpdate` / `OnLateUpdate` / `OnFixedUpdate` / `OnFocus` / `OnPause` / `OnQuit` as
signals, implements `ICoroutineProvider`, and keeps the boot in `Startup` — an awaitable `Task` an
integration test can await and a failure cannot vanish into.

## API overview

This is the whole public surface of the package.

| Type | Namespace | Purpose |
| --- | --- | --- |
| `ContextBehaviour` | `OpenUGD.Core` | MonoBehaviour entry point: owns the `Lifetime`, builds the `Context`, exposes the Unity loop as signals, and surfaces the boot as an awaitable `Startup`. |
| `ContextBehaviourEditor` | `OpenUGD.Core.Editor` | Inspector for every `ContextBehaviour`: boot status, the failure message, and Rebuild in play mode. Editor only. |
| `Presenter`, `Presenter.Root`, `Presenter<TView>`, `Presenter<TView, TModel>` | `OpenUGD.Core.Presenters` | Hierarchical view composition with per-presenter lifetimes. Engine-free. |
| `IPresenterWithView`, `IPresenterWithModel`, `IPresenterWithModel<TModel>` | `OpenUGD.Core.Presenters` | The untyped faces through which code that knows a presenter only as a `Presenter` hands it a view and a model. |
| `PresenterExtensions` | `OpenUGD.Core.Presenters` | `GetViewType` and `GetChildren` snapshots of the tree. |
| `ViewBehaviour` | `OpenUGD.Core.Presenters` | A MonoBehaviour whose `Lifetime` ends in `OnDestroy`, to tie a presenter's scope to its view. |
| `ILog`, `ILogSink`, `LogFlags`, `LogRoot` | `OpenUGD.Core.Logging` | Tagged, flag-filtered logging with pluggable sinks. |
| `UnityLogSink`, `UnityLogSinkExtensions` | `OpenUGD.Core.Logging` | A sink that writes to the Unity console, and `UseUnityConsole(lifetime)` to attach one. |
| `ILocalization`, `ILocalizationChanged` | `OpenUGD.Core` | Optional localization seams the text presenters in `com.openugd.corelib.widgets` take with `[Inject(Optional = true)]`. |
| `ICommand`, `IMessage`, `ICommandMapper`, `ICommandMapperRemove`, `IMapCommand`, `ITellMessage`, `CommandMap`, `CommandMapper`, `CommandMapperExtensions` | `OpenUGD.Commands` | Maps message types to command types; each command is built by the `Context`. |
| `CommandMapExtensions` | `OpenUGD.Services.Commands` | `AddCommandMap()` on a `ServiceCollection`; `MapCommand()` and `Tell(message)` on a `Context`. |
| `ICoroutineProvider`, `CoroutineProvider` | `OpenUGD.Utils` | Coroutines behind an interface a test can replace. |
| `ISynchronizationContext`, `SynchronizationContextWrapper` | `OpenUGD.Utils` | Thread marshalling behind an interface a test can replace. |
| `SignalMonoBehaviour` | `OpenUGD.Utils.Components` | A GameObject's `Start`, `OnEnable`, `OnDisable` and `OnDestroy` as signals. |
| `ContextInstanceComponent`, `ContextFactoryInstancesComponent`, `IContextInstanceProvider`, `ContextFactoryInstancesComponentEditor` | `OpenUGD.Core`, `OpenUGD.Core.Editor` | Several game instances, one per display, in one process, and the factory's inspector. |

Composition itself lives in [`com.openugd.context`](https://github.com/openugd/upm-context): `Context`,
`ContextBuilder`, `ServiceCollection`, `[Inject]`. CoreLib is the Unity boundary around it.

## Not in 2.0.0

- **The UI services** — the window, HUD and tooltip services, `ITransformProvider` and
  `TransformProviderComponent`, the UI layers, and `PrefabResourceManager`, which only they used. They
  are to be replaced by one presenter host with policies in a separate package,
  `com.openugd.corelib.ui`, released as a 2.x when it is ready. The 0.6.x code is kept unchanged on the
  branch `park/ui-services` of this repository as that package's starting point. It is not a drop-in:
  it registers through `BootPhase.Configure`, which `com.openugd.context` 2.0.0 does not have. Until the
  new package ships, a project that needs these services stays on corelib 0.6.1.
- **The 0.6.x base types `Service`, `ContextFactoryComponent` and `ContextFactoryComponent<T>`.** No
  shim replaces them: shims that ship in 2.0.0 could only be removed in 3.0. A `Service` becomes a plain
  class that implements `IAwakeService` / `IInitializeService`, as `ProfileService` does above, and a
  `ContextFactoryComponent` subclass derives from `ContextBehaviour`, as `Bootstrap` does. The
  [CHANGELOG](CHANGELOG.md) lists each step of the port.

## Requirements

- Unity 6000.0 or newer
- `com.openugd.lifetime`, `com.openugd.signal` and `com.openugd.context` 2.0.0, and `com.unity.ugui`
  2.0.0 (built into Unity 6000.0) — all declared in `package.json`

## Licence

Apache-2.0 — see [LICENSE.md](LICENSE.md).
