using System;
using System.Linq;
using OpenUGD.Core.Presenters;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>
    /// Typed and bulk conveniences over <see cref="IUIWindowService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every member here is a thin forward: the <c>Open</c> overloads to
    /// <see cref="IUIWindowService.Open"/>, <c>SubscribeOnChanged</c> to
    /// <see cref="IUIWindowService.Subscribe"/>, and <c>CloseAll</c> to the handles listed in
    /// <see cref="IUIWindowService.Opened"/>. The <c>Open</c> overloads add compile-time checking — the
    /// presenter type, and that the model is one the presenter can actually hold — and cast the presenter
    /// back for you. Nothing here changes behaviour, so every caveat of the underlying service applies
    /// unchanged.
    /// </para>
    /// <para>
    /// <b>An open is not finished when one of these methods returns.</b> The service starts a view load and
    /// hands back a <see cref="UIWindowReference"/>; the presenter is attached, the model applied and
    /// <c>onOpen</c> invoked from inside the provider's callback, which per
    /// <see cref="IUIComponentProvider.Provide"/> may run before the call returns, many frames after it, or
    /// never — the last if the handle is closed while the load is still in flight. Put everything that needs
    /// the presenter in <c>onOpen</c>, and tolerate an open that never completes.
    /// </para>
    /// </remarks>
    public static class UIWindowServiceExtension
    {
        /// <summary>
        /// Closes every window that is currently open, oldest first.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="IUIWindowService.Opened"/> is snapshotted into an array first, because closing a
        /// window removes it from that collection; the allocation is what makes the iteration legal.
        /// </para>
        /// <para>
        /// <b>Windows that are still loading are not closed.</b> One whose view has not arrived yet is not
        /// in <see cref="IUIWindowService.Opened"/>, so it survives this call and opens whenever its load
        /// finishes. Keep its <see cref="UIWindowReference"/> and close that if it must not appear at all.
        /// </para>
        /// </remarks>
        /// <param name="service">The window service.</param>
        /// <exception cref="AggregateException">
        /// A window's clean-up threw; see <see cref="UIWindowReference.Close"/>. That window is closed, but
        /// the loop stops there and every window after it stays open.
        /// </exception>
        public static void CloseAll(this IUIWindowService service)
        {
            foreach (var reference in service.Opened.ToArray())
            {
                reference.Close();
            }
        }

        /// <summary>
        /// Subscribes to every window open and close, discarding both which window it was and which of the
        /// two happened.
        /// </summary>
        /// <remarks>
        /// For "the window stack changed, re-evaluate" listeners — an input blocker, a music duck, a
        /// back-button handler. Use <see cref="IUIWindowService.Subscribe"/> when the answer depends on
        /// which presenter type it was. The listener runs after the change is already visible in
        /// <see cref="IUIWindowService.Opened"/>.
        /// </remarks>
        /// <param name="service">The window service.</param>
        /// <param name="lifetime">Ends the subscription when it terminates; there is no unsubscribe. A
        /// lifetime that has already terminated subscribes nothing, silently.</param>
        /// <param name="listener">
        /// Invoked once per notification, in subscription order relative to other listeners. Must not be
        /// <c>null</c>: it is wrapped in a lambda before it reaches the service, so the service's own
        /// argument check never sees it and a <c>null</c> surfaces as a
        /// <see cref="NullReferenceException"/> from the first window that opens or closes.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="lifetime"/> is <c>null</c>.</exception>
        public static void SubscribeOnChanged(this IUIWindowService service, Lifetime lifetime, Action listener)
            => service.Subscribe(lifetime, (type, kind) => listener());

        /// <summary>
        /// Opens the window registered for <typeparamref name="TPresenter"/>, with no model and no callback.
        /// </summary>
        /// <typeparam name="TPresenter">The window presenter type. Must have been registered with
        /// <c>AddWindow</c>.</typeparam>
        /// <param name="service">The window service.</param>
        /// <returns>A handle that closes the window; see <see cref="UIWindowReference"/> for what is not yet
        /// true of it.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// <typeparamref name="TPresenter"/> is not registered. <see cref="UIWindowService"/> indexes a
        /// dictionary of registrations, so a missing window fails this way rather than with a tailored
        /// message.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <typeparamref name="TPresenter"/> is <c>Presenter</c> itself rather than a subclass of it — the
        /// constraint permits that, the service does not — or the registry returned no registration for it,
        /// which only a substituted <see cref="IUIWindowsProvider"/> can do since the built-in one throws
        /// first.
        /// </exception>
        public static UIWindowReference Open<TPresenter>(this IUIWindowService service)
            where TPresenter : Presenter =>
            service.Open(typeof(TPresenter), null, null);

        /// <summary>
        /// Opens the window registered for <typeparamref name="TPresenter"/> and gives its presenter
        /// <paramref name="model"/>.
        /// </summary>
        /// <remarks>
        /// The constraint is the point: <typeparamref name="TPresenter"/> must be able to hold a
        /// <typeparamref name="TModel"/>, so a mismatched model is a compile error here rather than a cast
        /// failure deep inside the service once the view has loaded. The model is applied immediately before
        /// the view is attached, so the presenter's first render already sees it.
        /// </remarks>
        /// <typeparam name="TPresenter">The window presenter type. Must have been registered.</typeparam>
        /// <typeparam name="TModel">The model type, as declared by the presenter.</typeparam>
        /// <param name="service">The window service.</param>
        /// <param name="model">The model. A <c>null</c> is not passed on: the window still opens, and the
        /// presenter keeps its default model.</param>
        /// <returns>A handle that closes the window.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// <typeparamref name="TPresenter"/> is not registered.
        /// </exception>
        public static UIWindowReference Open<TPresenter, TModel>(this IUIWindowService service, TModel model)
            where TPresenter : Presenter, IPresenterWithModel<TModel>
            where TModel : class =>
            service.Open(typeof(TPresenter), null, model);

        /// <summary>
        /// Opens the window registered for <typeparamref name="TPresenter"/> and runs
        /// <paramref name="onOpen"/> once its presenter and view are wired.
        /// </summary>
        /// <typeparam name="TPresenter">The window presenter type. Must have been registered.</typeparam>
        /// <param name="service">The window service.</param>
        /// <param name="onOpen">
        /// Invoked with the typed presenter after the view is attached and its first render has run — the
        /// one place from which a caller can safely reach into the window. Never invoked if the window is
        /// closed before its view arrives. Must not be <c>null</c>: it is called through a wrapping lambda,
        /// so the service's own null check cannot see it and a <c>null</c> surfaces as a
        /// <see cref="NullReferenceException"/> when the view loads. Use the callback-free overload instead.
        /// </param>
        /// <returns>A handle that closes the window.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// <typeparamref name="TPresenter"/> is not registered.
        /// </exception>
        public static UIWindowReference Open<TPresenter>(this IUIWindowService service, Action<TPresenter> onOpen)
            where TPresenter : Presenter =>
            service.Open(typeof(TPresenter), w => onOpen((TPresenter)w), null);

        /// <summary>
        /// Opens the window registered for <typeparamref name="TPresenter"/> with a model and a callback:
        /// the two overloads above at once.
        /// </summary>
        /// <remarks>
        /// The model is applied first, then the view is attached, then <paramref name="onOpen"/> runs — so
        /// the presenter it receives has already rendered <paramref name="model"/> once. Note the argument
        /// order: the callback comes before the model.
        /// </remarks>
        /// <typeparam name="TPresenter">The window presenter type. Must have been registered.</typeparam>
        /// <typeparam name="TModel">The model type, as declared by the presenter.</typeparam>
        /// <param name="service">The window service.</param>
        /// <param name="onOpen">Invoked with the typed presenter once it is fully wired. Must not be
        /// <c>null</c>; see the callback-only overload for why.</param>
        /// <param name="model">The model. A <c>null</c> is not passed on to the presenter.</param>
        /// <returns>A handle that closes the window.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// <typeparamref name="TPresenter"/> is not registered.
        /// </exception>
        public static UIWindowReference Open<TPresenter, TModel>(this IUIWindowService service, Action<TPresenter> onOpen,
            TModel model)
            where TPresenter : Presenter, IPresenterWithModel<TModel>
            where TModel : class =>
            service.Open(typeof(TPresenter), w => onOpen((TPresenter)w), model);
    }
}
