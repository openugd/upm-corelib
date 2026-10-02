using System;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>
    /// What happened to a window, as reported to <see cref="IUIWindowService.Subscribe"/>.
    /// </summary>
    /// <remarks>
    /// A notification carries the presenter <see cref="Type"/> and this value and nothing else — not the
    /// <see cref="UIWindowReference"/> — so a listener cannot tell two concurrently open windows of the same
    /// type apart. When that distinction matters, read <see cref="IUIWindowService.Opened"/> instead of
    /// trying to track it from the notifications.
    /// </remarks>
    public enum UIWindowActionType
    {
        /// <summary>
        /// The end of an open attempt: the view was created, the presenter attached to it and the
        /// <c>onOpen</c> callback run.
        /// </summary>
        /// <remarks>
        /// Raised at the end of the open sequence unconditionally — including for a window that closed
        /// itself part-way through it. A presenter that terminates its own scope while being wired up
        /// therefore produces <see cref="Closed"/> first and this afterwards. Read this as "an open was
        /// attempted", and consult <see cref="IUIWindowService.Opened"/> for what is actually on screen.
        /// </remarks>
        Opened = 0,

        /// <summary>
        /// Obsolete alias of <see cref="Opened"/>, kept for source compatibility. It is the <i>same</i>
        /// value, not a second one, so no listener can distinguish the two and a <c>switch</c> carrying a
        /// case for each does not compile. Delete the usage rather than handling it.
        /// </summary>
        [Obsolete] WindowOpened = Opened,

        /// <summary>
        /// A window's scope has ended — through <see cref="UIWindowReference.Close"/>, the destruction of
        /// its view, or the disposal of the context that owns the service.
        /// </summary>
        /// <remarks>
        /// Raised once the presenter has been closed, the view released or pooled and the handle dropped
        /// from <see cref="IUIWindowService.Opened"/>, but before
        /// <see cref="UIWindowReference.Presenter"/> is cleared — so a listener can still reach the
        /// presenter that is going away, and must not assume its view is still usable.
        /// </remarks>
        Closed = 1,

        /// <summary>
        /// Obsolete alias of <see cref="Closed"/>, kept for source compatibility, and the same value rather
        /// than a distinct one. See <see cref="WindowOpened"/>.
        /// </summary>
        [Obsolete] WindowClosed = Closed
    }
}