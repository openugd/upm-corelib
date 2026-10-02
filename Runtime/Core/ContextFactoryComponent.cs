using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenUGD.Core
{
    /// <summary>
    /// Compatibility shim for the pre-2.0.0 <c>ContextFactoryComponent</c>, whose boot was a synchronous
    /// <c>CreateContext()</c> whose failures could not be observed. Derive from
    /// <see cref="ContextBehaviour"/> instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It keeps the old seam compiling for one release: everything a subclass overrode still exists and still
    /// runs at the same moment. What it cannot restore is the reason for the rewrite — a synchronous
    /// <c>CreateContext()</c> has nowhere to put a failure, so a subclass that throws still reaches
    /// <c>OnStartFailed</c>, but a subclass that discards its own boot task still loses its failures. That is
    /// fixed by moving to <see cref="ContextBehaviour.CreateContextAsync"/>, not by this type.
    /// </para>
    /// <para>
    /// <see cref="ContextBehaviour.Context"/> on this path is a real but <b>empty</b> context, scoped to
    /// <see cref="ContextBehaviour.Lifetime"/>. The pre-2.0.0 context object is whatever
    /// <c>CreateContext()</c> made, and the container knows nothing about it.
    /// </para>
    /// </remarks>
    [Obsolete("Derive from ContextBehaviour and override CreateContextAsync. " +
              "ContextFactoryComponent is removed in the next release.")]
    public abstract class ContextFactoryComponent : ContextBehaviour
    {
        /// <summary>
        /// The pre-2.0.0 synchronous boot seam. Called from
        /// <see cref="ContextBehaviour.CreateContextAsync"/>, on the main thread, exactly where it used to be
        /// called from <c>Awake</c>.
        /// </summary>
        protected abstract void CreateContext();

        /// <inheritdoc />
        protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken)
        {
            CreateContext();
            return OpenUGD.Context.CreateBuilder(Lifetime).BuildAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Compatibility shim for the pre-2.0.0 <c>ContextFactoryComponent&lt;T&gt;</c>. Derive from
    /// <see cref="ContextBehaviour"/> instead.
    /// </summary>
    /// <typeparam name="T">
    /// The pre-2.0.0 context type. The old <c>where T : IContext</c> constraint is gone with
    /// <c>OpenUGD.Core.IContext</c>; any reference type is now accepted.
    /// </typeparam>
    /// <remarks>
    /// <see cref="Context"/> hides <see cref="ContextBehaviour.Context"/> and returns what
    /// <see cref="CreateContext(OpenUGD.Lifetime)"/> made, exactly as before.
    /// </remarks>
    [Obsolete("Derive from ContextBehaviour and override CreateContextAsync. " +
              "ContextFactoryComponent<T> is removed in the next release.")]
    public abstract class ContextFactoryComponent<T> : ContextFactoryComponent
        where T : class
    {
        /// <summary>The object returned by <see cref="CreateContext(OpenUGD.Lifetime)"/>; <c>null</c> until
        /// then.</summary>
        public new T Context { get; private set; }

        /// <summary>Builds the pre-2.0.0 context object.</summary>
        /// <param name="lifetime">The behaviour's scope.</param>
        protected abstract T CreateContext(Lifetime lifetime);

        /// <inheritdoc />
        protected sealed override void CreateContext() => Context = CreateContext(Lifetime);
    }
}
