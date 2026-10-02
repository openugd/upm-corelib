using System;

namespace OpenUGD.Core
{
    /// <summary>
    /// Announces that the language changed, so anything already on screen can be re-rendered. An
    /// <b>optional</b> companion to <see cref="ILocalization"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both localisation services are taken with <c>[Inject(Optional = true)]</c>, and they are independent:
    /// registering <see cref="ILocalization"/> without this one gives translation that never refreshes —
    /// exactly right for a game whose language is chosen at launch — and registering this one without
    /// <see cref="ILocalization"/> refreshes widgets that have nothing to re-translate. A project that
    /// registers neither still builds and still runs.
    /// </para>
    /// <para>
    /// <b>Nothing in these packages implements it.</b> Raise it yourself from whatever owns the language
    /// setting, and raise it <i>after</i> the switch has taken effect: listeners re-read
    /// <see cref="ILocalization.Get"/> synchronously from inside the callback, so the new language must
    /// already be in place when they run.
    /// </para>
    /// </remarks>
    public interface ILocalizationChanged
    {
        /// <summary>
        /// Registers <paramref name="listener"/> to run on every language change, until
        /// <paramref name="lifetime"/> terminates.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>There is no unsubscribe, because the scope is the unsubscribe.</b> To detach earlier than the
        /// subscriber's natural end, subscribe on a <c>Lifetime.DefineNested</c> definition and terminate
        /// that. This is the same shape as <c>Signal.Subscribe</c>, which is what an implementation will
        /// almost certainly delegate to.
        /// </para>
        /// <para>
        /// <b>Nothing is invoked on subscription.</b> A listener only sees changes that happen after it
        /// registers, so render once yourself as well — which is what the text presenters do, by rendering
        /// from <c>OnViewAdded</c> and <c>OnRefresh</c> rather than from the callback alone.
        /// </para>
        /// <para>
        /// Subscribing on an already-terminated <paramref name="lifetime"/> should register nothing and
        /// return quietly rather than throw; callers treat this as fire-and-forget.
        /// </para>
        /// </remarks>
        /// <param name="lifetime">The <i>subscriber's</i> scope, not the service's. A presenter passes its
        /// own <c>Lifetime</c>, so the subscription dies with the widget it refreshes.</param>
        /// <param name="listener">Invoked with no arguments; read the new text from
        /// <see cref="ILocalization"/> inside it.</param>
        /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
        void Subscribe(Lifetime lifetime, Action listener);
    }
}
