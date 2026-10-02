# CoreLib

The Unity boundary of the OpenUGD family: a `MonoBehaviour` that boots a `com.openugd.context` `Context`,
plus presenters, logging, commands and the coroutine and thread seams that go with them.

`ContextBehaviour` owns a `Lifetime` that ends with its GameObject, builds a `Context` under it — the
container from [`com.openugd.context`](https://github.com/openugd/upm-context), with its async
`AwakeAsync` / `InitializeAsync` boot — and republishes Unity's frame and application callbacks as signals.
On top of that CoreLib adds a presenter tree for view composition, a command mapper for message handling,
tagged logging with a Unity console sink, and interfaces over coroutines and `SynchronizationContext` that a
test can replace. Presenters, commands and logging each have an assembly of their own that does not
reference UnityEngine — see [Assemblies](#assemblies).

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
using OpenUGD.Logging;
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

## Assemblies

The package holds five runtime assemblies, one per concern, each named as if it were its own package:

| Assembly | What it holds | References | UnityEngine |
| --- | --- | --- | --- |
| `com.openugd.corelib` | The Unity boundary: `ContextBehaviour`, `ViewBehaviour`, `SignalMonoBehaviour`, the coroutine and `SynchronizationContext` seams. | lifetime, signal, context | yes; no uGUI |
| `com.openugd.presenters` | The presenter tree. | lifetime, context | no |
| `com.openugd.commands` | The command map. | lifetime, context | no |
| `com.openugd.logging` | Tagged logging and its sink interface. | nothing | no |
| `com.openugd.logging.unity` | The Unity console sink. | logging, lifetime | yes |
| `com.openugd.corelib.editor` | The `ContextBehaviour` inspector. Editor only. | corelib, context, lifetime | editor |

"No" means the assembly definition sets `noEngineReferences`, so the compiler rejects any use of
UnityEngine there and the code runs in a plain .NET test. None of the five references another, except the
console sink, which extends logging.

All of them are auto-referenced, so scripts in `Assembly-CSharp` see every type below. An assembly
definition of your own lists the assemblies it uses by name: `com.openugd.presenters` for a presenter,
`com.openugd.commands` for commands, `com.openugd.logging` for `ILog`, plus the assemblies whose types
those signatures carry — usually `com.openugd.lifetime` and `com.openugd.context` — because Unity does not
pass references on.

## API overview

This is the whole public surface of the package.

| Type | Assembly | Namespace | Purpose |
| --- | --- | --- | --- |
| `ContextBehaviour` | `com.openugd.corelib` | `OpenUGD.Core` | MonoBehaviour entry point: owns the `Lifetime`, builds the `Context`, exposes the Unity loop as signals, and surfaces the boot as an awaitable `Startup`. |
| `ContextBehaviourEditor` | `com.openugd.corelib.editor` | `OpenUGD.Core.Editor` | Inspector for every `ContextBehaviour`: boot status, the failure message, and Rebuild in play mode. Editor only. |
| `ViewBehaviour` | `com.openugd.corelib` | `OpenUGD.Presenters` | A MonoBehaviour whose `Lifetime` ends in `OnDestroy`, to tie a presenter's scope to its view. |
| `ICoroutineProvider`, `CoroutineProvider` | `com.openugd.corelib` | `OpenUGD.Utils` | Coroutines behind an interface a test can replace. |
| `ISynchronizationContext`, `SynchronizationContextWrapper` | `com.openugd.corelib` | `OpenUGD.Utils` | Thread marshalling behind an interface a test can replace. |
| `SignalMonoBehaviour` | `com.openugd.corelib` | `OpenUGD.Utils.Components` | A GameObject's `Start`, `OnEnable`, `OnDisable` and `OnDestroy` as signals. |
| `Presenter`, `Presenter.Root`, `Presenter<TView>`, `Presenter<TView, TModel>` | `com.openugd.presenters` | `OpenUGD.Presenters` | Hierarchical view composition with per-presenter lifetimes. |
| `IPresenterWithView`, `IPresenterWithModel`, `IPresenterWithModel<TModel>` | `com.openugd.presenters` | `OpenUGD.Presenters` | The untyped faces through which code that knows a presenter only as a `Presenter` hands it a view and a model. |
| `PresenterExtensions` | `com.openugd.presenters` | `OpenUGD.Presenters` | `GetViewType`, the view type a presenter expects; `GetChildren`, a snapshot of its children, optionally recursive and filtered by type. |
| `ICommand`, `IMessage`, `ICommandMapper`, `ICommandMapperRemove`, `IMapCommand`, `ITellMessage`, `CommandMap`, `CommandMapper`, `CommandMapperExtensions` | `com.openugd.commands` | `OpenUGD.Commands` | Maps message types to command types; each command is built by the `Context`. |
| `CommandMapExtensions` | `com.openugd.commands` | `OpenUGD.Commands` | `AddCommandMap()` on a `ServiceCollection`; `MapCommand()` and `Tell(message)` on a `Context`. |
| `ILog`, `ILogSink`, `LogFlags`, `LogRoot` | `com.openugd.logging` | `OpenUGD.Logging` | Tagged, flag-filtered logging with pluggable sinks. |
| `UnityLogSink`, `UnityLogSinkExtensions` | `com.openugd.logging.unity` | `OpenUGD.Logging` | A sink that writes to the Unity console, and `UseUnityConsole(lifetime)` to attach one. |

Composition itself lives in [`com.openugd.context`](https://github.com/openugd/upm-context): `Context`,
`ContextBuilder`, `ServiceCollection`, `[Inject]`. CoreLib is the Unity boundary around it.

## Samples

Import them from the Package Manager window: select CoreLib, then the Samples tab.

- **Multi Instance** — several game instances in one process, one per display, with input and audio focus
  switched between them: `ContextInstanceComponent`, `ContextFactoryInstancesComponent` and
  `IContextInstanceProvider`, which were part of the package in 0.6.x. The scripts keep their 0.6.x GUIDs,
  so prefabs and scenes that carried them bind to the imported copies. See the sample's README.

## Not in 2.0.0

- **The UI services** — the window, HUD and tooltip services, `ITransformProvider` and
  `TransformProviderComponent`, the UI layers, and `PrefabResourceManager`, which only they used. They
  are to be replaced by one presenter host with policies in a separate package,
  `com.openugd.corelib.ui`, released as a 2.x when it is ready. The services are kept, exactly as they
  were when they left this package, on the branch `park/ui-services` of this repository as that
  package's starting point. That code is not a drop-in: it registers through `BootPhase.Configure`,
  which `com.openugd.context` 2.0.0 does not have. Until the new package ships, a project that needs
  these services stays on corelib 0.6.1.
- **The 0.6.x base types `Service`, `ContextFactoryComponent` and `ContextFactoryComponent<T>`.** No
  shim replaces them: shims that ship in 2.0.0 could only be removed in 3.0. A `Service` becomes a plain
  class that implements `IAwakeService` / `IInitializeService`, as `ProfileService` does above, and a
  `ContextFactoryComponent` subclass derives from `ContextBehaviour`, as `Bootstrap` does. The
  [CHANGELOG](CHANGELOG.md) lists each step of the port.
- **The multi-display components** `ContextInstanceComponent`, `ContextFactoryInstancesComponent` and
  `IContextInstanceProvider`. They are the *Multi Instance* sample now; see *Samples* above.
- **`ILocalization` and `ILocalizationChanged`.** They moved to `com.openugd.corelib.widgets`, whose text
  presenters are the only code that takes them.

## Requirements

- Unity 6000.0 or newer
- `com.openugd.lifetime`, `com.openugd.signal` and `com.openugd.context` 2.0.0, declared in `package.json`.
  CoreLib does not use uGUI and does not depend on `com.unity.ugui`.

## Licence

Apache-2.0 — see [LICENSE.md](LICENSE.md).
