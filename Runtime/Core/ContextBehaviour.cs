using System;
using System.Threading;
using System.Threading.Tasks;
using OpenUGD.Utils;
using UnityEngine;

namespace OpenUGD.Core
{
    /// <summary>
    /// The Unity boundary of a <see cref="OpenUGD.Context"/>: a <see cref="MonoBehaviour"/> that owns a
    /// scope, republishes the engine's per-frame and application callbacks as signals, and boots one
    /// context asynchronously.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it owns.</b> A <see cref="OpenUGD.Lifetime.Definition"/> nested under
    /// <see cref="OpenUGD.Lifetime.Eternal"/>, six signals scoped to it, and the boot <see cref="Task"/>.
    /// <c>OnDestroy</c> terminates the definition, which terminates the signals and — provided
    /// <see cref="CreateContextAsync"/> built the context under <see cref="Lifetime"/> — the context too.
    /// </para>
    /// <para>
    /// <b>Breaking change in 2.0.0.</b> The boot is no longer a discarded <c>Task</c>. It is
    /// <see cref="Startup"/>: owned, awaitable and observable, so a failure surfaces through
    /// <see cref="OnStartFailed"/> <i>and</i> faults <see cref="Startup"/> instead of vanishing. An
    /// integration test can therefore <c>await behaviour.Startup</c> and see the real exception. The old
    /// synchronous <c>CreateContext()</c> seam survives one release as
    /// <see cref="ContextFactoryComponent"/>.
    /// </para>
    /// <para>
    /// <b>Ordering.</b> Nothing here waits for the boot. The six signals exist from <c>Awake</c> and fire
    /// from the first frame, which is normally before <see cref="Startup"/> completes; <see cref="Context"/>
    /// is <c>null</c> until it does. Subscribe from <see cref="OnStarted"/>, or check
    /// <see cref="Context"/> for <c>null</c>.
    /// </para>
    /// </remarks>
    public abstract class ContextBehaviour : MonoBehaviour, ICoroutineProvider
    {
        private Lifetime.Definition _definition;
        private Signal _onUpdate;
        private Signal _onLateUpdate;
        private Signal _onFixedUpdate;
        private Signal _onQuit;
        private Signal<bool> _onFocus;
        private Signal<bool> _onPause;

        /// <summary>Fires once per frame, from <c>Update</c>.</summary>
        public ISignal OnUpdate => _onUpdate;

        /// <summary>Fires once per frame, from <c>LateUpdate</c>.</summary>
        public ISignal OnLateUpdate => _onLateUpdate;

        /// <summary>Fires from <c>FixedUpdate</c>, at the physics rate.</summary>
        public ISignal OnFixedUpdate => _onFixedUpdate;

        /// <summary>Fires from <c>OnApplicationQuit</c>. Not raised on platforms that kill the
        /// process.</summary>
        public ISignal OnQuit => _onQuit;

        /// <summary>Fires from <c>OnApplicationFocus</c>; the argument is Unity's <c>focus</c>
        /// flag.</summary>
        public ISignal<bool> OnFocus => _onFocus;

        /// <summary>Fires from <c>OnApplicationPause</c>; the argument is Unity's <c>pause</c>
        /// flag.</summary>
        public ISignal<bool> OnPause => _onPause;

        /// <summary>
        /// The scope this behaviour owns. Created in <c>Awake</c>, terminated in <c>OnDestroy</c>, and
        /// replaced by <see cref="Rebuild"/>. Build the context under it so the context dies with the
        /// GameObject.
        /// </summary>
        /// <exception cref="InvalidOperationException">Read before <c>Awake</c> ran.</exception>
        public Lifetime Lifetime =>
            _definition != null
                ? _definition.Lifetime
                : throw new InvalidOperationException(
                    "ContextBehaviour.Lifetime was read before Awake() ran, so there is no scope yet. " +
                    "Unity guarantees Awake on this component runs before any of its own callbacks; if you " +
                    "are reading it from another component's Awake, move that read to Start or to " +
                    "OnStarted.");

        /// <summary>
        /// The context produced by the boot. <c>null</c> until <see cref="Startup"/> completes
        /// successfully, and <c>null</c> again from the moment <see cref="Rebuild"/> is called until the
        /// new boot completes.
        /// </summary>
        public Context Context { get; private set; }

        /// <summary>
        /// The boot. Completes when <see cref="OnStarted"/> has returned; faults with whatever
        /// <see cref="CreateContextAsync"/> threw; ends cancelled if the behaviour was destroyed while the
        /// boot was still in flight. Never <c>null</c> after <c>Awake</c>.
        /// </summary>
        /// <remarks>
        /// A fault is reported through <see cref="OnStartFailed"/> whether or not anyone awaits this task,
        /// and the task's exception is observed internally so a failure cannot resurface later as an
        /// unobserved-exception report. Awaiting it is the supported way to write an integration test:
        /// <c>await behaviour.Startup;</c> rethrows the real failure.
        /// </remarks>
        public Task Startup { get; private set; }

        /// <summary>
        /// Builds the context. Called once from <c>Awake</c> and again on every <see cref="Rebuild"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancelled when <see cref="Lifetime"/> terminates — that is, when the GameObject is destroyed or
        /// <see cref="Rebuild"/> is called. Pass it to <c>BuildAsync</c>.
        /// </param>
        /// <returns>
        /// The built context. Must not be <c>null</c>: a boot that cannot produce a context must throw, so
        /// that the failure is reported rather than turning into a null <see cref="Context"/> that
        /// everything downstream dereferences.
        /// </returns>
        /// <example>
        /// <code>
        /// protected override Task&lt;Context&gt; CreateContextAsync(CancellationToken cancellationToken)
        /// {
        ///     var builder = Context.CreateBuilder(Lifetime);
        ///     builder.Services.AddInstance&lt;ICoroutineProvider&gt;(this);
        ///     builder.Services.Add&lt;PlayerService&gt;();
        ///     return builder.BuildAsync(cancellationToken);
        /// }
        /// </code>
        /// </example>
        protected abstract Task<Context> CreateContextAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Called on the main thread once the context is built, immediately before <see cref="Startup"/>
        /// completes. <see cref="Context"/> is already set. Throwing here faults <see cref="Startup"/> and
        /// reaches <see cref="OnStartFailed"/> like any other boot failure.
        /// </summary>
        /// <param name="context">The context just built; never <c>null</c>.</param>
        protected virtual void OnStarted(Context context)
        {
        }

        /// <summary>
        /// Called when the boot fails, before <see cref="Startup"/> faults. The default logs the exception,
        /// which is the minimum that makes a failed boot visible; override to show a error screen or to
        /// quit. Destroying the GameObject mid-boot is ordinary teardown and does <b>not</b> reach here.
        /// </summary>
        /// <param name="exception">The failure. Never <c>null</c>.</param>
        protected virtual void OnStartFailed(Exception exception) => Debug.LogException(exception, this);

        /// <summary>
        /// Terminates the current scope — and with it the context and every subscription — and boots again.
        /// Play mode only.
        /// </summary>
        /// <remarks>
        /// Exposed as a context-menu item so a running context can be restarted from the inspector. In Edit
        /// mode it does nothing but warn: there is no <c>Awake</c>, no frame loop and no
        /// <c>DontDestroyOnLoad</c> outside play mode, so a "rebuild" there would leave a half-built scope
        /// attached to a scene object and get serialized into the scene.
        /// </remarks>
        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "Rebuild() does nothing in Edit mode: a context needs the play-mode frame loop, and " +
                    "building one here would attach runtime state to a scene object. Enter play mode first.",
                    this);
                return;
            }

            _definition?.Terminate();
            Create();
        }

        private void Awake() => Create();

        // Null-conditional throughout: Unity raises these callbacks on any enabled component, and Rebuild()
        // can be called from a context menu on a component whose Awake has not run (Edit mode is guarded,
        // but a domain reload or a disabled-then-enabled object is not worth an NRE).
        private void Update() => _onUpdate?.Fire();

        private void FixedUpdate() => _onFixedUpdate?.Fire();

        private void LateUpdate() => _onLateUpdate?.Fire();

        private void OnApplicationFocus(bool focus) => _onFocus?.Fire(focus);

        private void OnApplicationPause(bool pause) => _onPause?.Fire(pause);

        private void OnApplicationQuit() => _onQuit?.Fire();

        private void OnDestroy() => _definition?.Terminate();

        private void Create()
        {
            Context = null;

            _definition = Lifetime.Eternal.DefineNested(gameObject.name);
            var lifetime = _definition.Lifetime;

            _onUpdate = new Signal(lifetime);
            _onLateUpdate = new Signal(lifetime);
            _onFixedUpdate = new Signal(lifetime);
            _onQuit = new Signal(lifetime);
            _onFocus = new Signal<bool>(lifetime);
            _onPause = new Signal<bool>(lifetime);

            DontDestroyOnLoad(gameObject);

            var startup = BootAsync(lifetime);
            Startup = startup;

            // The failure is already reported through OnStartFailed, so nobody has to await Startup. Touch
            // the exception anyway: an unawaited faulted Task otherwise re-reports itself from the
            // finalizer, at an unrelated moment, as a second copy of the same error.
            startup.ContinueWith(Observe, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static void Observe(Task task) => _ = task.Exception;

        private async Task BootAsync(Lifetime lifetime)
        {
            var cancellationToken = lifetime.AsCancellationToken();
            try
            {
                var context = await CreateContextAsync(cancellationToken);

                if (context == null)
                {
                    throw new InvalidOperationException(
                        GetType().Name + ".CreateContextAsync returned null. It must return the context it " +
                        "built - normally the result of ContextBuilder.BuildAsync - and throw if it cannot. " +
                        "Returning null would leave ContextBehaviour.Context null with nothing to explain " +
                        "why.");
                }

                if (lifetime.IsTerminated)
                {
                    // Destroyed or rebuilt while the boot was in flight. Do not publish a context nobody
                    // will ever tear down; disposing is idempotent if it was already scoped to this
                    // lifetime, and is the only cleanup if it was not.
                    context.Dispose();
                    throw new OperationCanceledException(cancellationToken);
                }

                Context = context;
                OnStarted(context);
            }
            catch (OperationCanceledException) when (lifetime.IsTerminated)
            {
                // Ordinary teardown, not a failure: Startup ends cancelled and nothing is logged.
                throw;
            }
            catch (Exception exception)
            {
                OnStartFailed(exception);
                throw;
            }
        }
    }
}
