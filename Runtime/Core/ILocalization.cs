namespace OpenUGD.Core
{
    /// <summary>
    /// Turns a key into the text to display. An <b>optional</b> service: the presenters that use it take it
    /// with <c>[Inject(Optional = true)]</c> and render the key verbatim when nothing is registered, so a
    /// project with no localisation is a supported configuration rather than a broken one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing in these packages implements it.</b> It is a seam. Register your own under this contract
    /// and <c>TextPresenter</c>, <c>TMPPresenter</c> and <c>HyperlinkPresenterExtensions</c> begin
    /// translating with no other change; leave it unregistered and they show the authored string. Marking
    /// it required with a plain <c>[Inject]</c> would be the wrong trade — an unresolvable required member
    /// fails the whole context build.
    /// </para>
    /// <para>
    /// <b>Pair it with <see cref="ILocalizationChanged"/></b> when the language can change while the game
    /// is running. This interface carries no change notification, so on its own the text a widget rendered
    /// at open time is the text it keeps.
    /// </para>
    /// <para>
    /// No thread-safety is assumed or required: every call site is a presenter render, which runs on
    /// Unity's main thread.
    /// </para>
    /// </remarks>
    public interface ILocalization
    {
        /// <summary>
        /// Returns the text for <paramref name="key"/> in the language currently in effect.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Called on every render</b> of every localised widget — and once more per <i>string</i>
        /// argument for a widget with substitutions, since non-string arguments are substituted as they
        /// are — so it must be a lookup, not a file read or a network call.
        /// </para>
        /// <para>
        /// <b>Return the key itself when the key is unknown.</b> The result goes straight onto the view's
        /// text field, and the two text presenters do not inspect it, so <c>null</c> or <c>""</c> renders
        /// an empty widget and destroys the only clue to which key is missing — and a <c>null</c> returned
        /// for a format string with arguments throws out of <c>string.Format</c> instead. Only the
        /// whole-string overloads of <c>HyperlinkPresenterExtensions</c> fall back to the raw key on a
        /// null-or-empty result; do not rely on that everywhere.
        /// </para>
        /// <para>
        /// The value may double as a <c>string.Format</c> pattern: when a text model carries arguments, its
        /// format string is itself passed through this method and the translated result is what gets the
        /// <c>{0}</c> substitutions. Placeholders must therefore survive translation.
        /// </para>
        /// </remarks>
        /// <param name="key">The lookup key. This is the raw string authored on the model, so it is
        /// whatever the caller wrote — never assume it is a well-formed identifier.</param>
        /// <returns>The text to display.</returns>
        string Get(string key);
    }
}
