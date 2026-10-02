using System;
using System.Threading;
using System.Threading.Tasks;
using OpenUGD.Core.Logging;

namespace OpenUGD.Services
{
    /// <summary>
    /// Compatibility shim for the pre-2.0.0 service base class. New services should derive from nothing and
    /// implement <see cref="IAwakeService"/> and/or <see cref="IInitializeService"/> directly, taking their
    /// dependencies through the constructor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existing subclasses compile untouched: <see cref="Lifetime"/>, <see cref="ILog"/>,
    /// <see cref="Resolve{T}"/>, <see cref="OnAwake"/> and <see cref="OnInitialize"/> all keep their
    /// signatures and their meaning, and the two boot phases run in the same order as before.
    /// </para>
    /// <para>
    /// <b>Gone in 2.0.0.</b> <c>ServiceState</c>, the <c>State</c> property and the public
    /// <c>Service.Internal</c> class. The state machine existed to check an ordering that the caller could
    /// get wrong; the new pipeline runs the phases itself, so there is no ordering left to validate. Its two
    /// <c>async void</c> completion checks went with it — an exception thrown from either could not be caught
    /// by anything and took down the process.
    /// </para>
    /// <para>
    /// <b>Also gone.</b> <c>Service</c> no longer implements <c>IResolve</c>: that interface lives in
    /// <c>com.openugd.dependency.injection</c>, which corelib no longer depends on. Code that cast a service
    /// to <c>IResolve</c> must ask the <see cref="OpenUGD.Context"/> instead.
    /// </para>
    /// </remarks>
    [Obsolete("Derive from nothing and implement IAwakeService/IInitializeService instead. " +
              "Service is removed in the next release.")]
    public abstract class Service : IAwakeService, IInitializeService
    {
        [Inject] private Context _context;
        private ILog _logger;

        /// <summary>
        /// The scope of the context this service belongs to. Terminates when the context is disposed.
        /// </summary>
        public Lifetime Lifetime => Context.Lifetime;

        /// <summary>
        /// A logger tagged with this service's type, resolved lazily from the context on first read.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// No <see cref="OpenUGD.Core.Logging.ILog"/> is registered. Previously the builder always handed
        /// one in; now it is an ordinary registration, so its absence is reported the first time the property
        /// is actually read rather than silently yielding <c>null</c>.
        /// </exception>
        public ILog Log
        {
            get
            {
                if (_logger != null) return _logger;

                ILog root;
                if (!Context.TryResolve(out root))
                {
                    throw new InvalidOperationException(
                        "'" + GetType().Name + ".Log' was read, but no OpenUGD.Core.Logging.ILog is " +
                        "registered in this Context. Register one - for example " +
                        "services.AddInstance<ILog>(new LogRoot()) - or stop using the property.");
                }

                return _logger = root.WithTag(GetType());
            }
        }

        /// <summary>Runs in the Awake boot phase. Override to do async start-up work.</summary>
        protected virtual Task OnAwake() => Task.CompletedTask;

        /// <summary>Runs in the Initialize boot phase, after every service has finished its Awake.</summary>
        protected virtual Task OnInitialize() => Task.CompletedTask;

        /// <summary>Resolves a service from the context this service belongs to.</summary>
        /// <typeparam name="T">The registered contract.</typeparam>
        /// <exception cref="ContextException">Nothing is registered for <typeparamref name="T"/>.</exception>
        protected T Resolve<T>() => ContextExtensions.Resolve<T>(Context);

        Task IAwakeService.AwakeAsync(CancellationToken cancellationToken) =>
            OnAwake() ?? throw new InvalidOperationException(
                "'" + GetType().Name + ".OnAwake()' returned null. Return Task.CompletedTask for a " +
                "synchronous override; a null Task cannot be awaited and would stop the boot with a " +
                "NullReferenceException that names nothing.");

        Task IInitializeService.InitializeAsync(CancellationToken cancellationToken) =>
            OnInitialize() ?? throw new InvalidOperationException(
                "'" + GetType().Name + ".OnInitialize()' returned null. Return Task.CompletedTask for a " +
                "synchronous override; a null Task cannot be awaited and would stop the boot with a " +
                "NullReferenceException that names nothing.");

        private Context Context =>
            _context ?? throw new InvalidOperationException(
                "'" + GetType().Name + "' was constructed outside a Context, so it has no Lifetime, no " +
                "ILog and nothing to resolve from. Register it - services.Add<" + GetType().Name +
                ">() - and let BuildAsync construct it; a Service created with 'new' is never wired.");
    }
}
