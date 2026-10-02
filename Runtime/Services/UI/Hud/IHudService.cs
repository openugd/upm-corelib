using System;
using System.Collections.ObjectModel;
using OpenUGD.Core.Presenters;

namespace OpenUGD.Services.UI.Hud
{
#pragma warning disable CS0649
    /// <summary>Opens, tracks and reports HUD presenters.</summary>
    /// <remarks>
    /// <para>
    /// A HUD is UI that stays up rather than being navigated to, so opens are tracked in a flat list
    /// instead of a stack: any number of HUD presenters can be open at once, and each is closed through its
    /// own <see cref="UIHudReference"/>. Contrast <c>UITooltipService</c>, which keeps at most one.
    /// </para>
    /// <para>
    /// <b>Everything here is keyed by presenter type.</b> A HUD is registered by type with <c>AddHud</c>,
    /// opened by type, found by type and reported by type; the service never hands out a view.
    /// </para>
    /// <para>
    /// The default implementation, <see cref="UIHudService"/>, builds its notification signal in the Awake
    /// boot phase, so <see cref="Subscribe"/> only works once that phase has finished — from Configure
    /// onwards. Called earlier it throws <see cref="NullReferenceException"/>.
    /// </para>
    /// </remarks>
    public interface IHudService
    {
        /// <summary>The HUD handles whose view has finished loading, in the order the loads
        /// finished.</summary>
        /// <remarks>
        /// A <b>live view</b> over the service's own list, and the same instance on every call: it changes
        /// as HUDs open and close, so do not hold it across an open or a close, and copy it first if the
        /// loop you are writing can close anything. A handle returned by <see cref="Open"/> appears here
        /// only once its view exists, so a HUD that is still loading — or one whose open was cancelled
        /// before the view arrived — is never in it.
        /// </remarks>
        ReadOnlyCollection<UIHudReference> Opened { get; }

        /// <summary>The first open presenter assignable to <typeparamref name="T"/>.</summary>
        /// <remarks>
        /// A linear scan of <see cref="Opened"/>, so it sees only HUDs whose view has already loaded: with
        /// an asynchronous provider a HUD opened moments ago is not findable yet. Nothing is cached, and
        /// the result goes stale the moment that HUD closes — call this when you need the presenter rather
        /// than holding on to what it returned.
        /// </remarks>
        /// <typeparam name="T">The presenter type to look for. Matching is a runtime type test, so a base
        /// class matches any open HUD derived from it.</typeparam>
        /// <returns>The matching presenter, or <c>null</c> if no open HUD is a
        /// <typeparamref name="T"/>.</returns>
        T Find<T>() where T : Presenter;

        /// <summary>Starts opening a HUD presenter and returns its handle immediately, before the view
        /// exists.</summary>
        /// <remarks>
        /// <para>
        /// The presenter is built from the registration's context and its view is requested from the
        /// registration's <see cref="IUIComponentProvider"/>. Only when that view arrives is the handle
        /// added to <see cref="Opened"/> — first, before any of the wiring — and the presenter then
        /// initialised, given <paramref name="model"/>, given the view and handed to
        /// <paramref name="onOpen"/>. None of that has happened when this method returns:
        /// <see cref="UIHudReference.Presenter"/> is still <c>null</c>, and if the provider's load never
        /// completes it stays that way and no notification is ever raised.
        /// </para>
        /// <para>
        /// <b>Model before view</b>, so the presenter's first refresh already sees the model.
        /// </para>
        /// <para>
        /// <b>Closing early is safe.</b> Calling <see cref="UIHudReference.Close"/> on the returned handle
        /// before the view arrives cancels the open: the provider is handed that same scope and is required
        /// to abandon the load with it, so no view is instantiated, no presenter is initialised and
        /// <paramref name="onOpen"/> never runs.
        /// </para>
        /// <para>
        /// <b>Argument validation is an assertion, not an exception.</b> <see cref="UIHudService"/> checks
        /// that <paramref name="type"/> derives from <see cref="Presenter"/> — and, when
        /// <paramref name="model"/> is non-<c>null</c>, that the presenter can hold a model — with
        /// <c>UnityEngine.Assertions.Assert</c>, which is <c>[Conditional("UNITY_ASSERTIONS")]</c> and is
        /// therefore compiled out of a release build. There the mistake surfaces later and elsewhere, while
        /// the open is being carried out, and as two different failures: a <paramref name="type"/> that is
        /// not a presenter as an <see cref="InvalidCastException"/> from the cast, and a presenter that
        /// cannot hold <paramref name="model"/> as a <see cref="NullReferenceException"/> from the failed
        /// <see cref="IPresenterWithModel"/> cast. Contrast <c>IUIWindowService.Open</c>, which throws a
        /// real <see cref="ArgumentException"/> in every build.
        /// </para>
        /// </remarks>
        /// <param name="type">The presenter type to open. Must derive from <see cref="Presenter"/> and must
        /// have been registered, normally with <c>ContextBuilder.AddHud</c>.</param>
        /// <param name="model">The model to set before the view is attached, or <c>null</c> to open the
        /// presenter without one. A non-<c>null</c> model requires the presenter to implement
        /// <see cref="IPresenterWithModel"/>.</param>
        /// <param name="onOpen">Invoked once, with the presenter, after it has its model and its view and
        /// before the <see cref="UIHudActionType.Opened"/> notification. Not invoked if the handle was
        /// closed first.</param>
        /// <returns>A handle that closes this HUD, and that exposes the presenter once it exists.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// No HUD is registered for <paramref name="type"/>. <see cref="UIHudService"/> resolves the
        /// registration with a dictionary indexer while carrying out the open, so this — rather than a
        /// named argument error — is what an unregistered type gives you.
        /// </exception>
        UIHudReference Open(Type type, object model = null, Action<Presenter> onOpen = null);

        /// <summary>Subscribes to HUD open and close notifications for as long as
        /// <paramref name="lifetime"/> is alive.</summary>
        /// <remarks>
        /// The listener is given the presenter type rather than the handle, so a listener that needs the
        /// presenter itself looks it up with <see cref="Find{T}"/>. The two notifications normally bracket
        /// a HUD that actually reached the screen: by the time <see cref="UIHudActionType.Opened"/> arrives
        /// the presenter is fully wired and already in <see cref="Opened"/>, and by the time
        /// <see cref="UIHudActionType.Closed"/> arrives it has closed, left <see cref="Opened"/> and had
        /// its view released. See <see cref="UIHudActionType"/> for the one case in which they arrive the
        /// other way round.
        /// </remarks>
        /// <param name="lifetime">The subscriber's scope; the listener is detached when it ends. There is
        /// deliberately no explicit unsubscribe — terminate a nested scope instead.</param>
        /// <param name="listener">Receives the presenter type and what happened to it.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="lifetime"/> or <paramref name="listener"/> is <c>null</c>.
        /// </exception>
        void Subscribe(Lifetime lifetime, Action<Type, UIHudActionType> listener);
    }
}