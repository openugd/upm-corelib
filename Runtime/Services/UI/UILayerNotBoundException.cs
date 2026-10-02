using System;

namespace OpenUGD.Services.UI
{
    /// <summary>
    /// Thrown when a layer is asked for by key and the scene does not bind it.
    /// </summary>
    /// <remarks>
    /// Its own type rather than <see cref="ArgumentException"/> because the mistake is almost never in
    /// the argument: the key is usually a <c>const</c> that is spelled correctly, and what is missing
    /// is a row in the scene's <see cref="TransformProviderComponent"/>. Catching this specifically is
    /// how an editor tool or a boot check can offer to fix the scene.
    /// </remarks>
    public class UILayerNotBoundException : Exception
    {
        /// <summary>Builds the message from the key and what the provider does bind.</summary>
        /// <param name="key">The key that was asked for.</param>
        /// <param name="bound">A human-readable list of the keys the provider offers; may be empty.</param>
        public UILayerNotBoundException(string key, string bound)
            : base(Compose(key, bound))
        {
            Key = key;
        }

        /// <summary>The key that was asked for. May be <c>null</c> or empty, which never matches.</summary>
        public string Key { get; }

        private static string Compose(string key, string bound)
        {
            var asked = string.IsNullOrEmpty(key) ? "an empty key" : "'" + key + "'";
            var has = string.IsNullOrEmpty(bound)
                ? "the provider binds no layers at all"
                : "the provider binds: " + bound;

            return "No UI layer is bound to " + asked + " - " + has +
                   ". Add a row for it to the scene's TransformProviderComponent, or bind an " +
                   "ITransformProvider that answers this key.";
        }
    }
}
