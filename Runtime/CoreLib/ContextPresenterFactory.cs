using System;
using System.Diagnostics.CodeAnalysis;

namespace OpenUGD.Presenters
{
    /// <summary>
    /// The <see cref="IPresenterFactory"/> over an <see cref="OpenUGD.Context"/>: presenters are constructed with
    /// <see cref="OpenUGD.Context.Instantiate"/> and injected with <see cref="OpenUGD.Context.Inject"/>, so their
    /// <c>[Inject]</c> members are filled in whether the container built them or they were attached with
    /// <c>new</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Root a tree with it — <c>new Presenter.Root(lifetime, new ContextPresenterFactory(context))</c> — and every
    /// presenter added under that root is injected from the context passed to the constructor, before its
    /// <c>OnInitialize</c>. An <c>[Inject(Optional = true)]</c> member whose contract is not
    /// registered is left as it was; any other unresolvable member makes the attach throw
    /// <see cref="ContextException"/>.
    /// </para>
    /// <para>
    /// It can also be registered, so that services take an <see cref="IPresenterFactory"/> rather than a
    /// <see cref="OpenUGD.Context"/>: <c>builder.Services.Add&lt;ContextPresenterFactory&gt;().As&lt;IPresenterFactory&gt;()</c>.
    /// The container then hands it the context being built.
    /// </para>
    /// <para>
    /// It lives in <c>com.openugd.corelib</c> so that <c>com.openugd.presenters</c> references no container.
    /// </para>
    /// </remarks>
    public sealed class ContextPresenterFactory : IPresenterFactory
    {
        private readonly Context _context;

        /// <summary>
        /// Creates a factory over <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The context presenters are constructed and injected from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
        public ContextPresenterFactory(Context context) =>
            _context = context ?? throw new ArgumentNullException(nameof(context), $"{nameof(context)} can't be null");

        /// <summary>
        /// Constructs <paramref name="presenterType"/> with <see cref="OpenUGD.Context.Instantiate"/>, which picks
        /// the constructor, resolves its parameters and fills in the <c>[Inject]</c> members.
        /// </summary>
        /// <remarks>
        /// Attaching the result injects it again, through <see cref="Inject"/>. A context has one instance per
        /// contract, so the second pass assigns what the first one did.
        /// </remarks>
        /// <param name="presenterType">A concrete type deriving from <see cref="Presenter"/>.</param>
        /// <returns>The new, unattached presenter.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="presenterType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="presenterType"/> does not derive from
        /// <see cref="Presenter"/>.</exception>
        /// <exception cref="ContextException">The type cannot be constructed from the context; see
        /// <see cref="OpenUGD.Context.Instantiate"/>.</exception>
        /// <exception cref="ObjectDisposedException">The context has been disposed.</exception>
        public Presenter Create([DynamicallyAccessedMembers(Trimming.Constructors)] Type presenterType)
        {
            if (presenterType == null)
                throw new ArgumentNullException(nameof(presenterType), $"{nameof(presenterType)} can't be null");
            if (!typeof(Presenter).IsAssignableFrom(presenterType))
                throw new ArgumentException($"{presenterType} does not derive from {typeof(Presenter)}",
                    nameof(presenterType));

            return (Presenter)_context.Instantiate(presenterType);
        }

        /// <summary>
        /// Fills in the <c>[Inject]</c> members of <paramref name="presenter"/> with
        /// <see cref="OpenUGD.Context.Inject"/>.
        /// </summary>
        /// <param name="presenter">The presenter being attached.</param>
        /// <exception cref="ArgumentNullException"><paramref name="presenter"/> is <c>null</c>.</exception>
        /// <exception cref="ContextException">A required member cannot be resolved; see
        /// <see cref="OpenUGD.Context.Inject"/>.</exception>
        /// <exception cref="ObjectDisposedException">The context has been disposed.</exception>
        public void Inject(Presenter presenter)
        {
            if (presenter == null)
                throw new ArgumentNullException(nameof(presenter), $"{nameof(presenter)} can't be null");

            _context.Inject(presenter);
        }
    }
}
