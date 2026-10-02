using System;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>
    /// The read side of the window registry: turns a presenter type into the registration that says how its
    /// view is built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Split from <see cref="IUIWindowsRegister"/> so the two capabilities can be handed out separately, and
    /// implemented explicitly by <see cref="UIWindowService"/> — its members are reachable through these
    /// interfaces only, never through a <see cref="UIWindowService"/>-typed reference.
    /// </para>
    /// <para>
    /// <b>Substituting one is how registrations come from somewhere else</b> — a scriptable object, an
    /// addressables catalogue, a test double. <see cref="UIWindowService"/> resolves every lookup through an
    /// injected <see cref="IUIWindowsProvider"/> rather than reading its own dictionary directly, so the
    /// lookup can be replaced without replacing the service that does the opening. Expect
    /// <see cref="Get"/> to be called twice for a single open, and to be cheap.
    /// </para>
    /// </remarks>
    public interface IUIWindowsProvider
    {
        /// <summary>
        /// Finds the registration for a window presenter type.
        /// </summary>
        /// <param name="type">The presenter type, as passed to <see cref="IUIWindowService.Open"/>.</param>
        /// <returns>
        /// The registration. Reading <see cref="UIWindowFactoryInfo.Options"/> on it is what finally applies
        /// the configuration callback given at registration time, so the options are built once and shared
        /// by every open of that type however often this is called.
        /// </returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// <paramref name="type"/> is not registered — how <see cref="UIWindowService"/> reports it, since
        /// it indexes a dictionary. An implementation that returns <c>null</c> instead is also tolerated:
        /// <see cref="IUIWindowService.Open"/> turns that into an <see cref="ArgumentException"/>.
        /// </exception>
        UIWindowFactoryInfo Get(Type type);
    }

    /// <summary>
    /// The write side of the window registry: which presenter types can be opened, and how their views are
    /// built.
    /// </summary>
    /// <remarks>
    /// Implemented explicitly by <see cref="UIWindowService"/>. Prefer the <c>ContextBuilder.AddWindow</c>
    /// installer extension, which also registers the service, defers the registration to
    /// <see cref="BootPhase.Configure"/> — after every service's awake, before the initialize phase that
    /// might open one — and supplies the <see cref="Options.Context"/> that presenters and providers are
    /// built from. Calling this interface directly is for registering a window after the context has been
    /// built, and then the context is yours to set.
    /// </remarks>
    public interface IUIWindowsRegister
    {
        /// <summary>
        /// Registers <paramref name="type"/> as an openable window, replacing any registration it already
        /// had.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b><paramref name="options"/> does not run here.</b> It is applied to a fresh
        /// <see cref="WindowOptions"/> the first time this registration is opened, and never again — so it
        /// must not depend on anything that is only true at registration time, and every window of this type
        /// shares the one options instance it configured.
        /// </para>
        /// <para>
        /// <b>An already-terminated <paramref name="lifetime"/> registers nothing that outlives the
        /// call</b>: <see cref="OpenUGD.Lifetime.AddAction"/> runs the removal immediately, so the entry is
        /// added and taken straight back out.
        /// </para>
        /// <para>
        /// <b>Removal is keyed by type, not by entry.</b> If the same type is registered twice from two
        /// different lifetimes, whichever ends first removes whatever registration is current — including
        /// the newer one that replaced it.
        /// </para>
        /// </remarks>
        /// <param name="lifetime">The scope the registration lives in. Once it terminates the window can no
        /// longer be opened; windows already open are untouched.</param>
        /// <param name="type">The presenter type to register. <see cref="IUIWindowService.Open"/> requires
        /// it to derive from <c>Presenter</c>, but nothing checks that here.</param>
        /// <param name="options">Configures the registration — where the view is loaded from, which
        /// <see cref="IUIComponentProvider"/> builds it, which <see cref="OpenUGD.Context"/> it is built
        /// from. Applied lazily; see the remarks.</param>
        void AddWindow(Lifetime lifetime, Type type, Action<WindowOptions> options);
    }
}
