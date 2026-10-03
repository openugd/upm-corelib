# CoreLib

`com.openugd.corelib` is the Unity side of the OpenUGD family: `ContextBehaviour`, a `MonoBehaviour` that boots a
[`com.openugd.context`](https://github.com/openugd/upm-context) `Context` and ends it with its GameObject, plus a
presenter tree for UI composition, a command map for message handling and tagged logging. Use it when a Unity
project composes its services with `com.openugd.context` and wants one tested entry point, scopes tied to
GameObjects and the play session, and those three building blocks around it.

The package is five assemblies, each named as if it were its own package. Presenters, commands and logging do not
reference UnityEngine, so they run in a plain .NET test.

| Assembly | What it is | Use it when |
| --- | --- | --- |
| `com.openugd.corelib` | The Unity boundary: `ContextBehaviour`, `PlaySession`, `gameObject.GetLifetime()`, `ViewBehaviour`, `SignalMonoBehaviour`, coroutine and `SynchronizationContext` seams, and `ContextPresenterFactory`. | Always, in a Unity project: it boots the context and gives every scope an owner. |
| `com.openugd.presenters` | `Presenter`, a tree of scoped objects that render a model into a view they are handed. | You build UI, or any view, as presenters over views. |
| `com.openugd.commands` | `CommandMap`: messages mapped to commands built per message. | Systems should announce things without knowing who handles them. |
| `com.openugd.logging` | `ILog` and `LogRoot`: tagged, filtered logging with pluggable sinks. | You want one log tree with per-class tags and one place to filter. |
| `com.openugd.logging.unity` | `UnityLogSink` and `UseUnityConsole`. | That log should reach the Unity console. |

## Install

### openupm-cli

```bash
openupm add com.openugd.corelib@2.0.0
```

### Scoped registry

Add the OpenUPM registry and the package to `Packages/manifest.json`:

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

A git URL does not resolve the package's OpenUGD dependencies, so list them too:

```json
{
  "dependencies": {
    "com.openugd.lifetime": "https://github.com/openugd/upm-lifetime.git#2.0.0",
    "com.openugd.signal": "https://github.com/openugd/upm-signal.git#2.0.0",
    "com.openugd.context": "https://github.com/openugd/upm-context.git#2.0.0",
    "com.openugd.corelib": "https://github.com/openugd/upm-corelib.git#2.0.0"
  }
}
```

## Requirements

- Unity 6000.0 or newer.
- `com.openugd.lifetime`, `com.openugd.signal` and `com.openugd.context` 2.0.0 or a later 2.x, declared in
  `package.json`. The package does not use uGUI and does not depend on `com.unity.ugui`.

What each assembly references:

| Assembly | References | UnityEngine |
| --- | --- | --- |
| `com.openugd.corelib` | lifetime, signal, context, presenters | yes |
| `com.openugd.presenters` | lifetime | no |
| `com.openugd.commands` | lifetime, context | no |
| `com.openugd.logging` | nothing | no |
| `com.openugd.logging.unity` | logging, lifetime | yes |
| `com.openugd.corelib.editor` | corelib, context, lifetime | Editor only: the `ContextBehaviour` inspector |

"No" means the assembly definition sets `noEngineReferences`, so the compiler rejects UnityEngine there. All five
runtime assemblies are auto-referenced, so scripts in `Assembly-CSharp` see everything. An assembly definition of
your own lists by name what it uses — `com.openugd.presenters` for a presenter, `com.openugd.commands` for
commands, `com.openugd.logging` for `ILog` — plus the assemblies those signatures carry, usually
`com.openugd.lifetime` and `com.openugd.context`, because Unity does not pass references on.

## Quick start

A service, a log in the Unity console, and the `ContextBehaviour` that boots them. Put `GameContext` on a root
GameObject and press Play.

```csharp
using System.Threading;
using System.Threading.Tasks;
using OpenUGD;
using OpenUGD.Core;
using OpenUGD.Logging;

public interface IProfile
{
    string Name { get; }
}

// A service is a plain class: the container constructs it, fills its constructor, and runs the boot phases it
// implements - every AwakeAsync, then every InitializeAsync.
public sealed class Profile : IProfile, IAwakeService, IInitializeService
{
    private readonly ILog _log;

    public Profile(ILog log) => _log = log.WithTag(nameof(Profile));

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

public sealed class GameContext : ContextBehaviour
{
    // Runs from Awake. Build under Lifetime, and the context ends with this GameObject or the play session.
    protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken)
    {
        var log = new LogRoot("Game");
        log.UseUnityConsole(Lifetime);

        var builder = Context.CreateBuilder(Lifetime);
        builder.Services.AddInstance<ILog>(log);
        builder.Services.Add<Profile>().As<IProfile>();
        return builder.BuildAsync(cancellationToken);
    }

    // Runs once the boot has finished. A failed boot goes to OnStartFailed instead, which logs it.
    protected override void OnStarted(Context context) =>
        context.Resolve<ILog>().Info($"started as {context.Resolve<IProfile>().Name}");
}
```

`BuildAsync` validates the whole graph before it constructs anything, and a failure disposes whatever was built;
see the [`com.openugd.context` README](https://github.com/openugd/upm-context#readme) for registrations, the boot
and child contexts. The *Bootstrap* sample is a larger version of this.

## Concepts

### The boot: `ContextBehaviour`

`ContextBehaviour` owns a `Lifetime`, created in `Awake` and ended in `OnDestroy` or with the play session, and
calls `CreateContextAsync` from `Awake`. The boot is `Startup`, a `Task`:

- `OnStarted(context)` runs when it succeeds; `Context` is set from then on, and `null` before.
- A failure reaches `OnStartFailed(exception)` — which logs it unless you override it — *and* faults `Startup`,
  so an integration test can `await behaviour.Startup` and see the real exception. If `OnStarted` throws, the
  context is disposed and `Context` is `null` again.
- Destroying the GameObject during the boot cancels it: `Startup` ends cancelled and nothing is reported.
- `Rebuild()`, also in the component's context menu, ends the scope and boots again, in play mode only.

The behaviour republishes Unity's callbacks as signals — `OnUpdate`, `OnLateUpdate`, `OnFixedUpdate`, `OnFocus`,
`OnPause` and `OnQuit` — which exist from `Awake` and start firing whether or not the boot has finished. It
implements `ICoroutineProvider`, so a service can be handed it as its coroutine runner:
`builder.Services.AddInstance<ICoroutineProvider>(this)`.

By default the GameObject is kept across scene loads. A context that belongs to its scene says so, and a subclass
that handles a Unity message overrides it and calls `base`:

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

`ContextBehaviour`, `ViewBehaviour` and `SignalMonoBehaviour` handle `Awake`, `Update`, `OnDestroy` and the rest
as `protected virtual` methods; the base method creates the scope, fires the signal or ends the scope. Declaring
one without `override` hides it, and the compiler warns (CS0114). `PersistAcrossScenes` applies only to a root
object, as Unity's `DontDestroyOnLoad` does; on a child the behaviour logs a warning instead.

### Scopes: the play session and the GameObject

`PlaySession.Lifetime` is the scope of one play session. It ends when the application quits — in the editor, when
play mode is exited, at `Application.quitting`, before Unity destroys the scene — and a fresh one starts with the
next session, whether or not the domain is reloaded. `Lifetime.Eternal` is a static field, so with domain reload
disabled in *Enter Play Mode Options* everything nested in it survives into the next session; nest application-long
things in the play session instead:

```csharp
using System.Threading.Tasks;
using OpenUGD;
using OpenUGD.Core;

public static class Analytics
{
    // Not owned by a ContextBehaviour: it ends with the play session, and the next session builds a new one.
    public static Task<Context> BuildAsync() => Context.CreateBuilder(PlaySession.Lifetime).BuildAsync();
}
```

`gameObject.GetLifetime()` returns the scope of a GameObject: it ends when the GameObject is destroyed or the play
session ends, whichever comes first, and is kept by a `LifetimeBehaviour` it adds the first time.

```csharp
using OpenUGD;
using OpenUGD.Core;
using UnityEngine;

public class ScoreLabel : MonoBehaviour
{
    public TextMesh Label;

    // Awake or later: the scope exists from the GameObject's Awake.
    private void Start() =>
        Scores.Changed.Subscribe(gameObject.GetLifetime(), score => Label.text = score.ToString());
}

public static class Scores
{
    // Filled each session, not by a static initializer: with domain reload disabled a static outlives the
    // session, and a signal made once would be dead from the second session on.
    public static Signal<int> Changed { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void OnSessionStart() => Changed = new Signal<int>(PlaySession.Lifetime);
}
```

Every scope in the package follows the same rules: it is created in `Awake` and nowhere earlier, because Unity
sends `Awake` and `OnDestroy` only to a component on an active GameObject; it is nested in the play session; it ends
in `OnDestroy`; and reading it before `Awake` throws `InvalidOperationException`. That is why `GetLifetime` on an
inactive GameObject with no scope yet throws instead of creating one that might never end.

### Presenters

A presenter is handed a view and a model and renders one from the other. Presenters form a tree: each has a
`Lifetime` nested in its parent's, so closing a presenter closes everything under it. A tree is rooted on a
`Lifetime` with an `IPresenterFactory`, which injects every presenter attached to it; with `com.openugd.context`
that is `ContextPresenterFactory`, which fills `[Inject]` members of presenters created with `new` before their
`OnInitialize`.

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

- **Two hooks.** `OnViewAdded` runs once per attached view, for wiring. `OnRefresh` runs after every view attach,
  model change and `Refresh()`, for rendering, and only while the presenter is live — a view attached, the
  presenter's scope alive, the view not destroyed — so it needs no `View != null` guard.
- **Two scopes.** `Lifetime` is the presenter's. `ViewLifetime` is the current view's: it ends just before that
  view is detached or replaced, and when the presenter closes, so a listener registered on it never outlives its
  view. `presenter.CloseWith(view.Lifetime)` goes the other way and closes the presenter when its view is
  destroyed.
- **Open sequence.** Attach, then `SetModel`, then `SetView`: `SetView` throws before the presenter is attached and
  does nothing once it has closed.
- **Teardown order.** A presenter's lifetime unwinds newest first: children and clean-up registered after the
  attach, in reverse order, then `OnClose`, then the unlink from the parent. Clean-up that throws does not stop
  the rest; afterwards one failure is rethrown as itself and several as one `AggregateException`.
- **Hosts.** Code that owns a presenter's scope — a window service, say — calls
  `Presenter.Attach(presenter, definition, factory)`, then `SetModel` and `SetView`. Clean-up the host registers on
  the definition before `Attach` runs after the presenter's `OnClose`: the place to return a pooled view.
  `IPresenterFactory.Create(type)` builds a presenter known only by its type.

The *Presenters* sample runs all of this step by step and prints the order.

**Another container.** `IPresenterFactory` has two members, so an adapter is short. A sketch for VContainer, not
compiled or tested here; VContainer injects members marked with its own `[Inject]`, not OpenUGD's:

<!-- upm-tools: no-compile (VContainer is not a dependency of this package) -->
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
attribute as this package declares one; without it Unity's linker reports IL2092 and IL2067.

### Commands

A message is a class that implements `IMessage`; a command is a class that implements `ICommand` and runs once for
each message it is mapped to. `AddCommandMap()` registers the map; `Map` maps, `Tell` sends.

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

        // By type: checked and planned now - a constructor or [Inject] member the context cannot supply throws
        // here, not on the first Tell.
        Lifetime.Definition registration = map.Map<BuyMessage, BuyCommand>();

        // By factory: your code builds the command, with no reflection.
        map.Map<BuyMessage>((message, lifetime) => new BuyCommand(message, shop));

        context.Tell(new BuyMessage("sword")); // runs both

        registration.Terminate(); // unregisters the first; it also ends with the map's scope
    }
}
```

- **Routing is by exact type.** `Tell` runs the commands mapped to the message's runtime type and nothing else: a
  mapping for a base class or an interface of the message does not run, and neither does one for a derived type.
- **By type or by factory.** A command registered by type is checked at registration — the constructor is chosen
  as `Context.Instantiate` chooses one, and every `[Inject]` member must be resolvable from the context — and
  each `Tell` then calls that constructor and fills those members. A factory builds the command itself, with no
  reflection, and its command is not injected.
- **Every registration returns its `Lifetime.Definition`.** Terminate or dispose it to unregister;
  `ICommandMapperRemove.Remove<T>()` removes every registration of a command type. A `oneTime` registration runs
  once — even if its command tells the same message again — and then ends.
- **Each execution has a scope.** The `Lifetime` a command receives ends when `Execute` returns or throws.
- **Failures.** One failing command does not stop the others, and listeners still run. Afterwards a single
  failure is rethrown as itself and several as one `AggregateException` of the failures themselves.
- **Listeners.** `CommandMap.Subscribe(lifetime, listener)` forwards every message, whatever its type, to an
  `ITellMessage`, after the commands.
- **Main thread.** Mapping, removing and telling are not thread-safe. A command may map, remove or tell from
  inside `Execute`; a registration made during a dispatch runs from the next one.

**Managed code stripping.** A command registered by type is constructed by reflection. Unity's linker keeps the
constructors of a command type written at the registration call — `RegisterCommand<BuyCommand>()`,
`Map<BuyMessage, BuyCommand>()`, `RegisterCommand(typeof(BuyCommand))` — at Medium and High stripping. A type it
cannot trace, read from data or passed on by a generic method of your own, needs `[Inject]` on its constructor, as
the [`com.openugd.context` README](https://github.com/openugd/upm-context#readme) describes; or register a
factory.

### Logging

`LogRoot` is the root of a tree of tagged loggers and hands every record to its sinks. Derive a logger per class
with `WithTag`; each write method names its level.

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

- `Verbose`, `Info`, `Warn`, `Error`, `Debug` and `Fatal` write at their level and return the logger, so calls
  chain. `Tag` is the dotted path a record carries (`Game.Inventory` under `new LogRoot("Game")`).
- A record is delivered when its level is set in the logger's `Flag` and in every `Flag` above it, the root's
  included; `LogFlag` is that effective set and `IsEnabled(flag)` tests it. Narrow the root's `Flag` to quieten a
  whole build.
- `UseUnityConsole(lifetime)` attaches the console sink for as long as `lifetime` lives; `Subscribe` attaches a
  sink of your own. Writing is safe from any thread, and so is subscribing or unsubscribing while records are
  written. A sink that throws aborts that record's delivery and the exception reaches the writer.
- Loggers are not disposable. `LogRoot.Dispose()` detaches every sink, so a context that disposes what it built
  leaves a working, silent root behind.

### Coroutines, threads and Unity callbacks

`ICoroutineProvider` lets a service or a presenter run a coroutine without holding a `MonoBehaviour`, and a test
replace it. `ContextBehaviour` implements it; `new CoroutineProvider(monoBehaviour)` adapts any other behaviour.
`StartCoroutine` never returns `null`: on a host that is destroyed, disabled or inactive it throws. A coroutine is
bound to its host, not to a `Lifetime`; stop it on one with
`lifetime.AddAction(() => provider.StopCoroutine(coroutine))`.

`ISynchronizationContext` wraps a `SynchronizationContext` the same way; capture
`new SynchronizationContextWrapper(SynchronizationContext.Current)` on the main thread during the boot.

`SignalMonoBehaviour` exposes a GameObject's `Start`, `OnEnable`, `OnDisable` and `OnDestroy` as signals, for a
plain C# object that needs them. It has no per-frame signals; subscribe to `ContextBehaviour.OnUpdate` instead.

## API overview

| Type | Assembly, namespace | Purpose |
| --- | --- | --- |
| `ContextBehaviour` | corelib, `OpenUGD.Core` | Owns a `Lifetime`, boots a `Context` (`CreateContextAsync`, `Startup`, `OnStarted`, `OnStartFailed`, `Rebuild`), republishes the Unity loop as signals, `PersistAcrossScenes`. |
| `PlaySession` | corelib, `OpenUGD.Core` | `PlaySession.Lifetime`: the scope of one play session. |
| `LifetimeBehaviour`, `GameObjectLifetimeExtensions` | corelib, `OpenUGD.Core` | The scope of a GameObject: `gameObject.GetLifetime()`, `component.GetLifetime()`. |
| `ContextBehaviourEditor` | corelib.editor, `OpenUGD.Core.Editor` | Inspector for every `ContextBehaviour`: boot status, the failure, Rebuild. |
| `ViewBehaviour` | corelib, `OpenUGD.Presenters` | A `MonoBehaviour` view with a public `Lifetime` that ends in `OnDestroy`. |
| `ContextPresenterFactory` | corelib, `OpenUGD.Presenters` | The `IPresenterFactory` over a `Context`: `Context.Instantiate` and `Context.Inject`. |
| `ICoroutineProvider`, `CoroutineProvider` | corelib, `OpenUGD.Utils` | Coroutines behind an interface. |
| `ISynchronizationContext`, `SynchronizationContextWrapper` | corelib, `OpenUGD.Utils` | Thread marshalling behind an interface. |
| `SignalMonoBehaviour` | corelib, `OpenUGD.Utils.Components` | `StartSignal`, `EnableSignal`, `DisableSignal`, `DestroySignal`. |
| `Presenter`, `Presenter.Root` | presenters, `OpenUGD.Presenters` | A tree node: `Lifetime`, `Children`, `AddPresenter`, `Close`/`Dispose`, `OnInitialize`, `OnClose`; static `Attach`. `Root` roots a tree on a `Lifetime`. |
| `Presenter<TView>`, `Presenter<TView, TModel>` | presenters, `OpenUGD.Presenters` | `View`, `SetView`, `ViewLifetime`, `Refresh`, `OnViewAdded`, `OnViewAfterRemoved`, `OnRefresh`, `IsLive`; `Model`, `SetModel`, `OnBeforeModelChange`. |
| `IPresenterWithView`, `IPresenterWithModel`, `IPresenterWithModel<TModel>` | presenters, `OpenUGD.Presenters` | The untyped faces a host uses to hand a presenter its view and model. |
| `IPresenterFactory` | presenters, `OpenUGD.Presenters` | `Create(Type)` and `Inject(Presenter)`: how a tree gets its presenters built and injected. |
| `PresenterExtensions` | presenters, `OpenUGD.Presenters` | `CloseWith`, `GetViewType`, `GetChildren`. |
| `IMessage`, `ICommand` | commands, `OpenUGD.Commands` | A message; a command run for it. |
| `IMapCommand`, `ITellMessage` | commands, `OpenUGD.Commands` | Map messages to commands; send a message. |
| `ICommandMapper`, `ICommandMapperRemove` | commands, `OpenUGD.Commands` | Register commands for one message type, by type or by factory; remove by type. |
| `CommandMap`, `CommandMapper` | commands, `OpenUGD.Commands` | The implementations; `CommandMap.Subscribe` adds a listener for every message. |
| `CommandMapperExtensions` | commands, `OpenUGD.Commands` | `RegisterCommand<TCommand>()`, `Map<TMessage, TCommand>()`, `Map<TMessage>(factory)`. |
| `CommandMapExtensions` | commands, `OpenUGD.Commands` | `ServiceCollection.AddCommandMap()`; `Context.MapCommand()`, `Context.Tell(message)`. |
| `ILog`, `LogRoot` | logging, `OpenUGD.Logging` | A tagged, filtered logger and the root that owns the sinks. |
| `ILogSink`, `LogFlags` | logging, `OpenUGD.Logging` | Where records go; the six levels as flags. |
| `UnityLogSink`, `UnityLogSinkExtensions` | logging.unity, `OpenUGD.Logging` | The console sink and `UseUnityConsole(lifetime)`. |

## Samples

Import them from *Window > Package Manager > CoreLib > Samples*. Each folder has its own README.

| Sample | Shows |
| --- | --- |
| Bootstrap | A `ContextBehaviour` that boots two services, with a `LogRoot` rooted at `PlaySession.Lifetime` writing to the console and a coroutine through `ICoroutineProvider`. Start here. |
| Presenters | A presenter tree attached with `Presenter.Attach` and `ContextPresenterFactory`, a view swap that shows `ViewLifetime` unwiring the old view, `CloseWith`, and the teardown order. |
| Commands | A shop: commands mapped by type, by factory and one-time, a registration undone, a refusal rethrown to the caller of `Tell`, a per-execution `Lifetime` and a listener. |
| Multi Instance | Several game instances in one process, one per display: the 0.6.x multi-display components with their GUIDs, for projects that used them. |

## Running the tests

The package ships four test assemblies: `com.openugd.presenters.tests`, `com.openugd.commands.tests` and
`com.openugd.logging.tests` in Edit Mode, and `com.openugd.corelib.playmode.tests` in Play Mode. To run them in
your project, list the package under `testables` in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.openugd.corelib": "2.0.0"
  },
  "testables": [
    "com.openugd.corelib"
  ]
}
```

Then open *Window > General > Test Runner* and run the *EditMode* and *PlayMode* tabs. The Unity Test Framework
package must be installed; new projects include it.

## Upgrading to 2.0

This section is for users of `com.openugd.corelib` 0.6.1. Version 2.0.0 requires Unity 6000.0 or newer and is
licensed under Apache-2.0. Most of it is breaking; each part says whom it affects.

1. In `Packages/manifest.json`, set `com.openugd.corelib` to `2.0.0` and remove `com.openugd.dependency.injection`:
   corelib now depends on `com.openugd.lifetime`, `com.openugd.signal` and `com.openugd.context` 2.0.0, and code
   that sees both `com.openugd.dependency.injection` and `com.openugd.context` fails with CS0433 on `[Inject]`.
2. Fix the `using` lines and assembly references below, then work through the sections that apply.

### Assemblies and namespaces

The one runtime assembly of 0.6.1 is five. All are auto-referenced, so `Assembly-CSharp` needs only the
namespaces; an assembly definition of your own adds the assemblies it uses (see [Requirements](#requirements)).

| 0.6.1 namespace | 2.0 namespace | 2.0 assembly |
| --- | --- | --- |
| `OpenUGD.Core.Widgets` | `OpenUGD.Presenters` | `com.openugd.presenters`; `ViewBehaviour` is in `com.openugd.corelib` |
| `OpenUGD.Core.Loggers` | `OpenUGD.Logging` | `com.openugd.logging`; `UnityLogSink` is in `com.openugd.logging.unity` |
| `OpenUGD.Commands`, `OpenUGD.Services.Commands` | `OpenUGD.Commands` | `com.openugd.commands` |
| `OpenUGD.Core`, `OpenUGD.Utils`, `OpenUGD.Utils.Components` | unchanged | `com.openugd.corelib` |

### Services and the context: `Service` → `IAwakeService` / `IInitializeService`

The composition layer of 0.6.1 — `ContextStartup`, `IContextServiceSetup`, the service builder and observers,
`IContext`, `OpenUGD.Core.Context` — is gone; `com.openugd.context` replaces it, and its README has a section,
*From corelib 0.6.x*, that maps each piece. A `Service` becomes a plain class that implements the boot phases it
needs. **Affects you if** you derived from `Service`.

<!-- upm-tools: no-compile (the corelib 0.6.1 API, removed in 2.0) -->
```csharp
// 0.6.1
public class SaveService : Service, ISaveService
{
    private IClock _clock;

    protected override Task OnAwake()
    {
        _clock = Resolve<IClock>();      // null when IClock was not registered
        Lifetime.AddAction(Flush);
        Logger.I("awake");
        return Task.CompletedTask;
    }

    protected override Task OnInitialize() => Load();
}
```

```csharp
// 2.0
using System.Threading;
using System.Threading.Tasks;
using OpenUGD;
using OpenUGD.Logging;

public interface IClock { }

public interface ISaveService { }

public sealed class SaveService : ISaveService, IAwakeService, IInitializeService
{
    private readonly IClock _clock;
    private readonly ILog _log;

    // What Resolve<T>() returned, Lifetime and Logger are constructor parameters. The build fails if IClock is
    // not registered; Lifetime is the context's own.
    public SaveService(IClock clock, ILog log, Lifetime lifetime)
    {
        _clock = clock;
        _log = log.WithTag(nameof(SaveService));
        lifetime.AddAction(Flush);
    }

    public Task AwakeAsync(CancellationToken cancellationToken)
    {
        _log.Info("awake");
        return Task.CompletedTask;
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => Load();

    private void Flush() { }

    private Task Load() => Task.CompletedTask;
}
```

`Service.State` and `Service.Internal` have no replacement: a context that `BuildAsync` returned has booted every
service. Register the logger yourself (`AddInstance<ILog>(log)`) and take it as a parameter.

### `ContextFactoryComponent` → `ContextBehaviour`

`ContextFactoryComponent` and `ContextFactoryComponent<T>` are removed. Derive from `ContextBehaviour` and build the
context in `CreateContextAsync`. **Affects you if** you had a factory component; keep the subclass's file and
`.meta`, and scenes keep referencing it, because a scene stores the script GUID of the concrete class.

<!-- upm-tools: no-compile (the corelib 0.6.1 API, removed in 2.0) -->
```csharp
// 0.6.1
public class GameFactory : ContextFactoryComponent<GameContext>
{
    protected override GameContext CreateContext(Lifetime lifetime) => new GameContext(lifetime, this);
}
```

```csharp
// 2.0
using System.Threading;
using System.Threading.Tasks;
using OpenUGD;
using OpenUGD.Core;
using OpenUGD.Utils;

public sealed class GameFactory : ContextBehaviour
{
    protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken)
    {
        var builder = Context.CreateBuilder(Lifetime);
        builder.Services.AddInstance<ICoroutineProvider>(this);   // what Injector.ToValue(factory) did
        // builder.Services.Add<...>() for each service
        return builder.BuildAsync(cancellationToken);
    }

    protected override void OnStarted(Context context)
    {
        // what ContextStartup.OnStart did
    }
}
```

What else changed at the boundary, compiling unchanged unless noted:

- **The boot is `Startup`.** A failure reaches `OnStartFailed` and faults `Startup`; 0.6.1 lost it in a discarded
  task. `Context` is `null` until the boot has finished.
- **`DontDestroyOnLoad` is a choice.** It is still the default; override `PersistAcrossScenes` to return `false`.
- **Unity messages are `protected virtual`.** A subclass that declared its own `Update`, `Awake` or `OnDestroy`
  silently replaced the base's in 0.6.1; it now gets warning CS0114. Declare it `protected override` and call
  `base` (`base.Awake()` first).
- **Scopes end with the play session.** `ContextBehaviour`, `ViewBehaviour` and `SignalMonoBehaviour` nest their
  scopes in `PlaySession.Lifetime`, not `Lifetime.Eternal`. When play mode is exited they end at
  `Application.quitting`, newest first, rather than one by one in `OnDestroy`.
- **`ICoroutineProvider.StartCoroutine` never returns `null`.** `CoroutineProvider` and `ContextBehaviour` throw
  for a host that is destroyed, disabled or inactive in the hierarchy; 0.6.1 returned `null` and the coroutine never
  ran. `SynchronizationContextWrapper` rejects a `null` context.
- **`SignalMonoBehaviour`** loses `UpdateSignal`, `LateUpdateSignal`, `FixedUpdateSignal` and `AwakeSignal`;
  subscribe to `ContextBehaviour.OnUpdate` instead. Reading a signal before `Awake` throws.

### `Widget` → `Presenter`

**Affects you if** you wrote widgets. The type is renamed, its two lifecycle hooks are replaced, and it reaches its
dependencies through a factory instead of the injector.

| 0.6.1 | 2.0 |
| --- | --- |
| `Widget`, `Widget<TView>`, `Widget<TView, TModel>` | `Presenter`, `Presenter<TView>`, `Presenter<TView, TModel>` |
| `WidgetView` (protected `Lifetime`, `OnAwake`) | `ViewBehaviour` (public `Lifetime`; override `Awake` and call `base.Awake()`) |
| `WidgetExtensions` | `PresenterExtensions` |
| `IWidgetWithView`, `IWidgetWithModel` | `IPresenterWithView`, `IPresenterWithModel` |
| `AddWidget` | `AddPresenter` |
| `new Widget.Root(lifetime, injector)` | `new Presenter.Root(lifetime, new ContextPresenterFactory(context))` |
| `OnReady()` | `OnViewAdded()` to wire the view, `OnRefresh()` to render |
| `OnAfterModelChanged()` | `OnRefresh()` |
| `OnViewBeforeRemove()` | clean-up registered on `ViewLifetime` in `OnViewAdded` |
| `Resolve<T>()` on a widget (`IResolve`) | an `[Inject]` member, `[Inject(Optional = true)]` if it may be absent |
| `Children` (a new array per call) | `Children` (a live `IReadOnlyList`); `GetChildren(list)` for a snapshot |

<!-- upm-tools: no-compile (the corelib 0.6.1 API, removed in 2.0) -->
```csharp
// 0.6.1
public class ScoreWidget : Widget<ScoreView, int>
{
    [Inject] private Logger _logger;

    protected override void OnReady()
    {
        View.ResetClicked.AddListener(OnReset);
        Lifetime.AddAction(() => View.ResetClicked.RemoveListener(OnReset));   // outlives a replaced view
        View.Label.text = Model.ToString();
    }

    protected override void OnAfterModelChanged()
    {
        if (View != null) View.Label.text = Model.ToString();
    }

    private void OnReset() => SetModel(0);
}

var root = new Widget.Root(lifetime, injector);
var score = root.AddWidget(new ScoreWidget());
score.SetView(view);
score.SetModel(42);
```

```csharp
// 2.0
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
    [Inject] private ILog _log;

    protected override void OnViewAdded()
    {
        var view = View;
        view.ResetClicked.AddListener(OnReset);
        ViewLifetime.AddAction(() => view.ResetClicked.RemoveListener(OnReset));
    }

    protected override void OnRefresh() => View.Label.text = Model.ToString();

    private void OnReset() => SetModel(0);
}

public static class ScoreScreen
{
    public static void Open(Lifetime lifetime, Context context, ScoreView view)
    {
        var root = new Presenter.Root(lifetime, new ContextPresenterFactory(context));
        var score = root.AddPresenter(new ScorePresenter()).CloseWith(view.Lifetime);
        score.SetModel(42);   // attach, then model, then view: SetView throws before the attach
        score.SetView(view);
    }
}
```

Behaviour that compiles unchanged:

- `OnRefresh` runs on every view attach as well as on every model change, and never without a live view, so the
  `View != null` guards go. `OnViewAfterRemoved` no longer runs on the first attach.
- `SetView` before the presenter is attached throws `InvalidOperationException` (0.6.1 half-applied it); after
  close it does nothing.
- The object-typed `SetView(object)` and `SetModel(object)` throw `ArgumentException` naming the presenter for a
  value of the wrong type, instead of `InvalidCastException`.
- `Close()` closes the whole subtree even when clean-up throws, then rethrows one failure as itself or several as
  one `AggregateException`; in 0.6.1 the first failure stopped the teardown. An exception in `Inject` or
  `OnInitialize` undoes the attach instead of leaving the child in `Children`.
- A destroyed Unity view no longer counts as live: `Refresh` skips it.

### `Logger` → `ILog`

**Affects you if** you used the 0.6.1 logging types. `Logger` collided with `UnityEngine.Logger`; the family is
renamed, and the single-letter write methods are named.

| 0.6.1 | 2.0 |
| --- | --- |
| `Logger` (interface) | `ILog` |
| `LoggerGlobal` | `LogRoot` (`sealed`) |
| `ILoggerProvider` (in `OpenUGD.Core.Loggers`) | `ILogSink` |
| `LoggerFlag` | `LogFlags` |
| `UnityLoggerProvider`, `UseUnityLogger(lifetime)` | `UnityLogSink`, `UseUnityConsole(lifetime)` |
| `V`, `I`, `W`, `E`, `D`, `F` (`dynamic`) | `Verbose`, `Info`, `Warn`, `Error`, `Debug`, `Fatal` (`object`) |

```csharp
// 0.6.1
var logger = new LoggerGlobal("Game");
logger.UseUnityLogger(lifetime);
var log = logger.WithTag(typeof(Inventory));
log.I("added");
log.Dispose();   // every later write threw NullReferenceException

// 2.0
var root = new LogRoot("Game");
root.UseUnityConsole(lifetime);
var log = root.WithTag(typeof(Inventory));
log.Info("added");
// ILog is not IDisposable; root.Dispose() detaches the sinks
```

Also: a logger derived from the root has the root as its `Parent` and honours the root's `Flag`; `WithTag(null)`
and `WithTag("")` throw; a derived logger builds its tag path once instead of on every write;
`LogRoot.Dispose()` detaches the sinks instead of throwing `NotImplementedException`. To intercept every record,
implement `ILogSink` rather than overriding `LogRoot`.

### Commands

**Affects you if** you used the command map. A command used to receive its message through an `[Inject]` field that
the mapper wrote into the injector around each execution; now the message is a constructor argument, and nothing
is written into the container.

| 0.6.1 | 2.0 |
| --- | --- |
| `RegisterCommand(Func<Lifetime, ICommand>, bool)` returning `Lifetime` | `RegisterCommand(Type, bool)`, `RegisterCommand(Func<object, Lifetime, ICommand>, bool)`, `RegisterCommand<T>()`, `Map<TMessage, TCommand>()`, `Map<TMessage>(factory)`, all returning `Lifetime.Definition` |
| `IContextSetup.AddCommandMap()` | `ServiceCollection.AddCommandMap()` |
| `CommandMap(Lifetime, IInjector)`, `CommandMapper(Lifetime, Type, IInjector)` | `CommandMap(Lifetime, Context)`, `CommandMapper(Lifetime, Type, Context)` |
| the registration's `Lifetime` offered to the command | the execution's `Lifetime` (ends when `Execute` returns); the registration is the `Lifetime.Definition` argument |
| `ICommandMapperRemove` (implemented by nothing) | implemented by `CommandMapper` |

<!-- upm-tools: no-compile (the corelib 0.6.1 API, removed in 2.0) -->
```csharp
// 0.6.1
public class BuyCommand : ICommand
{
    [Inject] private BuyMessage _message;   // written into the injector for each Tell
    [Inject] private IShop _shop;

    public void Execute() => _shop.Buy(_message.Item);
}

map.Map<BuyMessage>().RegisterCommand(lifetime => new BuyCommand());
```

```csharp
// 2.0
using OpenUGD;
using OpenUGD.Commands;

public sealed class BuyMessage : IMessage
{
    public string Item;
}

public interface IShop
{
    void Buy(string item);
}

public sealed class BuyCommand : ICommand
{
    private readonly BuyMessage _message;

    [Inject] private IShop _shop;   // services may stay [Inject] members; the message may not

    public BuyCommand(BuyMessage message) => _message = message;

    public void Execute() => _shop.Buy(_message.Item);
}

public static class Wiring
{
    public static void Map(IMapCommand map) => map.Map<BuyMessage, BuyCommand>();
}
```

Behaviour: a command registered by type is checked at registration, so an unsatisfiable constructor or `[Inject]`
member — including one of the message type — throws `ArgumentException` there instead of failing every `Tell`. A
factory's command is not injected. A throwing command no longer stops the others, and one failure is rethrown as
itself; in 0.6.1 it also left the message registered in the injector. A `oneTime` registration runs once even when
its command tells the same message again.

### The UI services are not in 2.0

**Affects you if** you used the window, HUD or tooltip service. They are not in corelib 2.0.0, and nothing in 2.0
replaces them yet:

- `IUIWindowService`, `UIWindowService` and the rest of `Runtime/Services/UI/Windows`;
- `IHudService`, `UIHudService` and the rest of `Runtime/Services/UI/Hud`;
- `UITooltipService`, `UITooltipWidget` and the rest of `Runtime/Services/UI/Tooltip`;
- what they shared: `Options`, `IUIComponentProvider`, `UIComponentProviderContext`, `ITransformProvider`,
  `TransformProviderComponent`, `IUIContextServiceSetup`;
- `PrefabResourceManager` (`OpenUGD.Utils`), whose only callers were their component providers.

They will be replaced by one presenter host with policies in a separate package, `com.openugd.corelib.ui`, released
as a 2.x. **If you need them now, stay on corelib 0.6.1.** Their last state before removal is kept on the branch
`park/ui-services` of this repository as that package's starting point; it is not a drop-in for 2.0, because it
registers through `BootPhase.Configure`, which `com.openugd.context` 2.0 does not have.

### `ValueSubscriber` and `DisposableHandler` are removed

`ValueSubscriber<T>` (`OpenUGD.Utils`) had no tests and no settled design; a designed `ObservableValue<T>` may
come in a later 2.x. Until then, this replacement over `Signal<T, T>` does the same job. Copy it into your project:

```csharp
using System.Collections.Generic;
using OpenUGD;

/// A value that tells its subscribers when it changes, with the new and the previous value.
public sealed class ObservableValue<T>
{
    private readonly Signal<T, T> _changed;
    private readonly IEqualityComparer<T> _comparer;
    private T _value;

    public ObservableValue(Lifetime lifetime, T initial = default, IEqualityComparer<T> comparer = null)
    {
        _changed = new Signal<T, T>(lifetime);
        _comparer = comparer ?? EqualityComparer<T>.Default;
        _value = initial;
    }

    /// Fires (current, previous) after every change.
    public ISignal<T, T> Changed => _changed;

    public T Value
    {
        get => _value;
        set
        {
            if (_comparer.Equals(_value, value)) return;
            var previous = _value;
            _value = value;
            _changed.Fire(value, previous);
        }
    }

    public void ForceFire() => _changed.Fire(_value, _value);

    public static implicit operator T(ObservableValue<T> value) => value.Value;
}
```

| `ValueSubscriber<T>` | `ObservableValue<T>` |
| --- | --- |
| `Current` | `Value` |
| `Prev` | the second argument of a `Changed` handler |
| `SubscribeOnChange(lifetime, v => ... v.Current ...)` | `Changed.Subscribe(lifetime, (current, previous) => ...)` |
| `ForceFire()`, implicit conversion to `T` | the same |

`DisposableHandler` duplicated `Lifetime.Definition`, which is already an idempotent `IDisposable`:

```csharp
// 0.6.1
IDisposable handle = new DisposableHandler(Release);

// 2.0
var handle = lifetime.DefineNested();   // a Lifetime.Definition: Dispose() terminates it once
handle.Lifetime.AddAction(Release);
```

### Moved

- **`ILocalization` and `ILocalizationChanged`** are in `com.openugd.corelib.widgets`, whose text presenters are
  their only consumer, with their script GUIDs. An implementation needs that package and its namespace.
- **`ContextInstanceComponent`, `ContextFactoryInstancesComponent`, `IContextInstanceProvider`** and the factory's
  inspector are the *Multi Instance* sample, with their 0.6.x GUIDs, namespace and field names, so prefabs bind to
  the imported copies; see that sample's README.

### Also removed

- `OpenUGD.Core.ILifetimeProvider`: use `OpenUGD.ILifetimeProvider` from `com.openugd.context`, which `Context`
  implements. `OpenUGD.Core.ILoggerProvider`, which nothing could reach.
- Seventeen utility files with no caller and no serialized reference: `ArrayUtils`, `RectExtension`,
  `NumberConversionUtils`, `TimeFormat`, `Persist`, `IPersistProvider`, `PersistValueSubscriber`, `ResourceManager`,
  `ResourceBatchLoader`, `KeepReference`, `MethodInvoker`, `MethodAttributeUtil`, `FitOrthographicComponent`,
  `FillOrthographicComponent`, `SpriteRendererFillOrthographicComponent`, `IgnoreOnPointEnterInputModule`,
  `IgnoreOnPointerEnter`. Copy what you used from 0.6.1.
- The `com.unity.ugui` dependency. A project that uses uGUI keeps it through its own manifest or through
  `com.openugd.corelib.widgets`.

## Versioning

The OpenUGD packages share a major version and have independent minor and patch versions. Each 2.x package works
with the 2.x versions of its dependencies at or above the minimums declared in its `package.json`; for this package
that is `com.openugd.lifetime`, `com.openugd.signal` and `com.openugd.context` 2.0.0. The changes in each version
are in [CHANGELOG.md](CHANGELOG.md).

## Licence

Apache-2.0 — see [LICENSE.md](LICENSE.md).
