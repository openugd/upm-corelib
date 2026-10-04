# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-10-03

Version 2.0.0, the first of the synchronized OpenUGD 2.x family; it follows 0.6.1. Most of it is breaking; the
README's "Upgrading to 2.0" section walks through each change with before and after code.

### Added
- **`ContextBehaviour`**: boots a `com.openugd.context` `Context` from `Awake` (`CreateContextAsync`), exposes the
  boot as the `Startup` task with `OnStarted` and `OnStartFailed`, and republishes the Unity loop as signals.
  Replaces `ContextFactoryComponent`. `ContextBehaviourEditor` shows the boot status and the failure.
- **`ContextBehaviour.PersistAcrossScenes`**, `true` unless overridden: `DontDestroyOnLoad` becomes a choice.
- **`PlaySession.Lifetime`**: the scope of one play session, ended at `Application.quitting` and started afresh
  each session, with or without a domain reload.
- **`LifetimeBehaviour` and `GetLifetime()`** on a GameObject or a component: the scope of a GameObject.
- **`IPresenterFactory`** (`Create`, `Inject`) and **`ContextPresenterFactory`**, its implementation over a
  `Context`; presenters reach the container only through it.
- **`Presenter.Attach(presenter, definition, factory)`**, public, for hosts that own a presenter's scope.
- **`Presenter<TView>.ViewLifetime`**, the scope of the current view, and **`OnRefresh` / `Refresh()`**.
- **`PresenterExtensions.CloseWith(lifetime)`**: close a presenter when a lifetime, typically its view's, ends.
- **Command registration by type or by factory**: `RegisterCommand(Type)`, `RegisterCommand(Func<object, Lifetime,
  ICommand>)`, `RegisterCommand<TCommand>()`, `Map<TMessage, TCommand>()`, `Map<TMessage>(factory)`, each returning
  the registration's `Lifetime.Definition`; `ServiceCollection.AddCommandMap()`.
- **`ILog.IsEnabled(LogFlags)` and `ILog.Tag`.**
- Samples: Bootstrap, Presenters, Commands and Multi Instance.
- Test assemblies `com.openugd.presenters.tests`, `com.openugd.commands.tests` and `com.openugd.logging.tests`
  (Edit Mode) and `com.openugd.corelib.playmode.tests` (Play Mode).

### Changed
- **Five assemblies instead of one**: `com.openugd.corelib`, `com.openugd.presenters`, `com.openugd.commands`,
  `com.openugd.logging`, `com.openugd.logging.unity`, the three middle ones engine-free. Affects you if an
  assembly definition of yours references `com.openugd.corelib`: add the assemblies whose types it uses.
- **Namespaces follow the assemblies**: `OpenUGD.Core.Widgets` → `OpenUGD.Presenters`, `OpenUGD.Core.Loggers` →
  `OpenUGD.Logging`, `OpenUGD.Services.Commands` → `OpenUGD.Commands`.
- **Dependencies** are `com.openugd.lifetime`, `com.openugd.signal` and `com.openugd.context` 2.0.0;
  `com.openugd.dependency.injection` and `com.unity.ugui` are no longer dependencies. Remove
  `com.openugd.dependency.injection`: with both, `[Inject]` fails with CS0433.
- **`Widget` is `Presenter`**: `Widget<TView>`, `Widget<TView, TModel>`, `WidgetView` → `ViewBehaviour`,
  `AddWidget` → `AddPresenter`, `IWidgetWith*` → `IPresenterWith*`, `WidgetExtensions` → `PresenterExtensions`.
- **`OnReady` and `OnAfterModelChanged` are replaced by `OnViewAdded` (wire) and `OnRefresh` (render)**. `OnRefresh`
  runs on every view attach and model change, only while live. Affects every widget.
- **`Widget.Root(Lifetime, IInjector)` → `Presenter.Root(Lifetime, IPresenterFactory)`**; a presenter no longer
  implements `IResolve`/`IInject`: take services as `[Inject]` members.
- **`SetView` throws before the presenter is attached** and does nothing after it has closed.
- **`SetView(object)` and `SetModel(object)` throw `ArgumentException`** naming the presenter for a wrong type,
  instead of `InvalidCastException`.
- **`Close()` closes the whole subtree when clean-up throws**, then rethrows one failure as itself and several as
  one `AggregateException`. Affects you if clean-up can throw.
- **`Presenter.Children` is a live `IReadOnlyList<Presenter>`**, not a new array; `GetChildren(list)` copies.
- `GetChildren<T>(recursively: true)` walks depth-first.
- **Logging is renamed**: `Logger` → `ILog`, `LoggerGlobal` → `LogRoot` (now `sealed`), `ILoggerProvider` →
  `ILogSink`, `LoggerFlag` → `LogFlags`, `UnityLoggerProvider` → `UnityLogSink`, `UseUnityLogger` →
  `UseUnityConsole`.
- **The write methods are named** and take `object`: `V`/`I`/`W`/`E`/`D`/`F(dynamic)` → `Verbose`, `Info`, `Warn`,
  `Error`, `Debug`, `Fatal`.
- **`ILog` is not `IDisposable`.** `LogRoot.Dispose()` detaches the sinks. A derived logger's `Parent` is the root,
  and its `LogFlag` includes the root's `Flag`.
- **Logging arguments are checked**: `WithTag(null)`, `WithTag("")` and `Subscribe(null)` throw; `UseUnityConsole`
  throws for a `null` argument and attaches nothing on an ended lifetime.
- **A command takes its message as a constructor argument.** The mapper writes nothing into the container, so an
  `[Inject]` member of the message type is refused at registration.
- **A command's `Lifetime` argument is the execution's**, ended when `Execute` returns; the registration is the
  `Lifetime.Definition` argument.
- **Command types are checked at registration**: constructor and `[Inject]` members, with the constructor chosen
  as `Context.Instantiate` chooses one. Unsatisfiable ones throw `ArgumentException` there instead of on `Tell`.
- **Command failures**: every command and listener runs; then one failure is rethrown as itself and several as one
  `AggregateException`. Affects you if you caught around `Tell`.
- **A factory-built command is not injected**; hand it what it needs.
- **`CommandMap` and `CommandMapper` take a `Context`** instead of an `IInjector`; `CommandMapper` implements
  `ICommandMapperRemove`.
- **The Unity messages of `ContextBehaviour`, `ViewBehaviour` and `SignalMonoBehaviour` are `protected virtual`.**
  A subclass that declares one without `override` gets CS0114; override and call `base`.
- **`ViewBehaviour.OnAwake` is removed**: override `Awake` and call `base.Awake()` first. `ViewBehaviour.Lifetime`
  is public, and reading it before `Awake` throws `InvalidOperationException`.
- **Scopes nest in `PlaySession.Lifetime`**, not `Lifetime.Eternal`, for `ContextBehaviour`, `ViewBehaviour` and
  `SignalMonoBehaviour`. When play mode is exited they end at `Application.quitting`, newest first.
- **Reading a `SignalMonoBehaviour` signal before `Awake` throws** `InvalidOperationException`; it returned
  `null`.
- **`ICoroutineProvider.StartCoroutine` never returns `null`.** `CoroutineProvider` (now `sealed`) and
  `ContextBehaviour` throw for a destroyed, disabled or inactive host, and work from `Awake`.
- **`SynchronizationContextWrapper` is `sealed` and rejects a `null` context.**
- Minimum Unity version raised to 6000.0.
- Licence changed from a modified MIT text to Apache-2.0, in `LICENSE.md`. Earlier releases keep their terms.

### Removed
- **The UI services**: the window, HUD and tooltip services, `ITransformProvider`, `TransformProviderComponent`,
  `IUIContextServiceSetup`, and `PrefabResourceManager`. They come back as `com.openugd.corelib.ui`, a later 2.x;
  until then stay on 0.6.1 if you need them. Their code as released is in the `0.6.1` tag, under
  `Runtime/Services/UI` (`PrefabResourceManager` under `Runtime/Utils`).
- **The composition layer**: `ContextStartup`, `IContextSetup`, `IContextServiceSetup`, the service builder, its
  options, resolvers and observers, `UnityDebugServiceObserver`, `OpenUGD.Core.Context`, `IContext`,
  `IInjectorProvider`. `com.openugd.context` replaces it.
- **`Service`, `ContextFactoryComponent`, `ContextFactoryComponent<T>`** and its inspector, with no shim. Implement
  `IAwakeService`/`IInitializeService`; derive from `ContextBehaviour`.
- **`ValueSubscriber<T>` and `DisposableHandler`.** The README gives a replacement over `Signal<T, T>`; use
  `Lifetime.Definition` for the second.
- **`OpenUGD.Core.ILifetimeProvider`** (use `OpenUGD.ILifetimeProvider`) and `OpenUGD.Core.ILoggerProvider`, which
  nothing in the package used.
- **`ILocalization` and `ILocalizationChanged`**, moved to `com.openugd.corelib.widgets` with their GUIDs.
- **`ContextInstanceComponent`, `ContextFactoryInstancesComponent`, `IContextInstanceProvider`** and the factory's
  inspector, moved to the Multi Instance sample with their GUIDs, namespace and field names.
- **`SignalMonoBehaviour.UpdateSignal`, `LateUpdateSignal`, `FixedUpdateSignal` and `AwakeSignal`.** Subscribe to
  `ContextBehaviour.OnUpdate`.
- **The view and model members of the widget interfaces**, `IWidgetWithView<TView>` (use `Presenter<TView>`),
  `Widget.Internal`, `ISubscribeNotify` and `OnViewBeforeRemove`; register view clean-up on `ViewLifetime`.
- **`ICommandMapper.RegisterCommand(Func<Lifetime, ICommand>, bool)`**; see the registrations under *Added*.
- **Seventeen unused utility files**: `ArrayUtils`, `RectExtension`, `NumberConversionUtils`, `TimeFormat`,
  `Persist`, `IPersistProvider`, `PersistValueSubscriber`, `ResourceManager`, `ResourceBatchLoader`, `KeepReference`,
  `MethodInvoker`, `MethodAttributeUtil`, `FitOrthographicComponent`, `FillOrthographicComponent`,
  `SpriteRendererFillOrthographicComponent`, `IgnoreOnPointEnterInputModule`, `IgnoreOnPointerEnter`.
- The obsolete `category` key in `package.json`.

### Fixed
- A command that threw left its message registered in the injector and stopped the commands after it.
- A one-time command that told its own message from `Execute` ran twice.
- Clean-up a command registered on its `Lifetime` piled up on the registration, one more per message.
- A widget whose `Inject` or `OnInitialize` threw stayed in its parent's `Children`, alive; one whose `OnClose`
  threw was not unlinked from its parent.
- `Rebuild` did not boot again when tearing down the old scope threw.
- `StartCoroutine` returned `null` for an inactive host — `CoroutineProvider`, and the factory component through
  Unity's own method — so the coroutine never ran; `CoroutineProvider` checked only the host's own `activeSelf`.
- Scopes rooted in `Lifetime.Eternal` survived into the next play session with domain reload disabled.
- `LoggerGlobal.Dispose()`, now `LogRoot.Dispose()`, threw `NotImplementedException`.
- The sink list was not thread-safe: a write during `Subscribe` or `Unsubscribe` could fail with "Collection was
  modified".
- A derived logger rebuilt its tag path on every write.
- The multi-display components leaked a scope when never activated, lost their instances on scene loads and ran
  `Rebuild` in edit mode; fixed in the Multi Instance sample's copies.

## Earlier versions

Versions before 2.0.0 were released without changelog entries. The tags `0.1.0` to `0.6.1` in this repository mark
most of them; 0.0.1 and 0.2.0, published on OpenUPM, have no tag. The README's "Upgrading to 2.0" section covers
the move from 0.6.1.

[2.0.0]: https://github.com/openugd/upm-corelib/compare/0.6.1...2.0.0
