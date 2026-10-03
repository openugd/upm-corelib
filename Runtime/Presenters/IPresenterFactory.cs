using System;
using System.Diagnostics.CodeAnalysis;

namespace OpenUGD.Presenters
{
    /// <summary>
    /// How a presenter tree gets its presenters built and injected, without knowing which container does it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two members, two callers.</b> <see cref="Inject"/> is what the tree itself calls: every presenter
    /// attached with <see cref="Presenter.Attach"/>, <see cref="Presenter.AddPresenter{T}"/> or
    /// <see cref="Presenter.Root"/> is passed to it once, after it is given its <see cref="Presenter.Lifetime"/>
    /// and before its <c>OnInitialize</c>. A presenter created with <c>new</c> therefore still has its injected
    /// members filled in. <see cref="Create"/> is for code that knows a presenter only by its type — a
    /// presenter-opening service, for example — and needs it constructed. Nothing in the presenter tree calls
    /// <see cref="Create"/>.
    /// </para>
    /// <para>
    /// <b>Implementations.</b> <c>ContextPresenterFactory</c>, in the <c>com.openugd.corelib</c> assembly, is the
    /// one for <c>OpenUGD.Context</c>. For another container, write the two members over it: build the type
    /// with its constructor injection, inject an existing object with its member injection. A factory that injects
    /// nothing, and constructs with <c>Activator.CreateInstance</c>, is a legitimate choice for presenters that
    /// are handed what they need through their constructors by whoever writes <c>new</c>; it is also what a unit
    /// test usually wants.
    /// </para>
    /// <para>
    /// <b>One factory per tree.</b> A child attached with <see cref="Presenter.AddPresenter{T}"/> is injected by
    /// the factory its parent was attached with, so the factory handed to <see cref="Presenter.Root"/> or
    /// <see cref="Presenter.Attach"/> serves that presenter's whole subtree.
    /// </para>
    /// </remarks>
    public interface IPresenterFactory
    {
        /// <summary>
        /// Constructs a presenter of <paramref name="presenterType"/>, with its dependencies supplied. The result is
        /// not attached; hand it to <see cref="Presenter.Attach"/> or <see cref="Presenter.AddPresenter{T}"/>.
        /// </summary>
        /// <remarks>
        /// <b>Managed code stripping.</b> <paramref name="presenterType"/> is annotated for Unity's linker, so a
        /// presenter type written at a call site, as in <c>factory.Create(typeof(ShopPresenter))</c>, keeps its
        /// constructors in a stripped build. An implementation in another assembly should carry the same
        /// annotation on its parameter, through an internal copy of
        /// <c>System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute</c> as this package declares one;
        /// without it the linker reports the mismatch as warning IL2092.
        /// </remarks>
        /// <param name="presenterType">A concrete type deriving from <see cref="Presenter"/>.</param>
        /// <returns>The new presenter, never <c>null</c>. Implementations throw rather than return
        /// <c>null</c>.</returns>
        Presenter Create([DynamicallyAccessedMembers(Trimming.Constructors)] Type presenterType);

        /// <summary>
        /// Fills in the dependencies of a presenter that was constructed elsewhere, typically with <c>new</c>.
        /// Called once per presenter by <see cref="Presenter.Attach"/>, before the presenter's
        /// <c>OnInitialize</c>.
        /// </summary>
        /// <param name="presenter">The presenter being attached. Its <see cref="Presenter.Lifetime"/> is already
        /// set.</param>
        void Inject(Presenter presenter);
    }
}
