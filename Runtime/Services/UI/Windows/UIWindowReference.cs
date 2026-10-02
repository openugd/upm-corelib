using System;
using OpenUGD.Core.Presenters;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>
    /// The handle <see cref="IUIWindowService.Open"/> hands back: what a caller keeps in order to close a
    /// window, and to reach its presenter once one exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The handle exists before the window does.</b> Opening loads a view, which may take frames, so
    /// this object is returned while <see cref="Presenter"/> is still <c>null</c>. Anything that depends on
    /// the presenter belongs in the <c>onOpen</c> callback of <see cref="UIWindowServiceExtension"/>, not
    /// on the line after <c>Open</c>. The opposite ordering is equally legal: per
    /// <see cref="IUIComponentProvider.Provide"/> a provider may complete synchronously, in which case the
    /// window is fully open — <c>onOpen</c> invoked, <see cref="UIWindowActionType.Opened"/> raised — before
    /// the handle is ever returned. Neither ordering is guaranteed, so rely on neither.
    /// </para>
    /// <para>
    /// <b>One handle, one window.</b> It is not reusable: once <see cref="Lifetime"/> has terminated the
    /// window is gone and reopening means calling <c>Open</c> again. <see cref="Close"/> stays safe to call.
    /// </para>
    /// </remarks>
    public class UIWindowReference
    {
        private readonly Lifetime.Definition _definition;

        /// <summary>
        /// Creates a handle. Called by <see cref="IUIWindowService"/> implementations — there is little
        /// reason to construct one by hand, because nothing outside the service owns the scope it needs.
        /// </summary>
        /// <param name="definition">
        /// Owns the window's scope and is the only thing that can end it, which is what makes
        /// <see cref="Close"/> possible. Not validated: a <c>null</c> surfaces later as a
        /// <see cref="NullReferenceException"/> from <see cref="Lifetime"/> or <see cref="Close"/>.
        /// </param>
        /// <param name="options">The registration's shared settings instance, not a snapshot of it; see
        /// <see cref="Options"/>.</param>
        /// <param name="type">The presenter type being opened. Stored as given; nothing here checks it
        /// against the registry or against <c>Presenter</c>.</param>
        /// <param name="model">The model to hand the presenter, or <c>null</c> for a window without
        /// one.</param>
        public UIWindowReference(
            Lifetime.Definition definition,
            WindowOptions options,
            Type type,
            object model
        )
        {
            _definition = definition;
            Options = options;
            Type = type;
            Model = model;
        }

        /// <summary>
        /// The window's scope, nested in the window service's own: it ends when <see cref="Close"/> is
        /// called, when the view's <c>GameObject</c> is destroyed, or when the context that owns the service
        /// is disposed — whichever comes first.
        /// </summary>
        /// <remarks>
        /// Register on it anything that must not outlive the window. It is already terminated by the time
        /// the <see cref="UIWindowActionType.Closed"/> notification is raised, so testing
        /// <c>Lifetime.IsTerminated</c> reliably tells a live handle from a spent one.
        /// </remarks>
        public Lifetime Lifetime => _definition.Lifetime;

        /// <summary>
        /// The settings this window was opened with: the registration's own
        /// <see cref="WindowOptions"/> instance, not a copy of it.
        /// </summary>
        /// <remarks>
        /// Every open of this presenter type shares that one instance, and it is configured exactly once —
        /// on the first open. Read it to find out where the view came from or whether the registration is
        /// marked <see cref="WindowOptions.Fullscreen"/>; writing to it changes every later open too.
        /// </remarks>
        public WindowOptions Options { get; private set; }

        /// <summary>
        /// The presenter type this window was opened for. Unlike <see cref="Presenter"/> it is known the
        /// moment the handle exists, so it is what to test while a window is still loading.
        /// </summary>
        public Type Type { get; private set; }

        /// <summary>
        /// The model passed to <c>Open</c>, or <c>null</c> if none was.
        /// </summary>
        /// <remarks>
        /// This is the model that <i>will be</i> given to the presenter once its view has loaded, recorded
        /// once and never updated. A presenter that has since replaced its own model no longer agrees with
        /// this property; ask the presenter, not the handle.
        /// </remarks>
        public object Model { get; private set; }

        /// <summary>
        /// The presenter driving this window, or <c>null</c> before its view has loaded and again once the
        /// window has closed.
        /// </summary>
        /// <remarks>
        /// Non-<c>null</c> only between those two moments, which is very nearly the span in which the handle
        /// is listed in <see cref="IUIWindowService.Opened"/>: it is set just after the handle is added
        /// there, and cleared at the end of the same clean-up step that raises
        /// <see cref="UIWindowActionType.Closed"/> — after that notification, so a listener reading it from
        /// the callback still sees the presenter that is going away.
        /// </remarks>
        public Presenter Presenter { get; internal set; }

        /// <summary>
        /// Closes the window: terminates <see cref="Lifetime"/>, which closes the presenter and its
        /// children, releases or pools the view, drops the handle from
        /// <see cref="IUIWindowService.Opened"/> and raises <see cref="UIWindowActionType.Closed"/>.
        /// </summary>
        /// <remarks>
        /// Idempotent, and safe to call before the view has finished loading — that cancels the open, so the
        /// presenter is never attached, <see cref="Presenter"/> stays <c>null</c> and <c>onOpen</c> never
        /// runs.
        /// </remarks>
        /// <exception cref="AggregateException">
        /// One or more clean-up actions threw. All of them still ran and the window is closed either way; a
        /// later call neither re-runs them nor re-throws.
        /// </exception>
        public void Close() => _definition.Terminate();
    }
}
