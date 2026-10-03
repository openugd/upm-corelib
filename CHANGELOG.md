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

- `ContextBehaviour` — the replacement for `ContextFactoryComponent`. Same six signals, same
  `DontDestroyOnLoad`, same `[ContextMenu("Rebuild")]`, but the boot is now `Task Startup`: owned,
  awaitable and observable. A failure reaches `OnStartFailed` *and* faults `Startup` instead of
  vanishing into a discarded task, and an integration test can `await behaviour.Startup` and see the
  real exception. `Context` is `null` until the boot completes. Implements `ICoroutineProvider`.
- `ContextBehaviourEditor` — shows `Startup.Status`, the failure message if it faulted, and whether
  `Context` is built. Before 2.0.0 a context that failed to start looked identical in the inspector to
  one that started fine.
- `Presenter<TView>.OnRefresh()` and `Presenter<TView>.Refresh()` — see *Presenters*.
- `IPresenterFactory` (`com.openugd.presenters`): how a presenter tree has its presenters built and
  injected, in two members — `Presenter Create(Type presenterType)` for code that knows a presenter only by
  its type, and `void Inject(Presenter presenter)`, which the tree calls once for every presenter it
  attaches, before `OnInitialize`, so a presenter created with `new` still gets its injected members.
  `Create`'s parameter carries `[DynamicallyAccessedMembers]` for constructors, so a presenter type written at
  the call site survives IL2CPP stripping.
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
- `CommandMapperExtensions.RegisterCommand<TCommand>()` and `IMapCommand.Map<TMessage, TCommand>()`.
- `ServiceCollection.AddCommandMap()` — `TryAdd`-shaped, so a consumer registration always wins.
- Test suites, one per tested assembly, each referencing only what it tests. Three are Edit Mode suites
  that need no Unity runtime, and each checks that its assembly uses no Unity assembly:
  `com.openugd.presenters.tests` (61 tests over the presenter tree built with a hand-written
  `IPresenterFactory`, the two hooks, `ViewLifetime`, `Attach`, the deleted surface and the presenter open
  sequence, all through public API), `com.openugd.commands.tests` (10 tests over the command mapper and its
  stripping annotations) and `com.openugd.logging.tests` (14 tests over tag paths, the two filters, sink
  fan-out and teardown). `com.openugd.corelib.playmode.tests` is a Play Mode suite for the Unity
  boundary; its 7 tests over `ContextPresenterFactory` need no Unity runtime.

### Changed

- **Breaking: corelib is five assemblies.** The one `com.openugd.corelib` runtime assembly of 0.6.1 is
  split by concern, inside this one package. Each assembly is named as if it were its own package:

  | Assembly | Contents | References | UnityEngine |
  | --- | --- | --- | --- |
  | `com.openugd.corelib` | the Unity boundary: `ContextBehaviour`, `ViewBehaviour`, `SignalMonoBehaviour`, `ICoroutineProvider`/`CoroutineProvider`, `ISynchronizationContext`/`SynchronizationContextWrapper`, and `ContextPresenterFactory`, which adapts presenters to the context | lifetime, signal, context, presenters | yes, no uGUI |
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
  `OnDestroy` — and `Refresh()` is skipped once the lifetime has ended.
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
- **Breaking: `ICommandMapper.RegisterCommand(Func<Lifetime, ICommand>, bool)` is now
  `RegisterCommand(Type, bool)`.** A command is described by its type and built with
  `Context.Instantiate`, which offers the message, the registration's `Lifetime.Definition` and its
  `Lifetime` as constructor arguments and resolves the rest. `Map<Msg>().RegisterCommand(l => new
  BuyCommand())` becomes `Map<Msg>().RegisterCommand<BuyCommand>()`, and a message that arrived on an
  `[Inject]` field becomes a constructor parameter. It has to change: the factory form cannot receive
  the message except through the container mutation that was the defect. The consolation is that
  `ICommandMapperRemove` — declared since 0.6.1 and implemented by nothing, because a factory delegate
  is not a key — is now implemented by `CommandMapper`.

  Because a type replaces the factory, nothing in a player calls a command's constructor except
  `Context.Instantiate`, by reflection, and IL2CPP managed code stripping would remove it: checked with
  Unity's own linker, every command then failed to build at `Tell`. So `RegisterCommand(Type)` (on
  `ICommandMapper` and `CommandMapper`), `RegisterCommand<TCommand>()` and `Map<TMessage, TCommand>()`
  carry `[DynamicallyAccessedMembers]` for constructors, as `com.openugd.context`'s registration points
  do: a command type written at the registration call keeps its constructors at Medium and High
  stripping. Migration: register with a type argument or `typeof`; a `Type` read from data, or passed on by
  a generic method of your own, needs `[Inject]` on the command's constructor.
- **Breaking: `CommandMap(Lifetime, IInjector)` → `CommandMap(Lifetime, Context)`;
  `CommandMapper(Lifetime, Type, IInjector)` → `CommandMapper(Lifetime, Type, Context)`.**
- **Breaking: `Logger`'s six write methods take `object` instead of `dynamic`.** Source-compatible at
  every call site — a `dynamic` parameter is already `object` plus `[Dynamic]` in IL — and at every
  implementation, since `LoggerGlobal` and its nested `LoggerImpl` (now `LogRoot` and `TaggedLog`)
  *already* declared `object`. What it
  removes is the obligation: `dynamic` forced a `Microsoft.CSharp` reference on every implementing
  assembly, and an implementer that actually used the parameter dynamically would have built a call
  site and thrown `ExecutionEngineException` under IL2CPP on device. Nothing here did — but the trap
  was loaded and pointed at the log, which is where a failure is least likely to be noticed.
- **Breaking: `ICoroutineProvider.StartCoroutine` must never return `null`** — an implementation that
  cannot start the coroutine throws. `CoroutineProvider` used to return `null` for an inactive host, so
  the scheduled work simply never ran and nothing said so.
- `CoroutineProvider` checks `isActiveAndEnabled` instead of `gameObject.activeSelf`, rejects a null or
  destroyed host in the constructor, and is `sealed`. The old check read only the host's own flag and
  ignored its parents, so a host under a deactivated parent passed and then threw from inside Unity.
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

- **`OnViewAdded`** runs once each time a view is attached, with `View` already set. Wiring goes here:
  event listeners, subscriptions, anything registered on the presenter's `Lifetime`. It is the "connect
  this view" hook, and it runs exactly as many times as a view is attached.
- **`OnRefresh`** renders the current model into the current view, and must be idempotent. It runs on
  every `SetModel` and on every explicit `Refresh()`, and it runs only while the presenter is live — a
  presenter with no view, or whose `Lifetime` has terminated, is skipped rather than guarded at every
  call site.

Neither hook waits for the other. A presenter with a view and no model renders its empty state; a
presenter with a model and no view renders nothing and renders correctly as soon as a view arrives.
That is what the latch was trying and failing to express.

`OnAfterModelChanged` is gone with `OnReady`, and there is no `[Obsolete]` shim for either. A shim was
written and rejected: it would have had to be called from the overridable `OnRefresh`, so a subclass
that forgot `base.OnRefresh()` would have silently stopped rendering — a worse failure than a compile
error, and invisible until someone looked at the screen.

`ISubscribeNotify`, `Widget.Internal.Ready` and the per-widget notification `Signal` that existed only
to drive `OnReady` are gone with it.

Note that the type names above are the 1.x ones. `Widget` is now `Presenter` throughout — see the
rename entry near the top of this section for the full table.

### Fixed

- **`LogRoot.Dispose()` no longer throws.** It used to throw `NotImplementedException` on purpose: the
  argument was that failing loudly beats implying a teardown that does not exist. That argument stopped
  holding once the container underneath changed. `Context` registers every constructed service that
  implements `IDisposable` for disposal when its lifetime ends, and `ILog` extends `IDisposable` — so a
  root registered with `Add<LogRoot>()` threw *while the context was tearing down*, turning an ordinary
  shutdown into a failure, in the one place a failure is least likely to be noticed. `Dispose` now
  detaches every sink, which is a real teardown, and is safe to call twice.


- `SliderIntWidget` ignored range changes after the first render: it updated `value` on a model change
  but never `minValue` / `maxValue`, because the range was written only in `OnReady`. Merging the two
  duplicated render bodies into one `OnRefresh` fixed it.
- `ImageWidget` set `View.sprite` once and never again. Same cause, same fix.

