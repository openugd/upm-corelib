# Changelog
### corelib

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0]

CoreLib is now the Unity boundary for `com.openugd.context` plus the utilities that go with it, and
nothing else. The composition layer it used to own is gone: `com.openugd.context` 2.0.0 does that job,
validates the whole graph before constructing anything, and has its own test suite. Read this whole
section before upgrading — most of it is breaking, and every break is named so you can tell whether it
reaches you. The three headline breaks are the UI services leaving corelib, the first entry under
*Removed*; the split into five assemblies, the first entry under *Changed*; and the rename of `Widget` to
`Presenter`, further down *Changed*.

### Removed

- **Breaking: the UI services are not in corelib 2.0.0.** `Runtime/Services/UI` is removed in full:
  - the window service: `IUIWindowService`, `UIWindowService`, `IUIWindowsProvider`,
    `IUIWindowsRegister`, `UIWindowComponentProvider`, `UIWindowReference`, `UIWindowFactoryInfo`,
    `UIWindowActionType`, `WindowOptions`, `UIWindowServiceExtension`,
    `UIWindowServiceInstallerExtension`, `UIWindowsRegisterExtensions`;
  - the HUD service: `IHudService`, `UIHudService`, `IUIHudProvider`, `IUIHudRegister`,
    `UIHudComponentProvider`, `UIHudReference`, `UIHudFactoryInfo`, `UIHudActionType`, `HudOptions`,
    `UIHudServiceExtension`;
  - the tooltip service: `IUITooltip`, `IUITooltipProvider`, `IUITooltipRegister`,
    `UITooltipComponent`, `UITooltipComponentProvider`, `UITooltipMap`, `UITooltipPresenter`,
    `UITooltipReference`, `UITooltipService`, `UITooltipServiceExtensions`;
  - what the three shared: `Options`, `OptionsKeys`, `IUIComponentProvider`,
    `UIComponentProviderContext`, `ITransformProvider`, `TransformProviderComponent`,
    `TransformProviderExtensions`, `UILayer`, `UILayers`, `UILayerNotBoundException`;
  - `PrefabResourceManager` with its nested `ResourceResult` and `ResourceLoadProgress`
    (`OpenUGD.Utils`), whose only callers were the three component providers;
  - `IUIContextServiceSetup` and `IUIContextServiceBuilder` from 0.6.1.

  They are to be replaced by one presenter host with policies, in a separate package,
  `com.openugd.corelib.ui`, released as a 2.x once it passes its acceptance test. Shipping the old
  services in 2.0.0 would have meant carrying three copy-paste services through every 2.x next to their
  replacement, because a type released in 2.0.0 can only be removed in 3.0. The code is kept, exactly as
  it was when it left this package, on the branch `park/ui-services` of this repository, as the starting
  point of that package. It is not a drop-in: it registers through `BootPhase.Configure`, which
  `com.openugd.context` 2.0.0 does not have. `TransformProviderComponent` is planned to move there with
  its `.meta` unchanged, so scene references to it survive the move. Until that package ships, corelib
  2.0.0 has no window, HUD or tooltip service; a project that needs them now stays on 0.6.1. Their 24
  tests are on the same branch.

- **Breaking: `com.openugd.dependency.injection` is no longer a dependency.** It is removed from
  `package.json` and from the runtime asmdef. Nothing in corelib references `IInjector`, `IInject`,
  `IResolve`, `OpenUGD.Resolvers` or `OpenUGD.Descriptions` any more. If your code obtained an injector
  *through* a corelib type, take a `Context` instead; a presenter, which implemented `IResolve` and `IInject`
  in 0.6.1, takes an `[Inject]` member instead (see *Changed*).
- **Breaking: the whole composition layer.** `Runtime/Core/ContextBuilder/` in its entirety —
  `IContextSetup`, `IContextServiceSetup`, `IContextBuilder`/`IContextServiceBuilder`,
  `ContextServiceBuilder`, `ContextServiceRegisterImpl`, `IServiceRegister`, `IServiceResolver`,
  `ServiceResolver`, `ServiceResolverExtensions`, `ContextServiceBuilderOptions`,
  `ContextServiceInitializationStrategy`, `ContextStartup`, `IServicesObserverRegister`,
  `UnityDebugServiceObserver` — plus `OpenUGD.Core.Context`, `OpenUGD.Core.IContext`,
  `IInjectorProvider` and the editor drawer `UnityDebugServiceObserverEditor`. Replaced by
  `OpenUGD.Context` / `OpenUGD.ContextBuilder` / `OpenUGD.ServiceCollection`. This subsumes the
  previously noted removal of the public `ContextServiceRegisterImpl(IInjector)` constructor, which
  never assigned `_options` and would have thrown `NullReferenceException` on every path that read
  `_options.InitializationStrategy`; it had zero call sites and existed only as a trap.
- **Breaking: `OpenUGD.Core.ILoggerProvider`.** It collided by name with
  `OpenUGD.Core.Loggers.ILoggerProvider` and was already unreachable — both `UnityLoggerProvider` and
  `LoggerGlobal` bind to the `Loggers` one, because that is the namespace they lived in. That
  namespace is now `OpenUGD.Logging` — see *Changed*.
- **Breaking: `OpenUGD.Core.ILifetimeProvider`.** `OpenUGD.ILifetimeProvider`, from the context
  package, replaces it. `Context` already implements it.
- **Breaking: `Service`, `ContextFactoryComponent` and `ContextFactoryComponent<T>` are removed, with no
  shim,** and so is the inspector `ContextFactoryComponentEditor`. Earlier 2.0.0 work kept the three as
  `[Obsolete]` shims "removed in the next release", a promise 2.x could not keep: a type released in
  2.0.0 can only be removed in 3.0. The shims themselves were never released, so removing them breaks no
  published version. Code written against the 0.6.x types is ported once, when it moves to 2.0:
  - `Service` becomes a plain class that implements `IAwakeService` and/or `IInitializeService` from
    `com.openugd.context`. `OnAwake()` and `OnInitialize()` become `AwakeAsync(CancellationToken)` and
    `InitializeAsync(CancellationToken)`. What `Resolve<T>()` returned, the `Lifetime` and the `Context`
    become constructor parameters; the container supplies `Lifetime` and `Context` itself. `Logger`
    becomes an injected `ILog`, tagged with `log.WithTag(GetType())`. `IResolve` is not implemented by
    anything any more; ask the `Context`. The state machine (`ServiceState`, `State` and the public
    `Service.Internal`) has no replacement: the container runs the phases itself, so there is no
    ordering left to validate. Its two `async void` completion checks are gone with it; an exception
    from either could not be caught by anything and took the process down.
  - A `ContextFactoryComponent` or `ContextFactoryComponent<T>` subclass derives from
    `ContextBehaviour` instead and overrides `CreateContextAsync`, returning
    `builder.BuildAsync(cancellationToken)`. Scene references survive the port, because a scene
    serializes the `MonoScript` GUID of the concrete subclass, never of an abstract base.
- **Breaking: `Widget.OnReady`, `Widget.Internal.Ready`, `ISubscribeNotify`,
  `IWidgetWithModel.ModelChanged`, `IWidgetWithView<TView>`, `IWidgetWithView.View`,
  `IWidgetWithView.OnViewAdded` / `.OnViewBeforeRemove` / `.OnViewAfterRemoved`,
  `IWidgetWithModel.Model` / `.OnBeforeModelChange` / `.OnAfterModelChanged`, and
  `Widget<TView>.OnViewBeforeRemove`.** See *Presenters* below. The four view/model interfaces go from 14
  members to 4.
- **Breaking: `Widget<TView, TModel>.OnAfterModelChanged`, with no shim.** Move the body to
  `OnRefresh`, which now runs on a view attach as well as on a model change and always with a live
  view — so the hand-written `View != null` guard goes away too. A shim was considered and rejected: it
  would have to be invoked from an overridable `OnRefresh`, so any subclass overriding `OnRefresh`
  without calling `base` would silently stop rendering.
- **Breaking: `SignalMonoBehaviour.UpdateSignal`, `.LateUpdateSignal`, `.FixedUpdateSignal` and
  `.AwakeSignal`.** Unity dispatches a per-frame message to every component that merely *defines*
  `Update`, subscribers or not, and the 0.6.x UI services attached one of these to every window, HUD
  element and tooltip — three engine dispatches per view per frame for nothing. `AwakeSignal` could
  never reach a subscriber: `AddComponent` runs `Awake` before it returns the reference you would
  subscribe through.
- **Breaking: 17 utility files.** `ArrayUtils`, `RectExtension`, `NumberConversionUtils`, `TimeFormat`,
  `Persist`, `IPersistProvider`, `PersistValueSubscriber`, `ResourceManager`, `ResourceBatchLoader`,
  `KeepReference`, `MethodInvoker`, `MethodAttributeUtil`, `FitOrthographicComponent`,
  `FillOrthographicComponent`, `SpriteRendererFillOrthographicComponent`,
  `IgnoreOnPointEnterInputModule`, `IgnoreOnPointerEnter`. Verified by type name, by extension-method
  name and by `.meta` GUID against every scene, prefab, asset and controller in the repository: none has
  a caller or a serialized reference. `MethodInvoker`/`MethodAttributeUtil` were also the only
  reflective-invocation code in the package.

### Moved

- **Breaking: `ILocalization` and `ILocalizationChanged` leave corelib for `com.openugd.corelib.widgets`,**
  whose text presenters are their only consumer. They keep their script GUIDs
  (`811ccaf8d3814cd79d54b3644f8bcee2`, `05cafe5169154497a5743cc363799b14`). Migration: an implementation
  of either interface needs `com.openugd.corelib.widgets` and its namespace; see that package's CHANGELOG.
- **Breaking: the multi-display components move out of the package into the *Multi Instance* sample.**
  `ContextInstanceComponent`, `ContextFactoryInstancesComponent`, `IContextInstanceProvider` and the
  inspector `ContextFactoryInstancesComponentEditor` are no longer compiled into corelib. Import the
  sample from the Package Manager to get them back as project code. The copies keep the 0.6.x script
  GUIDs (`bde234200e4d407696b34ff23b9e6ea2`, `cdba43c5badf44d28b2cfdf25807862b`,
  `1fc09025e03547a7b692210cd488a0e6`, `bbe59184e52143a9953989764296a74b`), the namespace `OpenUGD.Core`
  and the serialized field names. Prefabs and scenes that carried the components therefore bind to the
  copies with no "Missing script", and code that used them compiles unchanged once its assembly can see
  the sample's `com.openugd.corelib.samples.multiinstance`. The copies fix three defects (audit CC-21,
  UH-8):
  - `ContextInstanceComponent` creates its scope in `Awake`, not in a field initializer. An instance that
    was never activated, and so never received `OnDestroy`, used to leave a `Lifetime` on
    `Lifetime.Eternal` for the rest of the process. `Subscribe` before `Awake` now throws
    `InvalidOperationException`.
  - The factory marks every instance `DontDestroyOnLoad`, as it does itself, and destroys its instances
    when it is destroyed. A scene load used to destroy the instances under a factory that survived and
    went on tracking dead components.
  - `Rebuild` is play mode only, and the inspector's Rebuild button is disabled outside it. The button
    used to call Unity's `Destroy` in edit mode and create the clones into the open scene.

  The `EventSystem` and `AudioListener` halves are compiled only when the project has `com.unity.ugui`
  and the built-in Audio module, through `versionDefines` in the sample's assembly definition; the
  package never declared the Audio module it used. The sample also adds `InstanceContextExample`, a
  `ContextBehaviour` that registers the instance and copies `TargetDisplay` to the instance's cameras,
  which is the 2.0 form of the context every consumer wrote by hand.
- **Breaking: `ValueSubscriber<T>` and `DisposableHandler` are removed, not moved.** Neither had a
  consumer, tests or a settled design. `ValueSubscriber` returns in a later 2.x as a designed
  `ObservableValue<T>`; MIGRATION-2.0 gives a ~25-line replacement over `Signal<T, T>`. `DisposableHandler`
  duplicated `Lifetime.Definition`, which is already an idempotent `IDisposable`.

### Added

- `ContextBehaviour` — the replacement for `ContextFactoryComponent`. Same six signals, the same
  `DontDestroyOnLoad` by default (now a choice, see `PersistAcrossScenes` below), same
  `[ContextMenu("Rebuild")]`, but the boot is now `Task Startup`: owned,
  awaitable and observable. A failure reaches `OnStartFailed` *and* faults `Startup` instead of
  vanishing into a discarded task, and an integration test can `await behaviour.Startup` and see the
  real exception. `Context` is `null` until the boot completes. Implements `ICoroutineProvider`.
- `PlaySession` (`OpenUGD.Core`): `PlaySession.Lifetime` is the scope of one play session, nested in
  `Lifetime.Eternal`. It is started at `RuntimeInitializeLoadType.SubsystemRegistration` — when a player
  starts and each time the editor enters play mode, domain reload or not — and ended at
  `Application.quitting`, which Unity raises when a player quits and when play mode is exited, before it
  destroys the scene; entering edit mode ends it as a backstop. Between sessions it returns the ended
  lifetime, so a scope created during shutdown is born terminated; in edit mode it is a session of its own
  that ends when play mode starts. See *Fixed* for why.
- `LifetimeBehaviour` and `gameObject.GetLifetime()` / `component.GetLifetime()` (`OpenUGD.Core`): the scope of
  a GameObject, created in `Awake`, nested in `PlaySession.Lifetime` and ended in `OnDestroy` — the one tested
  GameObject-lifetime adapter, in place of the hand-written copies in samples and projects (audit LS-14).
  `GetLifetime` adds the sealed, one-per-GameObject component the first time and returns the same scope after
  that; on a GameObject that has never been active, or outside play mode, it throws
  `InvalidOperationException` and adds nothing, because Unity would never tell that component the object was
  destroyed. `ViewBehaviour`, `SignalMonoBehaviour` and `ContextBehaviour` follow the same rules.
- `ContextBehaviourEditor` — shows `Startup.Status`, the failure message if it faulted, and whether
  `Context` is built. Before 2.0.0 a context that failed to start looked identical in the inspector to
  one that started fine.
- `Presenter<TView>.OnRefresh()` and `Presenter<TView>.Refresh()` — see *Presenters*.
- `ContextBehaviour.PersistAcrossScenes` — `protected virtual bool`, `true` unless overridden: whether
  `Awake` marks the GameObject `DontDestroyOnLoad`. In 0.6.1 that was unconditional, so a context that
  belonged to one scene needed a workaround (audit CC-7). It is applied only to a root object, the only kind
  Unity keeps across scene loads; on a child the behaviour logs a warning saying so instead of calling
  `DontDestroyOnLoad`, which Unity would have ignored with a warning of its own.
- `PresenterExtensions.CloseWith(presenter, lifetime)` closes a presenter when a lifetime ends, typically
  its view's: `presenter.CloseWith(view.Lifetime)`. The binding is undone when either side ends first.
- `IPresenterFactory` (`com.openugd.presenters`): how a presenter tree has its presenters built and
  injected, in two members — `Presenter Create(Type presenterType)` for code that knows a presenter only by
  its type, and `void Inject(Presenter presenter)`, which the tree calls once for every presenter it
  attaches, before `OnInitialize`, so a presenter created with `new` still gets its injected members.
  A presenter from `Create` is injected when it is attached, like any other, so `Create` need only
  construct it. `Create`'s parameter carries `[DynamicallyAccessedMembers]` for constructors, so a presenter type written at
  the call site survives IL2CPP stripping. Checked with Unity's linker on 6000.0.41f1 and 6000.3.3f1 at Medium
  and High: through `ContextPresenterFactory`, the constructor is kept and an `[Inject]` member is filled, with
  no linker warning; without the annotation, the constructor is stripped and the linker warns IL2067.
- `ContextPresenterFactory` (`com.openugd.corelib`, namespace `OpenUGD.Presenters`): the
  `IPresenterFactory` over `OpenUGD.Context` — `Context.Instantiate` and `Context.Inject`. `[Inject]` and
  `[Inject(Optional = true)]` members of presenters attached under a tree rooted with it are filled in
  exactly as before. It can also be registered:
  `builder.Services.Add<ContextPresenterFactory>().As<IPresenterFactory>()`.
- `Presenter.Attach(Presenter presenter, Lifetime.Definition definition, IPresenterFactory factory)`: the
  public attach. It replaces the internal `Presenter.Internal.Initialize`, so a presenter-opening service
  in another assembly can root a presenter on a scope of its own and drive the
  `Attach` → `SetModel` → `SetView` open sequence with no `InternalsVisibleTo`. The presenter takes the
  definition over: `Close` terminates it, and terminating it closes the presenter.
- `Presenter<TView>.ViewLifetime`: one scope per attached view, nested in the presenter's `Lifetime`,
  defined just before `OnViewAdded` and terminated just before that view is detached or replaced (with
  `View` still set), and when the presenter closes. Wire the view's listeners in `OnViewAdded` and register
  their removal on it. Registered on the presenter's `Lifetime` instead, as 0.6.1 code did, a listener
  outlives a replaced view and a re-attached view is subscribed twice (audit WG-4, CC-10).
- `PresenterExtensions.GetChildren(...)` — the two `Presenter.GetChildren` instance methods became
  extension methods. Call sites are unchanged as long as `OpenUGD.Presenters` is imported.
- `CommandMapperExtensions.RegisterCommand<TCommand>()`, `IMapCommand.Map<TMessage, TCommand>()` and
  `IMapCommand.Map<TMessage>(Func<TMessage, Lifetime, ICommand> factory)`.
- `ILog.IsEnabled(LogFlags)`, to skip building a message that would be dropped, and `ILog.Tag`, the full
  dotted path a logger's records carry.
- `ServiceCollection.AddCommandMap()` — `TryAdd`-shaped, so a consumer registration always wins.
- Test suites, one per tested assembly, each referencing only what it tests. Three are Edit Mode suites
  that need no Unity runtime, and each checks that its assembly uses no Unity assembly:
  `com.openugd.presenters.tests` (84 tests over the presenter tree built with a hand-written
  `IPresenterFactory`, the two hooks, `ViewLifetime`, `Attach` and a failed attach, `CloseWith`, the guards,
  the deleted surface and the presenter open sequence, all through public API),
  `com.openugd.commands.tests` (26 tests over the command mapper — registration, its check and its undoing,
  factories, the execution's lifetime, exact-type routing — and its stripping annotations) and
  `com.openugd.logging.tests` (29 tests over tag paths, the filters, `IsEnabled`, sink fan-out, writes and
  subscriptions from other threads and from inside a sink, teardown, and `UseUnityConsole`'s arguments).
  `com.openugd.corelib.playmode.tests` is a Play Mode suite for the Unity boundary: 30 tests need no Unity
  runtime (`ContextPresenterFactory`, the shape of the Unity message methods, which code implements
  `ICoroutineProvider` and how it checks the host, the wrapper that keeps a handle for a coroutine that ends at
  once, `PlaySession`, including that nothing else in the assembly roots a scope in `Lifetime.Eternal`, and
  that no component property creates a scope), and 34 marked `RequiresUnity` cover `ViewBehaviour`'s scope and
  `CloseWith`, a destroyed view skipping `Refresh`, `PersistAcrossScenes` on a root and on a child, a boot
  cancelled by destruction, a failing `OnStarted` and a failing `Rebuild` teardown, overrides that call `base`,
  coroutines through `ICoroutineProvider` (including from the boot in `Awake`, and one that ends at once),
  component scopes and `OnQuit` at the end of the play session, `GetLifetime` and `LifetimeBehaviour`, and
  `SignalMonoBehaviour` on a GameObject that is never activated.

### Changed

- **Breaking: corelib is five assemblies.** The one `com.openugd.corelib` runtime assembly of 0.6.1 is
  split by concern, inside this one package. Each assembly is named as if it were its own package:

  | Assembly | Contents | References | UnityEngine |
  | --- | --- | --- | --- |
  | `com.openugd.corelib` | the Unity boundary: `ContextBehaviour`, `ViewBehaviour`, `SignalMonoBehaviour`, `PlaySession`, `ICoroutineProvider`/`CoroutineProvider`, `ISynchronizationContext`/`SynchronizationContextWrapper`, and `ContextPresenterFactory`, which adapts presenters to the context | lifetime, signal, context, presenters | yes, no uGUI |
  | `com.openugd.presenters` | `Presenter` and its interfaces, `IPresenterFactory`, `PresenterExtensions` | lifetime | no |
  | `com.openugd.commands` | the command map, mappers and `AddCommandMap` | lifetime, context | no |
  | `com.openugd.logging` | `ILog`, `ILogSink`, `LogFlags`, `LogRoot` | nothing | no |
  | `com.openugd.logging.unity` | `UnityLogSink`, `UseUnityConsole` | logging, lifetime | yes |

  "No" means `noEngineReferences: true`: the compiler, not a comment, keeps UnityEngine out. All five are
  auto-referenced, so scripts in `Assembly-CSharp` need no change beyond the namespaces below. Migration
  for an assembly definition of your own that referenced `com.openugd.corelib`: add the assembly names of
  the types it uses — `com.openugd.presenters` for presenters, `com.openugd.commands` for commands,
  `com.openugd.logging` (and `com.openugd.logging.unity` for the console sink) for logging — and keep
  `com.openugd.corelib` only if it uses the Unity boundary types.
- **Breaking: namespaces follow the assemblies.** Migration: change the `using` lines.

  | 0.6.1 | 2.0.0 |
  | --- | --- |
  | `OpenUGD.Core.Loggers` | `OpenUGD.Logging` (`UnityLogSink` included, though it is in its own assembly) |
  | `OpenUGD.Core.Widgets` | `OpenUGD.Presenters` (`ViewBehaviour` included, though it is in `com.openugd.corelib`) |
  | `OpenUGD.Services.Commands` (`CommandMapExtensions`) | `OpenUGD.Commands`, with the rest of the command types |

  The Unity boundary keeps its 0.6.1 namespaces: `OpenUGD.Core` (`ContextBehaviour`), `OpenUGD.Utils`
  (coroutines, `ISynchronizationContext`) and `OpenUGD.Utils.Components` (`SignalMonoBehaviour`).
- **Breaking: the logging types are renamed, and the namespace `OpenUGD.Core.Loggers` becomes
  `OpenUGD.Logging`.** `Logger` collided with `UnityEngine.Logger`, so every file with both
  `using UnityEngine;` and `using OpenUGD.Core.Loggers;` failed to compile with CS0104 — this
  repository's own `ProjectContext.cs` carried a `using Logger = OpenUGD.Core.Loggers.Logger;` alias to
  work around it. `Logger` was also an *interface* declared without the `I` prefix.

  | was | is | why |
  | --- | --- | --- |
  | `Logger` (interface) | `ILog` | collides with `UnityEngine.Logger`; `ILogger` is taken by Unity too |
  | `ILoggerProvider` | `ILogSink` | it provides nothing — it receives records; also collides with `Microsoft.Extensions.Logging` |
  | `LoggerFlag` | `LogFlags` | a `[Flags]` set, not an ordered level |
  | `LoggerGlobal` | `LogRoot` | the root channel that owns the sinks and hands out tagged children |
  | `UnityLoggerProvider` | `UnityLogSink` | matches `ILogSink` |
  | `UseUnityLogger(...)` | `UseUnityConsole(...)` | says where the records actually go |

- **Breaking: the write methods are named, and the single-letter ones are gone** (audit CC-13, G-9). There
  are no aliases: one way to write each level. Migration, call by call:

  | 0.6.1 | 2.0.0 |
  | --- | --- |
  | `V(message)` | `Verbose(message)` |
  | `I(message)` | `Info(message)` |
  | `W(message)` | `Warn(message)` |
  | `E(message)` | `Error(message)` |
  | `D(message)` | `Debug(message)` |
  | `F(message)` | `Fatal(message)` |

- **Breaking: `ILog` no longer extends `IDisposable`** (audit CC-11). A derived logger held nothing to
  release, and disposing one detached it from its parent, after which every write on it threw
  `NullReferenceException` — which is what a container did to a logger it built from a factory
  registration, at the end of its lifetime. `LogRoot` implements `IDisposable` itself and keeps `Dispose`,
  which detaches its sinks. Migration: delete `Dispose()` calls on derived loggers; dispose the `LogRoot`
  you created, or let the context that constructed it do so.
- **Breaking: `LogRoot` no longer routes through a hidden inner logger** (audit CC-13). A logger derived from
  the root has the root as its `Parent` (it had the inner logger); its `LogFlag` includes the root's
  `Flag` (it ignored it, although the root still filtered the record); and the root's write methods return
  the root (they returned the inner logger, whose `Flag` silenced the whole tree when a chained call set
  it). Migration: none for ordinary code; code that walked `Parent` to find the root now finds it.
- **Breaking: `LogRoot` is `sealed`, and its write methods are not `virtual`.** An override intercepted only
  the writes made on the root itself, never those of the loggers derived from it. Migration: implement
  `ILogSink` to see every record; pass the tag to the constructor to name a root.
- **Breaking: arguments are validated where they used to fail later.** `WithTag(null)` throws
  `ArgumentNullException` and `WithTag("")` throws `ArgumentException` (they produced a path with a bare dot,
  or a `NullReferenceException` for a `null` type); `Subscribe(null)` throws `ArgumentNullException` (the
  next write threw). Under a root whose tag is empty, a derived logger's path no longer starts with a dot
  (`Inventory`, not `.Inventory`). `LogRoot.Log` drops a record whose flag names no level (it delivered
  it). Migration: pass a real tag and a real sink.


- **Breaking: `Widget` is renamed `Presenter`, and the namespace `OpenUGD.Core.Widgets` is renamed
  `OpenUGD.Presenters`.** The type is handed its view and never creates one — it is checked: all
  20 `SetView` call sites pass a view in, and the 0.6.x UI services that instantiate a prefab then call
  `SetView` on the presenter. Receiving the view rather than building it is what distinguishes a
  presenter (MVP, passive view) from a widget, which in every other UI framework *is* the view. The
  old name also made `Widget<Button, Action>` read as a widget wrapping a widget.
  - `Widget`, `Widget<TView>`, `Widget<TView, TModel>` -> `Presenter`, `Presenter<TView>`,
    `Presenter<TView, TModel>`
  - `Widget.Root` -> `Presenter.Root`; `AddWidget` -> `AddPresenter`
  - `IWidgetWithView` / `IWidgetWithModel` -> `IPresenterWithView` / `IPresenterWithModel`
  - `WidgetView` -> `ViewBehaviour` — it is a view, and `PresenterView` would have implied otherwise
  - `WidgetExtensions` -> `PresenterExtensions`

  There are no `[Obsolete]` forwarding types: at this boundary the base class, the lifecycle hooks and
  the whole DI layer change together, so affected code cannot compile regardless.


- **`Presenter<TView>` puts no constraint on `TView`, and there is no `IWidgetView`.** An interface
  demanding that every view answer for its own liveness was designed and abandoned: `UnityEngine.UI.Button`
  cannot implement an interface of ours, so the constraint cut off every leaf presenter over a Unity
  component — which is the whole of `com.openugd.corelib.widgets`. A `where TView : Component`
  constraint was rejected for the opposite reason: it drags Unity into the layer and makes a plain
  test double impossible. Liveness is a property of the presenter's own scope instead. Whoever creates
  a view ties the presenter's `Lifetime` to that view's destruction — `ViewBehaviour` terminates in
  `OnDestroy` — and `Refresh()` is skipped once the lifetime has ended. As a backstop it is also skipped
  for a destroyed Unity view, which the presenters assembly detects without referencing UnityEngine
  (see *Fixed*).
- **Breaking: the object-typed `IPresenterWithView.SetView(object)` and
  `IPresenterWithModel.SetModel(object)` throw `ArgumentException` for a value of the wrong type**, and
  `SetModel` does so for `null` when the model is a non-nullable value type. The message names the
  presenter, the type it expects and the type it was given (audit CC-9). They threw `InvalidCastException`,
  or `NullReferenceException` for the `null`, naming neither the presenter nor the parameter. Migration:
  catch `ArgumentException` where you caught `InvalidCastException`.
- **Breaking: `Presenter<TView>.SetView` throws `InvalidOperationException` before the presenter is
  attached, and does nothing once it has closed** (audit CC-10). Before attach it used to store the view
  and run `OnViewAdded`, then throw, and every later `Refresh` threw. After close — from the moment the
  presenter's lifetime starts terminating — the view is not stored and no hook runs, so a view that
  finished loading after its presenter closed is harmless; the caller keeps it. Migration: attach before
  `SetView` (`AddPresenter`/`Attach`, then `SetModel`, then `SetView`); release a late view through a
  clean-up registered on the presenter's `Lifetime`, which runs at once on a closed presenter.
- **Breaking: `Widget.Root(Lifetime, IInjector)` is now `Presenter.Root(Lifetime, IPresenterFactory)`.**
  Migration: `new Presenter.Root(lifetime, new ContextPresenterFactory(context))`.
- **Breaking: presenters reference no container.** `com.openugd.presenters` references
  `com.openugd.lifetime` and nothing else from the family; construction and injection go through the
  `IPresenterFactory` a tree is rooted with, and there is no property through which a presenter reaches its
  container (0.6.1's `IResolve`/`IInject` on `Widget` are gone, see *Removed*). Migration: a presenter that
  resolved a service from its tree takes it as an `[Inject]` member — `[Inject(Optional = true)]` if the
  service may be absent — which `ContextPresenterFactory` fills in before `OnInitialize`.
- **Breaking: `Presenter.Close()` and `Dispose()` close every presenter in the subtree even when clean-up
  throws, then report the failures as `Lifetime` 2.0.0 does: one failure as itself, with its stack trace,
  two or more as one `AggregateException`.** In 0.6.1 the first failure stopped the rest of the teardown, so the
  presenters after it never closed. Migration: catch the exception your clean-up throws; expect an
  `AggregateException` only when several fail.
- **Breaking: `Presenter.Children` is `IReadOnlyList<Presenter>` (a live view), not an array (a fresh
  one per call).** Do not hold it across anything that can close a presenter; use
  `PresenterExtensions.GetChildren` for a snapshot.
- `PresenterExtensions.GetChildren<T>(recursively: true)` is now plain depth-first. The old order emitted
  all matching direct children and only then recursed. There was no caller and the order was
  undocumented, so the simpler one is now the documented one.
- **Breaking: `ICommandMapper.RegisterCommand(Func<Lifetime, ICommand>, bool)` is replaced by
  `RegisterCommand(Type, bool)` and `RegisterCommand(Func<object, Lifetime, ICommand>, bool)`, and both
  return the registration's `Lifetime.Definition` instead of its `Lifetime`.** The 0.6.1 factory could not
  receive the message except through the container mutation that was the defect; the new one is handed the
  message and the execution's `Lifetime`. A command registered by type is built with `Context.Instantiate`,
  which offers the message, the registration's `Lifetime.Definition` and the execution's `Lifetime` as
  constructor arguments and resolves the rest. Migration: `Map<Msg>().RegisterCommand(l => new BuyCommand())`
  becomes `Map<Msg>((message, lifetime) => new BuyCommand(message))`, or `Map<Msg, BuyCommand>()` with the
  message, which arrived on an `[Inject]` field, taken as a constructor parameter. Code that stored the
  returned `Lifetime` compiles unchanged — a `Lifetime.Definition` converts to it implicitly — but a call on
  it such as `AddAction` needs `.Lifetime`. `ICommandMapperRemove` — declared since 0.6.1 and implemented by
  nothing, because a factory delegate is not a key — is now implemented by `CommandMapper`.

  Nothing in a player calls the constructor of a command registered by type except `Context.Instantiate`, by
  reflection, and IL2CPP managed code stripping would remove it: checked with Unity's own linker, every
  command then failed to build at `Tell`. So `RegisterCommand(Type)` (on `ICommandMapper` and
  `CommandMapper`), `RegisterCommand<TCommand>()` and `Map<TMessage, TCommand>()` carry
  `[DynamicallyAccessedMembers]` for constructors, as `com.openugd.context`'s registration points do: a
  command type written at the registration call keeps its constructors at Medium and High stripping.
  Migration: register with a type argument or `typeof`; a `Type` read from data, or passed on by a generic
  method of your own, needs `[Inject]` on the command's constructor — or register a factory.
- **Breaking: a command's `Lifetime` constructor argument is the execution's, not the registration's.** It
  ends when `Execute` returns; the registration is the `Lifetime.Definition` argument. See *Fixed*.
- **Breaking: `CommandMap(Lifetime, IInjector)` → `CommandMap(Lifetime, Context)`;
  `CommandMapper(Lifetime, Type, IInjector)` → `CommandMapper(Lifetime, Type, Context)`.**
- **Breaking: `Logger`'s six write methods take `object` instead of `dynamic`** (and are renamed, see
  above). On its own this change was source-compatible at every call site — a `dynamic` parameter is already
  `object` plus `[Dynamic]` in IL — and at every implementation, since `LoggerGlobal` and its nested
  `LoggerImpl` (now `LogRoot` and `TaggedLog`) *already* declared `object`. What it removes is the
  obligation: `dynamic` forced a `Microsoft.CSharp` reference on every implementing
  assembly, and an implementer that actually used the parameter dynamically would have built a call
  site and thrown `ExecutionEngineException` under IL2CPP on device. Nothing here did — but the trap
  was loaded and pointed at the log, which is where a failure is least likely to be noticed.
- **Breaking: the Unity messages of `ContextBehaviour`, `ViewBehaviour` and `SignalMonoBehaviour` are
  `protected virtual`** (audit CC-6, UH-9): `Awake`, `Update`, `FixedUpdate`, `LateUpdate`,
  `OnApplicationFocus`, `OnApplicationPause`, `OnApplicationQuit` and `OnDestroy` on `ContextBehaviour`;
  `Awake` and `OnDestroy` on `ViewBehaviour`; `Awake`, `Start`, `OnEnable`, `OnDisable` and `OnDestroy` on
  `SignalMonoBehaviour`. They were private, so a subclass that declared, say, its own `Update` silently
  replaced the base's — `OnUpdate` stopped firing, or the scope was never ended — because Unity finds the
  method by name. Such a subclass now gets warning CS0114. Migration: declare the method
  `protected override` and call `base` (`base.Awake()` first).
- **Breaking: `ViewBehaviour.OnAwake` is removed.** It existed only because `Awake` was private. Migration:
  override `Awake` and call `base.Awake()` first.
- `ViewBehaviour.Lifetime` is public. It was `protected`, so nothing outside the view could bind to the
  view's destruction, although the type's documentation said that was its purpose (audit CC-5). Reading it
  before `Awake` throws `InvalidOperationException` (it threw `NullReferenceException`).
- **Breaking: `ICoroutineProvider.StartCoroutine` must never return `null`** — an implementation that
  cannot start the coroutine throws. `CoroutineProvider` used to return `null` for an inactive host, so
  the scheduled work simply never ran and nothing said so.
- `CoroutineProvider` checks that the host is enabled and its GameObject `activeInHierarchy` instead of
  `gameObject.activeSelf`, rejects a null or destroyed host in the constructor, and is `sealed`. The old check
  read only the host's own flag and ignored its parents, so a host under a deactivated parent passed and then
  threw from inside Unity. The check is not `isActiveAndEnabled`, which Unity reports `false` until the host's
  `OnEnable`, so it would refuse a coroutine started from `Awake`. The coroutine Unity runs wraps the body you
  pass, so that a body that finishes in its first step still gets a handle; stop it through the provider or with
  `StopAllCoroutines`, not with `MonoBehaviour.StopCoroutine(IEnumerator)`.
- `SynchronizationContextWrapper` rejects a `null` context. `SynchronizationContext.Current` is `null`
  on any thread with no installed context; the null used to be stored and every later `Send`/`Post`
  threw `NullReferenceException` far from the cause.
- `package.json`: version 2.0.0; dependencies are now `com.openugd.lifetime`, `com.openugd.signal` and
  `com.openugd.context`; `com.openugd.dependency.injection` removed; description and keywords rewritten.
- CoreLib no longer uses uGUI. In 0.6.1, `ContextInstanceComponent`, `IContextInstanceProvider`,
  `UITooltipComponent`, `IUITooltip` and `IgnoreOnPointEnterInputModule` compiled against
  `UnityEngine.EventSystems` without the package declaring `com.unity.ugui`. The first two are in the
  *Multi Instance* sample now, the tooltip types left with the UI services, and the input module was
  deleted, so nothing in the package references `UnityEngine.UI` or `UnityEngine.EventSystems` and
  `package.json` does not declare `com.unity.ugui`. A project that uses uGUI itself, or through
  `com.openugd.corelib.widgets`, still gets it from there.
- **Licence changed from MIT to Apache-2.0.** The previous `LICENSE` was a mutated MIT whose copyright
  line had been deleted and whose attribution clause was replaced with the literal text "No
  conditions.", which left it legally ambiguous. It is now the verbatim Apache License 2.0 with an
  explicit copyright holder, the file is named `LICENSE.md`, and `package.json` declares
  `"license": "Apache-2.0"`. Apache-2.0 adds an express patent grant and requires that changes to the
  files be stated; releases made before this version remain under their original terms.
- Minimum supported editor raised to Unity 6000.0 (`"unity": "6000.0"`, `"unityRelease": "0f1"`).
  Unity 2022.3 is not supported. Earlier declared minimums (2020.3 / 2021.3) were never verified.
- Obsolete `category` key removed from `package.json`.

### Presenters

`OnReady` was **two mechanisms that disagreed**. A child added through `AddWidget` got a latch waiting
for both a view and a model; a widget opened by one of the three UI services got a direct
`Widget.Internal.Ready(...)` push that never looked at the model; `Widget.Root` got neither, so its
`OnReady` never fired at all. Which of the three you got depended on how the widget happened to be
created — the exact shape of "keeps running while doing the wrong thing". The widgets showed what was
actually wanted: `SliderFloatWidget` wrote the same render logic twice, in `OnAfterModelChanged` and
again in `OnReady`, because neither hook alone guaranteed both halves; `ImageWidget.OnReady() =>
View.sprite = Model` set the sprite once and never updated it.

It is replaced by two hooks split by responsibility rather than by time:

- **`OnViewAdded`** runs once each time a view is attached, with `View` and a fresh `ViewLifetime` already
  set. Wiring goes here: event listeners and subscriptions on the view, each with its clean-up registered
  on `ViewLifetime`, which ends before that view is detached or replaced. It is the "connect this view"
  hook, and it runs exactly as many times as a view is attached.
- **`OnRefresh`** renders the current model into the current view, and must be idempotent. It runs on
  every `SetModel` and on every explicit `Refresh()`, and it runs only while the presenter is live — a
  presenter with no view, whose `Lifetime` has terminated, or whose Unity view has been destroyed, is
  skipped rather than guarded at every call site.

Neither hook waits for the other. A presenter with a view and no model renders its empty state; a
presenter with a model and no view renders nothing and renders correctly as soon as a view arrives.
That is what the latch was trying and failing to express.

`OnAfterModelChanged` is gone with `OnReady`, and there is no `[Obsolete]` shim for either. A shim was
written and rejected: it would have had to be called from the overridable `OnRefresh`, so a subclass
that forgot `base.OnRefresh()` would have silently stopped rendering — a worse failure than a compile
error, and invisible until someone looked at the screen.

`ISubscribeNotify`, `Widget.Internal.Ready` and the per-widget notification `Signal` that existed only
to drive `OnReady` are gone with it.

Note that the type names above are the 0.6.x ones. `Widget` is now `Presenter` throughout — see the
rename entry near the top of this section for the full table.

### Fixed

- **`Presenter<TView>` no longer renders into a destroyed Unity view** (audit UH-12, WG-20). `IsLive`
  compared the view with `null` by reference, so a destroyed `UnityEngine.Object` — which Unity's own `==`
  reports as `null` — counted as live, and `OnRefresh` wrote into it. `IsLive` now also requires
  `!View.Equals(null)`: `UnityEngine.Object` overrides `Equals` so that a destroyed object equals `null`
  (checked against the `UnityEngine.CoreModule` of 6000.0.41f1 and 6000.3.3f1), which lets the engine-free
  presenters assembly ask Unity without referencing it. Other view types are unaffected: by the .NET
  contract, `Equals(null)` is `false`.
- **`LogRoot.Dispose()` no longer throws.** It used to throw `NotImplementedException` on purpose: the
  argument was that failing loudly beats implying a teardown that does not exist. That argument stopped
  holding once the container underneath changed. `Context` registers every constructed service that
  implements `IDisposable` for disposal when its lifetime ends, and a log root is disposable — so a
  root registered with `Add<LogRoot>()` threw *while the context was tearing down*, turning an ordinary
  shutdown into a failure, in the one place a failure is least likely to be noticed. `Dispose` now
  detaches every sink, which is a real teardown, and is safe to call twice.
- **Logging is safe across threads** (audit CC-12). The sink list was a `List<T>` that writes iterated
  while `Subscribe` and `Unsubscribe` changed it, so a write from a worker thread, or a sink unsubscribing
  itself during a write, failed with "Collection was modified". The list is now copied on every change and
  swapped in whole under a lock; a write reads it without one.
- **A write allocates nothing of its own** (audit UH-17). A derived logger rebuilt its tag path, one string
  per tag segment, for every record that passed its own filter — before the root's filter could still drop it.
  The path is now built once, when the logger is derived, and the one level test includes the root's.
- **Command registrations can be undone, are checked when they are made, and give each execution its own
  scope** (audit CC-22, UH-16).
  - `RegisterCommand`, `Map<TMessage, TCommand>()` and `RegisterCommand<TCommand>()` return the registration's
    `Lifetime.Definition`; terminating or disposing it unregisters that one registration. Before, the only
    public way out was `ICommandMapperRemove`, which removes every registration of a type and is not on
    `ICommandMapper`.
  - A command registered by type is checked at registration: if no constructor that `Context.Instantiate`
    would use can be satisfied from the message, the registration, the execution's lifetime and the context's
    services, `RegisterCommand` throws `ArgumentException` naming the parameter; so it does for a struct, which
    `Context.Instantiate` never builds. Before, every `Tell` failed, from wherever the message was sent.
  - Each execution gets a fresh `Lifetime`, nested in the registration's and terminated when `Execute` returns
    or throws. The `Lifetime` a command took was its registration's, so clean-up a command registered on it —
    a subscription, say — piled up for as long as the registration lived, one more per message.
  - `Map<TMessage>((message, lifetime) => command)` and `RegisterCommand(Func<object, Lifetime, ICommand>)`
    build the command with a factory, so `Tell` uses no reflection. A command registered by type is still built
    by `Context.Instantiate`, by reflection, on every message.
  - `Tell` no longer copies the list of commands, or of listeners, on every message: the lists are replaced
    when they change, and a dispatch walks the one it started with.
  - Routing stays by exact runtime type, now stated in `CommandMap.Tell` and the README as well as on
    `IMessage` and `IMapCommand`.
- **`SignalMonoBehaviour` no longer leaves a scope behind on a GameObject that is never activated** (audit
  UH-10, CC-27). Its signal properties created the scope on first read, so reading one on an inactive
  GameObject that was then destroyed without ever being activated — which Unity does without `OnDestroy` —
  left the scope and every subscriber on it alive. The scope and the signals are now created in `Awake`
  only. **Breaking:** reading a signal before `Awake` throws `InvalidOperationException`; subscribe once the
  GameObject has been active (`AddComponent` on an active GameObject runs `Awake` before it returns).
  `DestroySignal` is raised when the component's scope ends — from `OnDestroy`, or earlier when the play
  session ends — so it still fires once when the application quits; `DisableSignal` is not raised for the
  deactivation that follows the end of the session.
- **Nothing in corelib roots a scope in `Lifetime.Eternal` any more** (audit UH-11, LS-19, CX-21).
  `ContextBehaviour`, `ViewBehaviour` and `SignalMonoBehaviour` nested their scopes in `Lifetime.Eternal`, a
  static field, so with domain reload disabled (*Enter Play Mode Options*) a component Unity never destroyed —
  never activated, or with a subclass that skipped `base.OnDestroy()` — kept its scope, its subscriptions and,
  for a context, its running services into the next play session and into edit mode. They nest in
  `PlaySession.Lifetime` now, which ends with the session; the *Multi Instance* sample does the same. When the
  application quits or play mode is exited, every such scope therefore ends at `Application.quitting`, newest
  first and before Unity destroys the objects, rather than one by one in `OnDestroy`. `ContextBehaviour.OnQuit`
  still fires once on the way out, from `OnApplicationQuit` or from the end of the session, whichever Unity
  does first, while the context is alive. A `ContextBehaviour` that wakes after the session has ended boots
  nothing: `Startup` ends cancelled.
- **`UseUnityConsole` checks its arguments, and its docs use the names of 2.0.0** (audit CC-14). It was
  documented as throwing `NullReferenceException` for a `null` root or lifetime because "neither is checked";
  it throws `ArgumentNullException` now. On a lifetime that has already terminated it attaches nothing,
  instead of subscribing the sink and unsubscribing it again inside the call. The `UnityLogSink` docs still
  called sinks "providers", the 0.6.1 name.
- **A presenter whose attach fails no longer stays in the tree** (audit CC-4). An exception from the
  factory's `Inject` or from `OnInitialize` left the child in its parent's `Children` with a live
  `Lifetime`, so the parent went on closing and counting a presenter that never initialized. `AddPresenter`,
  `Attach` and `Presenter.Root` now terminate the presenter's lifetime and unlink it before rethrowing the
  failure as itself. Whatever `OnInitialize` registered before it threw runs, and so does clean-up the
  caller registered on the definition given to `Attach`; `OnClose` does not, because `OnInitialize` never
  completed. If undoing the attach throws too, both arrive as one `AggregateException`, the attach failure
  first.
- **`ContextBehaviour` honours the `ICoroutineProvider` contract it declares** (audit CC-8). It satisfied the
  interface with the `StartCoroutine` it inherits from `MonoBehaviour`, which returns `null` (and logs an
  error) when the GameObject is inactive, so a coroutine a service scheduled through the interface silently
  never ran. It now implements the interface explicitly, through the same checks as `CoroutineProvider`:
  `StartCoroutine` throws `ArgumentNullException` for a `null` body and `InvalidOperationException` when the
  behaviour is destroyed, inactive or disabled, and never returns `null`; `StopCoroutine` is a no-op on a
  destroyed behaviour. Calling `StartCoroutine` on the class itself still reaches Unity's method.
  - It works from the boot, which `Awake` runs. The checks read `enabled` and `activeInHierarchy`, not
    `isActiveAndEnabled`, which Unity reports `false` until `OnEnable` — so a service that started a coroutine
    while its context booted would otherwise have failed the boot. `CoroutineProvider` had the same flaw.
  - A coroutine that finishes inside `StartCoroutine` — its first step is its last — runs and returns a handle.
    Unity's own method returns `null` for it; the body is wrapped so that it stays alive one more, empty, frame,
    instead of the provider reporting a coroutine that ran as one Unity refused.
- **A failed `OnStarted` no longer leaves its context published and running** (audit CC-28). `Context` was
  set before `OnStarted` ran and stayed set when it threw, so code that checked `Context != null` used a
  context whose start had failed. The context is now disposed and `Context` is `null` again before
  `OnStartFailed` runs and `Startup` faults; if disposing throws too, both failures arrive as one
  `AggregateException`, `OnStarted`'s first. The behaviour's own `Lifetime` and signals stay alive, so
  `Rebuild` can retry.
- **`ContextBehaviour.Rebuild` starts the new boot even when tearing down the old one throws** (audit CC-28).
  The exception escaped before the new boot started, leaving a dead scope, no boot and a disposed context
  still in `Context`. The old scope is terminated whatever happens, the new scope and boot start, and then
  the teardown's failure is rethrown to the caller of `Rebuild`.
- **A presenter whose `OnClose` throws is still unlinked from its parent** (audit CC-4). The unlink came
  after `OnClose` without a `finally`, so the closed presenter stayed in `Children`.


- `SliderIntWidget` ignored range changes after the first render: it updated `value` on a model change
  but never `minValue` / `maxValue`, because the range was written only in `OnReady`. Merging the two
  duplicated render bodies into one `OnRefresh` fixed it.
- `ImageWidget` set `View.sprite` once and never again. Same cause, same fix.

