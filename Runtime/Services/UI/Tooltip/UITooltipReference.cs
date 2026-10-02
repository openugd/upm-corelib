namespace OpenUGD.Services.UI.Tooltip
{
    /// <summary>
    /// A handle to one open — or still opening — tooltip. Closing it is the only thing it does.
    /// </summary>
    /// <remarks>
    /// Returned by <c>UITooltipService.Open</c> before the view has loaded. Deliberately thinner than
    /// <c>UIHudReference</c>, which also exposes its presenter: the tooltip service keeps at most one
    /// tooltip and closes the previous one itself as soon as the next finishes opening, so a handle you are
    /// still holding may already refer to something that is gone. That is not a state you have to test for
    /// — <see cref="Close"/> on an already-closed tooltip does nothing.
    /// </remarks>
    public class UITooltipReference
    {
        private readonly Lifetime.Definition _definition;

        /// <summary>Wraps the scope of one open.</summary>
        /// <remarks>
        /// Called by <c>UITooltipService</c>. Constructing a handle yourself is pointless rather than
        /// forbidden: one built around a scope the service knows nothing about closes nothing.
        /// </remarks>
        /// <param name="definition">The scope of the open, terminated by <see cref="Close"/>. Not
        /// validated — a <c>null</c> definition makes <see cref="Close"/> throw
        /// <see cref="System.NullReferenceException"/>.</param>
        public UITooltipReference(Lifetime.Definition definition) => _definition = definition;

        /// <summary>Closes the tooltip, or cancels the open if its view has not arrived yet.</summary>
        /// <remarks>
        /// Terminates the scope of the open, which runs, in this order: the tooltip drops out of the
        /// service's open list, its view is released, and only then do the presenter and its subtree
        /// close. The presenter's teardown therefore runs after the view has already been handed back,
        /// which is the opposite of the order <c>UIHudReference</c> unwinds in. Idempotent, and safe on a
        /// tooltip the service has already replaced. Destroying the view's <c>GameObject</c> does the same
        /// thing, as does disposing the context that owns the service — the scope of an open is the
        /// intersection of this handle's scope and the service's, so whichever ends first ends the
        /// tooltip.
        /// </remarks>
        /// <exception cref="System.AggregateException">One or more clean-up actions threw. Every one of
        /// them still ran and the tooltip is closed either way; see
        /// <see cref="Lifetime.Definition.Terminate"/>.</exception>
        public void Close() => _definition.Terminate();
    }
}
