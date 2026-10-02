// © 2025 OpenUGD

using System;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>
    /// The registration call that predates <c>AddWindow</c>, kept so existing call sites still compile.
    /// A window registered through it cannot in fact be opened; see the method's remarks.
    /// </summary>
    public static class UIWindowsRegisterExtensions
    {
        /// <summary>
        /// Registers a window from the old positional arguments by forwarding them to
        /// <see cref="IUIWindowsRegister.AddWindow"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It sets no <see cref="Options.Context"/>, and the built-in registry supplies none</b>, so a
        /// window registered through this method throws <see cref="NullReferenceException"/> when it is
        /// opened. That is the substantive reason to move to
        /// <c>ContextBuilder.AddWindow&lt;TPresenter&gt;</c>, which registers the service, defers to
        /// <see cref="BootPhase.Configure"/> and passes the built context in — not merely a rename.
        /// </para>
        /// <para>
        /// <b><paramref name="provider"/> is shared by every open</b> of this window: it is captured and
        /// handed back by the options factory, rather than built afresh per open as the default provider is.
        /// An implementation keeping per-open state in a field will misbehave under concurrent opens; see
        /// <see cref="IUIComponentProvider"/>. Nor is <c>null</c> a "use the default" fallback here — it
        /// merely postpones the failure to open time.
        /// </para>
        /// </remarks>
        /// <param name="register">The window registry.</param>
        /// <param name="type">The presenter type to register. Must derive from <c>Presenter</c> to be
        /// openable, which is checked at open time rather than here.</param>
        /// <param name="path">Recorded as <see cref="Options.Path"/>. What it means is up to
        /// <paramref name="provider"/>; <see cref="UIWindowComponentProvider"/> reads it as a
        /// <c>Resources</c> path.</param>
        /// <param name="isFullscreen">Recorded as <see cref="WindowOptions.Fullscreen"/>. Nothing in this
        /// library reads that flag; it is metadata for your own layer to act on.</param>
        /// <param name="provider">Builds the view. Must not be <c>null</c>.</param>
        /// <param name="lifetime">The scope the registration lives in; when it terminates the window can no
        /// longer be opened.</param>
        [Obsolete("Use AddWindow instead.")]
        public static void Register(
            this IUIWindowsRegister register,
            Type type,
            string path,
            bool isFullscreen,
            IUIComponentProvider provider,
            Lifetime lifetime
        ) =>
            register.AddWindow(
                lifetime: lifetime,
                type: type,
                options: options => options
                    .SetFullscreen(isFullscreen)
                    .SetPath(path)
                    .SetProvider(() => provider)
            );
    }
}