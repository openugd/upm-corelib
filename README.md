# CoreLib

Application composition for Unity: contexts, services, presenters and utilities.

CoreLib provides the wiring layer for a Unity application. A context builder collects services, resolves
them through `com.openugd.dependency.injection`, and drives them through an async `OnAwake` / `OnInitialize`
lifecycle bound to a `Lifetime` from `com.openugd.lifetime`. On top of that it adds a presenter tree for view
composition, a command mapper for message handling, tagged logging, and helpers for resource loading,
persistence and observable values.

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
    "com.openugd.corelib": "0.6.1"
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
`com.openugd.lifetime`, `com.openugd.signal` and `com.openugd.dependency.injection` the same way.

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

| Type | Namespace | Purpose |
| --- | --- | --- |
| `ContextBehaviour` | `OpenUGD.Core` | MonoBehaviour entry point: owns the `Lifetime`, builds the `Context`, exposes the Unity loop as signals, and surfaces the boot as an awaitable `Startup`. |
| `Presenter`, `Presenter<TView>`, `Presenter<TView, TModel>`, `ViewBehaviour` | `OpenUGD.Core.Presenters` | Hierarchical view composition with per-presenter lifetimes. |
| `ILog`, `ILogSink`, `LogFlags`, `LogRoot`, `UnityLogSink` | `OpenUGD.Core.Logging` | Tagged, flag-filtered logging with pluggable sinks. |
| `ILocalization`, `ILocalizationChanged` | `OpenUGD.Core` | Optional localization seams the text presenters take with `[Inject(Optional = true)]`. |
| `ITransformProvider`, `UILayer`, `UILayers`, `TransformProviderComponent` | `OpenUGD.Services.UI` | The scene's UI stack as an open-ended, keyed list of layers. |
| `IUIWindowService`, `WindowOptions`, `UIWindowComponentProvider` | `OpenUGD.Services.UI.Windows` | Opening, closing and pooling windows. |
| `IHudService`, `HudOptions` | `OpenUGD.Services.UI.Hud` | Always-on gameplay interface. |
| `IUITooltip`, `UITooltipMap`, `UITooltipPresenter` | `OpenUGD.Services.UI.Tooltip` | Pointer-driven tooltips. |
| `ICommandMapper`, `CommandMapper`, `CommandMap`, `IMapCommand` | `OpenUGD.Commands` | Maps messages to command types. |
| `PrefabResourceManager`, `ResourceResult` | `OpenUGD.Utils` | Lifetime-scoped prefab loading, instantiation and pooling. |
| `ICoroutineProvider`, `CoroutineProvider`, `SynchronizationContextWrapper` | `OpenUGD.Utils` | Coroutines and thread marshalling, behind interfaces a test can replace. |
| `Service` | `OpenUGD.Services` | `[Obsolete]` migration shim. Implement `IAwakeService` / `IInitializeService` instead. |

Composition itself lives in [`com.openugd.context`](https://github.com/openugd/upm-context): `Context`,
`ContextBuilder`, `ServiceCollection`, `[Inject]`. CoreLib is the Unity boundary around it.

## Requirements

- Unity 6000.0 or newer
- `com.openugd.lifetime`, `com.openugd.signal` and `com.openugd.context` 2.0.0, and `com.unity.ugui`
  2.0.0 (built into Unity 6000.0) — all declared in `package.json`

## Licence

Apache-2.0 — see [LICENSE.md](LICENSE.md).
