using OpenUGD.Core.Presenters;

namespace OpenUGD.Services.UI.Hud
{
    /// <summary>
    /// A handle to one open — or still opening — HUD: the only way to close it, and the way to reach its
    /// presenter once there is one.
    /// </summary>
    /// <remarks>
    /// Returned by <see cref="IHudService.Open"/> before the view has loaded, so the handle is valid, and
    /// closeable, for the whole of the open rather than only once the HUD is on screen. It carries no
    /// registration data of its own; ask the service what is open, and this handle only about this HUD.
    /// </remarks>
    public class UIHudReference
    {
        private readonly Lifetime.Definition _definition;

        /// <summary>Wraps the scope of one open.</summary>
        /// <remarks>
        /// Called by <see cref="UIHudService"/>. Constructing a handle yourself is pointless rather than
        /// forbidden: one built around a scope the service knows nothing about closes nothing.
        /// </remarks>
        /// <param name="definition">The scope of the open, terminated by <see cref="Close"/>. Not
        /// validated — a <c>null</c> definition makes <see cref="Close"/> throw
        /// <see cref="System.NullReferenceException"/>.</param>
        public UIHudReference(Lifetime.Definition definition) => _definition = definition;

        /// <summary>
        /// The presenter behind this HUD: <c>null</c> until its view has loaded, and <c>null</c> again once
        /// it has closed.
        /// </summary>
        /// <remarks>
        /// The service sets it the moment the view arrives and clears it during teardown, so a handle kept
        /// past <see cref="Close"/> reads <c>null</c> rather than handing back a dead presenter. Read it
        /// when you need it; caching it defeats that.
        /// </remarks>
        public Presenter Presenter { get; internal set; }

        /// <summary>Closes this HUD, or cancels the open if its view has not arrived yet.</summary>
        /// <remarks>
        /// <para>
        /// Terminates the scope of the open, which runs, in this order: the presenter's own teardown and
        /// that of its whole subtree; removal of this handle from <see cref="IHudService.Opened"/>; release
        /// of the view; the <see cref="UIHudActionType.Closed"/> notification; and finally clearing
        /// <see cref="Presenter"/>. A listener therefore sees a world in which this HUD is already gone.
        /// </para>
        /// <para>
        /// Idempotent — closing an already-closed HUD does nothing. Destroying the view's <c>GameObject</c>
        /// has the same effect as calling this, as does disposing the context that owns the service, so a
        /// HUD never outlives the view or the context it belongs to.
        /// </para>
        /// </remarks>
        /// <exception cref="System.AggregateException">One or more clean-up actions threw. Every one of
        /// them still ran and the HUD is closed either way; see
        /// <see cref="Lifetime.Definition.Terminate"/>.</exception>
        public void Close() => _definition.Terminate();
    }
}