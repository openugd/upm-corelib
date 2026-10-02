using System;
using UnityEngine;

namespace OpenUGD.Services.UI
{
    /// <summary>
    /// What an <see cref="IUIComponentProvider"/> hands back: the loaded view component together with the
    /// scope that owns it. Disposing the context is how the view is released.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Who does what.</b> The provider constructs this, registers its own clean-up on
    /// <see cref="Lifetime"/> — returning the prefab handle, pooling the instance, destroying it — and
    /// passes it to its <c>onResult</c> callback. The UI service that asked for the view keeps the context
    /// and calls <see cref="Dispose"/> when whatever the view was opened for closes. Neither side needs to
    /// know what the other registered, which is the point: releasing a view stays with the code that knew
    /// how to create it.
    /// </para>
    /// <para>
    /// <b>The scope is nested, not owned.</b> It is defined on the lifetime passed to the constructor, so
    /// it also ends by itself when that lifetime ends; <see cref="Dispose"/> only ends it earlier. There
    /// is no state in which the provider's clean-up is quietly skipped.
    /// </para>
    /// <para>
    /// <b>Not a liveness check.</b> <see cref="Component"/> is captured once and never cleared, so after
    /// disposal it still refers to an object that may already have been destroyed. Track the view's
    /// validity through <see cref="Lifetime"/>, not through the reference.
    /// </para>
    /// </remarks>
    public class UIComponentProviderContext : IDisposable
    {
        private readonly Lifetime.Definition _definition;

        /// <summary>
        /// Captures the loaded component and defines the scope that will release it.
        /// </summary>
        /// <param name="component">The instantiated view. Not validated: a <c>null</c> is stored as-is and
        /// surfaces later, at whatever first dereferences <see cref="Component"/>.</param>
        /// <param name="lifetime">The scope of the open this view belongs to, typically the opened
        /// presenter's. <see cref="Lifetime"/> is nested in it, so the view is released no later than this
        /// ends.</param>
        /// <exception cref="ArgumentNullException"><paramref name="lifetime"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="lifetime"/> has already terminated.
        /// A view whose scope is already gone should not have been instantiated at all, so this reports the
        /// mistake here rather than leaking the instance.</exception>
        public UIComponentProviderContext(Component component, Lifetime lifetime)
        {
            Component = component;
            _definition = Lifetime.Define(lifetime);
        }

        /// <summary>
        /// The instantiated view — whatever the provider chose to return, which need not be the type the
        /// caller asked for: a caller wanting a specific component can <c>GetComponent</c> it off this
        /// one's <c>gameObject</c>, and the built-in UI services do exactly that.
        /// </summary>
        /// <remarks>
        /// Set once, at construction, and never cleared. Reading it after <see cref="Dispose"/> yields a
        /// reference to a component that may already be destroyed.
        /// </remarks>
        public Component Component { get; private set; }

        /// <summary>
        /// The scope the view lives in. Register anything that must be undone when the view goes away —
        /// this is the hook the built-in providers use to pool or destroy the instance.
        /// </summary>
        /// <remarks>
        /// Ends on <see cref="Dispose"/>, or when the lifetime given to the constructor ends, whichever
        /// comes first. Clean-up runs in reverse registration order, so the provider's release action —
        /// registered first, immediately after construction — runs after anything a consumer added later.
        /// </remarks>
        public Lifetime Lifetime => _definition.Lifetime;

        /// <summary>
        /// Releases the view by ending <see cref="Lifetime"/>, running everything registered on it exactly
        /// once, in reverse registration order.
        /// </summary>
        /// <remarks>
        /// Idempotent — later calls do nothing. What "released" means is entirely up to what the provider
        /// registered; the built-in ones return the instance to a pool or destroy it. This method itself
        /// touches neither <see cref="Component"/> nor its <c>GameObject</c>.
        /// </remarks>
        /// <exception cref="AggregateException">One or more clean-up actions threw. Every one of them still
        /// ran, and the context is disposed either way.</exception>
        public void Dispose() => _definition.Dispose();

        /// <summary>
        /// Obsolete alias for <see cref="Dispose"/>: both end the same definition, so the behaviour is
        /// identical. Kept for call sites written before this type implemented <see cref="IDisposable"/>;
        /// nothing in this package calls it.
        /// </summary>
        /// <exception cref="AggregateException">One or more clean-up actions threw; see
        /// <see cref="Dispose"/>.</exception>
        [Obsolete("Use Dispose instead.")]
        public void Terminate() => _definition.Terminate();
    }
}
