namespace OpenUGD.Services.UI.Hud
{
    /// <summary>What happened to a HUD, as reported to a listener of
    /// <see cref="IHudService.Subscribe"/>.</summary>
    /// <remarks>
    /// The two members bracket a HUD that actually reached the screen: an open cancelled before its view
    /// arrived raises neither. Every <see cref="Opened"/> is paired with exactly one
    /// <see cref="Closed"/> — including when the whole context is disposed — though a subscriber whose own
    /// scope ended first will not be there to hear it. The pair is out of order in exactly one case: a HUD
    /// closed from inside its own wiring, before <c>UIHudService</c> has got as far as raising
    /// <see cref="Opened"/>, is reported <see cref="Closed"/> first and <see cref="Opened"/> after.
    /// </remarks>
    public enum UIHudActionType
    {
        /// <summary>
        /// The HUD is fully open: its presenter has been initialised, given its model if one was supplied
        /// and given its view, its open callback has run, and it is already in
        /// <see cref="IHudService.Opened"/>. The exception is a HUD closed part-way through that wiring,
        /// which is reported <see cref="Closed"/> first and then this anyway, with none of the above true.
        /// </summary>
        Opened,

        /// <summary>
        /// The HUD has closed. Raised late in teardown — after the presenter and its subtree have closed,
        /// the handle has left <see cref="IHudService.Opened"/> and the view has been released — so a
        /// listener is told that the HUD is gone, not that it is going, and must not try to reach it.
        /// </summary>
        Closed
    }
}