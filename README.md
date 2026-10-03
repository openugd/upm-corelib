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
        _log.Info($"profile ready: {Name}");
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
Both phases go by dependency rank and, within a rank, one service at a time in registration order. A
failure disposes everything already built
and no `Context` escapes — see [`com.openugd.context`](https://github.com/openugd/upm-context).

`ContextBehaviour` is the MonoBehaviour entry point. It owns a `Lifetime` that ends with the
GameObject, exposes `OnUpdate` / `OnLateUpdate` / `OnFixedUpdate` / `OnFocus` / `OnPause` / `OnQuit` as
signals, implements `ICoroutineProvider`, and keeps the boot in `Startup` — an awaitable `Task` an
integration test can await and a failure cannot vanish into.

By default it keeps its GameObject across scene loads. A context that belongs to its scene says so:

```csharp
using System.Threading;
using System.Threading.Tasks;
using OpenUGD;
using OpenUGD.Core;

public class LevelContext : ContextBehaviour
{
    // Destroyed with the scene, and the context built under Lifetime with it.
    protected override bool PersistAcrossScenes => false;

    protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken) =>
        Context.CreateBuilder(Lifetime).BuildAsync(cancellationToken);

    protected override void Update()
    {
        base.Update(); // fires OnUpdate; leave it out and OnUpdate stops
        // per-frame work of your own
    }
}
```

**Play sessions.** `PlaySession.Lifetime` is the scope of one play session: nested in `Lifetime.Eternal`,
ended when the application quits — in the editor, when play mode is exited — and created afresh when the
next session starts. `Lifetime.Eternal` is a static field, so with domain reload disabled in *Enter Play Mode
Options* it carries everything nested in it from one session into the next; the play session does not.
`ContextBehaviour`, `ViewBehaviour` and `SignalMonoBehaviour` nest their scopes in it, so even a component
Unity never destroys is torn down with the session. Root anything else that lives as long as the
application there too:

```csharp
using System.Threading.Tasks;
using OpenUGD;
using OpenUGD.Core;

public static class Analytics
{
    // Not owned by a ContextBehaviour: ends with the play session, and the next session builds a new one.
    public static Task<Context> BuildAsync() => Context.CreateBuilder(PlaySession.Lifetime).BuildAsync();
}
```

The session ends at `Application.quitting`, before Unity destroys the scene, so the scopes nested in it end
newest first while every object is still alive. `ContextBehaviour.OnQuit` fires once on the way out, before
the context is disposed.

**A GameObject's lifetime.** `gameObject.GetLifetime()` returns a scope that ends when the GameObject is
destroyed or the play session ends, whichever comes first, kept by a `LifetimeBehaviour` it adds the first
time. Use it instead of writing that component yourself:

```csharp
using OpenUGD;
using OpenUGD.Core;
using UnityEngine;

public class ScoreLabel : MonoBehaviour
{
    public TextMesh Label;

    // Awake or later: the scope exists from the GameObject's Awake.
    private void Start() => Scores.Changed.Subscribe(gameObject.GetLifetime(), score => Label.text = score.ToString());
}

public static class Scores
{
    // Not a static readonly field: with domain reload disabled a static outlives the session, and a signal
    // made once would be dead from the second session on. AfterAssembliesLoaded runs each session, after
    // PlaySession has started it.
    public static Signal<int> Changed { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void OnSessionStart() => Changed = new Signal<int>(PlaySession.Lifetime);
}
```

The scope is created in `Awake` and nowhere earlier, because Unity sends `Awake` and `OnDestroy` only to a
component on an active GameObject, so a scope created before that might never end; on an inactive GameObject
that has no scope yet `GetLifetime` throws instead. `ViewBehaviour.Lifetime`, `SignalMonoBehaviour`'s signals and `ContextBehaviour.Lifetime` follow the
same rules: they exist from `Awake`, are nested in the play session, end in `OnDestroy`, and throw
`InvalidOperationException` if read before `Awake`.

`ContextBehaviour`, `ViewBehaviour` and `SignalMonoBehaviour` handle Unity's messages — `Awake`, `Update`,
`OnDestroy` and the rest — as `protected virtual` methods. Override one and call `base`: the base method is
what creates the scope, fires the signal or ends the scope. Declaring one without `override` hides it, and the
compiler says so (warning CS0114). Unity keeps only root objects across scene loads, so on a child object
`PersistAcrossScenes` changes nothing and `ContextBehaviour` logs a warning.

## Assemblies

The package holds five runtime assemblies, one per concern, each named as if it were its own package:

| Assembly | What it holds | References | UnityEngine |
| --- | --- | --- | --- |
| `com.openugd.corelib` | The Unity boundary: `ContextBehaviour`, `ViewBehaviour`, `SignalMonoBehaviour`, `PlaySession`, `LifetimeBehaviour`, the coroutine and `SynchronizationContext` seams, and `ContextPresenterFactory`, which adapts presenters to the context. | lifetime, signal, context, presenters | yes; no uGUI |
| `com.openugd.presenters` | The presenter tree and `IPresenterFactory`. | lifetime | no |
| `com.openugd.commands` | The command map. | lifetime, context | no |
| `com.openugd.logging` | Tagged logging and its sink interface. | nothing | no |
| `com.openugd.logging.unity` | The Unity console sink. | logging, lifetime | yes |
| `com.openugd.corelib.editor` | The `ContextBehaviour` inspector. Editor only. | corelib, context, lifetime | editor |

"No" means the assembly definition sets `noEngineReferences`, so the compiler rejects any use of
UnityEngine there and the code runs in a plain .NET test. None of the five references another, except the
console sink, which extends logging, and the Unity boundary, which adapts presenters to the context. The
presenters assembly does not reference the context: a presenter tree gets its presenters built and injected
through an `IPresenterFactory` — see [Presenters](#presenters).

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
| `LifetimeBehaviour`, `GameObjectLifetimeExtensions` | `com.openugd.corelib` | `OpenUGD.Core` | The scope of a GameObject, from `Awake` to `OnDestroy`: `gameObject.GetLifetime()`. |
| `PlaySession` | `com.openugd.corelib` | `OpenUGD.Core` | The scope of one play session: ends when the application quits or play mode is exited, and starts clean with domain reload disabled. |
| `ContextBehaviourEditor` | `com.openugd.corelib.editor` | `OpenUGD.Core.Editor` | Inspector for every `ContextBehaviour`: boot status, the failure message, and Rebuild in play mode. Editor only. |
| `ViewBehaviour` | `com.openugd.corelib` | `OpenUGD.Presenters` | A MonoBehaviour whose public `Lifetime` ends in `OnDestroy`; `presenter.CloseWith(view.Lifetime)` ties a presenter to it. |
| `ICoroutineProvider`, `CoroutineProvider` | `com.openugd.corelib` | `OpenUGD.Utils` | Coroutines behind an interface a test can replace. |
| `ISynchronizationContext`, `SynchronizationContextWrapper` | `com.openugd.corelib` | `OpenUGD.Utils` | Thread marshalling behind an interface a test can replace. |
| `SignalMonoBehaviour` | `com.openugd.corelib` | `OpenUGD.Utils.Components` | A GameObject's `Start`, `OnEnable`, `OnDisable` and `OnDestroy` as signals. |
| `Presenter`, `Presenter.Root`, `Presenter<TView>`, `Presenter<TView, TModel>` | `com.openugd.presenters` | `OpenUGD.Presenters` | Hierarchical view composition with per-presenter lifetimes, and a per-view `ViewLifetime`. `Presenter.Attach` roots a presenter on a scope of the caller's. |
| `IPresenterFactory` | `com.openugd.presenters` | `OpenUGD.Presenters` | How a tree has its presenters built (`Create`) and injected (`Inject`), without knowing the container. |
| `ContextPresenterFactory` | `com.openugd.corelib` | `OpenUGD.Presenters` | The `IPresenterFactory` over `OpenUGD.Context`. |
| `IPresenterWithView`, `IPresenterWithModel`, `IPresenterWithModel<TModel>` | `com.openugd.presenters` | `OpenUGD.Presenters` | The untyped faces through which code that knows a presenter only as a `Presenter` hands it a view and a model. |
| `PresenterExtensions` | `com.openugd.presenters` | `OpenUGD.Presenters` | `CloseWith`, which closes a presenter when a lifetime ends; `GetViewType`, the view type a presenter expects; `GetChildren`, a snapshot of its children, optionally recursive and filtered by type. |
| `ICommand`, `IMessage`, `ICommandMapper`, `ICommandMapperRemove`, `IMapCommand`, `ITellMessage`, `CommandMap`, `CommandMapper`, `CommandMapperExtensions` | `com.openugd.commands` | `OpenUGD.Commands` | Maps each message type, by exact type, to commands built by the `Context` or by a factory; every registration can be undone. |
| `CommandMapExtensions` | `com.openugd.commands` | `OpenUGD.Commands` | `AddCommandMap()` on a `ServiceCollection`; `MapCommand()` and `Tell(message)` on a `Context`. |
| `ILog`, `ILogSink`, `LogFlags`, `LogRoot` | `com.openugd.logging` | `OpenUGD.Logging` | Tagged, flag-filtered logging with pluggable sinks. |
| `UnityLogSink`, `UnityLogSinkExtensions` | `com.openugd.logging.unity` | `OpenUGD.Logging` | A sink that writes to the Unity console, and `UseUnityConsole(lifetime)` to attach one. |

Composition itself lives in [`com.openugd.context`](https://github.com/openugd/upm-context): `Context`,
`ContextBuilder`, `ServiceCollection`, `[Inject]`. CoreLib is the Unity boundary around it.

## Presenters

A presenter is handed a view and a model and renders one from the other. Presenters form a tree: each one
has a `Lifetime` nested in its parent's, so closing a presenter closes everything under it. The tree is
rooted on a `Lifetime` with an `IPresenterFactory`, which injects every presenter attached to it — with
`com.openugd.context`, that is `ContextPresenterFactory`, and `[Inject]` members of a presenter created with
`new` are filled in before its `OnInitialize`.

```csharp
using OpenUGD;
using OpenUGD.Logging;
using OpenUGD.Presenters;
using UnityEngine;
using UnityEngine.Events;

public class ScoreView : ViewBehaviour
{
    public UnityEvent ResetClicked = new UnityEvent();
    public TextMesh Label;
}

public class ScorePresenter : Presenter<ScoreView, int>
{
    [Inject(Optional = true)] private ILog _log;

    // Once per attached view: wire it, and register the unwiring on the view's own scope.
    protected override void OnViewAdded()
    {
        var view = View;
        view.ResetClicked.AddListener(OnReset);
        ViewLifetime.AddAction(() => view.ResetClicked.RemoveListener(OnReset));
    }

    // After every view attach and model change, only while live. Idempotent.
    protected override void OnRefresh() => View.Label.text = Model.ToString();

    private void OnReset()
    {
        _log?.Info("score reset");
        SetModel(0);
    }
}

public static class ScoreScreen
{
    public static ScorePresenter Open(Lifetime lifetime, Context context, ScoreView view)
    {
        var root = new Presenter.Root(lifetime, new ContextPresenterFactory(context));
        var score = root.AddPresenter(new ScorePresenter()).CloseWith(view.Lifetime);
        score.SetModel(42);
        score.SetView(view);
        return score;
    }
}
```

`OnViewAdded` wires a view and `OnRefresh` renders it. `ViewLifetime` is the scope of the current view: it
ends just before that view is detached or replaced, and when the presenter closes, so a listener registered
on it never outlives its view and a re-attached view is wired exactly once. `CloseWith(view.Lifetime)` goes
the other way: a `ViewBehaviour`'s `Lifetime` ends when its GameObject is destroyed, and the presenter
closes with it.

Attach first, then set the model and the view: `SetView` throws before the presenter is attached, and does
nothing once it has closed. `OnRefresh` runs only while the presenter is live — a view attached, the
presenter's scope alive, and the view not destroyed — so its body needs no `View != null` guard.

**Hosts.** Code that owns the scope a presenter lives in — a window service, say — roots each presenter on a
scope of its own with `Presenter.Attach(presenter, definition, factory)`, then calls `SetModel` and
`SetView`. `IPresenterFactory.Create(type)` builds a presenter it knows only by type; its members are
injected when it is attached. Both are public, so a host in another assembly needs no `InternalsVisibleTo`.

**Another container.** `IPresenterFactory` has two members, so an adapter is short. A sketch for
VContainer, not compiled or tested here; note that VContainer injects members marked with its own
`[Inject]`, not with OpenUGD's:

<!-- upm-tools: no-compile — VContainer is not a dependency of this package -->
```csharp
using System;
using OpenUGD.Presenters;
using VContainer;

public sealed class VContainerPresenterFactory : IPresenterFactory
{
    private readonly IObjectResolver _resolver;

    public VContainerPresenterFactory(IObjectResolver resolver) => _resolver = resolver;

    // Construction only: attaching the presenter calls Inject below, so its members are filled in then.
    public Presenter Create(Type presenterType) => (Presenter)Activator.CreateInstance(presenterType);

    public void Inject(Presenter presenter) => _resolver.Inject(presenter);
}
```

Under IL2CPP stripping, give `Create`'s parameter the annotation `IPresenterFactory.Create` carries,
`[DynamicallyAccessedMembers(PublicConstructors | NonPublicConstructors)]`, through an internal copy of the
attribute as this package declares one. Without it Unity's linker reports the mismatch (IL2092) and the
`Activator` call (IL2067).

## Logging

`LogRoot` is the root of a tree of tagged loggers and fans every record out to its sinks. Derive a logger per
class with `WithTag`; each write method names its level.

```csharp
using OpenUGD.Logging;

public class Inventory
{
    private readonly ILog _log;

    public Inventory(ILog log) => _log = log.WithTag(typeof(Inventory));

    public void Add(string item, int count)
    {
        _log.Info($"added {count} x {item}");

        // The argument of a write is evaluated even when the write is dropped; ask first if it is costly.
        if (_log.IsEnabled(LogFlags.Debug))
        {
            _log.Debug(DescribeContents());
        }
    }

    private string DescribeContents() => "...";
}
```

- `Verbose`, `Info`, `Warn`, `Error`, `Debug` and `Fatal` write at their level and return the logger they were
  called on, so calls chain. `Tag` is the full dotted path a record carries (`Bootstrap.Inventory` under
  `new LogRoot("Bootstrap")`), built once when the logger is derived.
- A record is delivered when its level is set in the logger's `Flag` and in every `Flag` above it, the root's
  included. `LogFlag` is that effective set, and `IsEnabled(flag)` tests it.
- `UseUnityConsole(lifetime)`, from `com.openugd.logging.unity`, attaches the console sink; `Subscribe`
  attaches your own `ILogSink`. Writing from any thread is safe, and so is subscribing or unsubscribing while
  records are being written, from inside a sink included.
- Loggers are not disposable, so a container that disposes what it built leaves them alone.
  `LogRoot.Dispose()` detaches every sink.

## Commands

A message is a class that implements `IMessage`; a command is a class that implements `ICommand` and runs once
for each message it is mapped to. `AddCommandMap()` registers the map; `Map` maps, `Tell` sends.

```csharp
using OpenUGD;
using OpenUGD.Commands;

public sealed class BuyMessage : IMessage
{
    public BuyMessage(string item) => Item = item;

    public string Item { get; }
}

public interface IShop
{
    void Buy(string item);
}

// Built for each message. Its constructor may take the message, a Lifetime (this execution's, which ends when
// Execute returns), a Lifetime.Definition (its registration: terminate it to unregister) and any service.
public sealed class BuyCommand : ICommand
{
    private readonly BuyMessage _message;
    private readonly IShop _shop;

    public BuyCommand(BuyMessage message, IShop shop)
    {
        _message = message;
        _shop = shop;
    }

    public void Execute() => _shop.Buy(_message.Item);
}

public static class ShopCommands
{
    public static void Wire(Context context, IShop shop)
    {
        var map = context.MapCommand();

        // By type: built by Context.Instantiate, by reflection, on every message. Checked now: a constructor
        // that cannot be satisfied throws here, not on the first Tell.
        Lifetime.Definition registration = map.Map<BuyMessage, BuyCommand>();

        // By factory: no reflection on Tell, for a message sent often.
        map.Map<BuyMessage>((message, lifetime) => new BuyCommand(message, shop));

        context.Tell(new BuyMessage("sword")); // runs both

        registration.Terminate(); // unregisters the first; it also ends with the map's scope
    }
}
```

- **Routing is by exact type.** `Tell` finds the mapping for the message's runtime type and nothing else: a
  mapping for a base class or an interface of the message does not run, and neither does one for a derived
  type. Map each concrete message type you send.
- **Every registration returns its `Lifetime.Definition`.** Terminate or dispose it to unregister;
  `ICommandMapperRemove.Remove<T>()` removes every registration of a command type.
- **One failing command does not stop the others.** They all run, and the failures arrive together as one
  `AggregateException`.
- **Main thread.** Registering, removing and telling are not thread-safe. A command may register, remove or tell
  from inside `Execute`; a registration made during a dispatch runs from the next one.

### Managed code stripping

A command registered by type is built by `Context.Instantiate`, by reflection. Unity's linker keeps the
constructor of a command type written at the registration call: `RegisterCommand<BuyCommand>()`,
`Map<BuyMessage, BuyCommand>()` and `RegisterCommand(typeof(BuyCommand))` all keep it, at Medium and High
stripping. A type the linker cannot trace does not keep it — one read from data, or one passed on by a generic
method of your own without the annotation. Put `[Inject]` on that command's constructor, as the
[`com.openugd.context`](https://github.com/openugd/upm-context) README describes under "Managed code
stripping", or register a factory, which the linker sees through.

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
