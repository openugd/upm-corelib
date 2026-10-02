using System.Text;
using UnityEngine;

namespace OpenUGD.Services.UI
{
    /// <summary>
    /// Layer lookups over <see cref="ITransformProvider"/>: the throwing form of
    /// <see cref="ITransformProvider.TryGetLayer"/>, and one accessor per key in
    /// <see cref="UILayers"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here is an extension method, and that is the point: the eight layers this package
    /// names have no more standing than one a project declares for itself. Copy the shape of any
    /// accessor below, put the key in a <c>const</c> beside it, and the new layer is reached exactly
    /// like the built-in ones.
    /// </para>
    /// <para>
    /// These are methods rather than properties because C# has no extension properties; <c>p.Hud()</c>
    /// rather than <c>p.Hud</c> is the whole ergonomic cost of the layers being open-ended.
    /// </para>
    /// </remarks>
    public static class TransformProviderExtensions
    {
        /// <summary>
        /// Returns the layer bound to <paramref name="key"/>, or throws naming what the scene does bind.
        /// </summary>
        /// <param name="provider">The provider to look in.</param>
        /// <param name="key">A key from <see cref="UILayers"/>, or one the caller declared.</param>
        /// <returns>The bound transform; never <c>null</c>.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
        /// <exception cref="UILayerNotBoundException">
        /// Nothing is bound to <paramref name="key"/>, or the bound transform has been destroyed.
        /// </exception>
        /// <remarks>
        /// Use this when the caller cannot proceed without the layer, which is every built-in provider.
        /// Before 2.0.0 a missing layer produced a <c>null</c> parent, and the providers read that as
        /// "scene root" and called <c>DontDestroyOnLoad</c> on the view — so a layer forgotten in the
        /// scene surfaced as interface drawn in the wrong place that also outlived every scene load,
        /// never as an error where the mistake was. Prefer
        /// <see cref="ITransformProvider.TryGetLayer"/> where absence is a real option.
        /// </remarks>
        public static RectTransform Layer(this ITransformProvider provider, string key)
        {
            if (provider == null) throw new System.ArgumentNullException(nameof(provider));

            RectTransform layer;
            if (provider.TryGetLayer(key, out layer)) return layer;

            throw new UILayerNotBoundException(key, Describe(provider));
        }

        /// <summary>Full-screen art behind all interface. See <see cref="UILayers.Background"/>.</summary>
        /// <param name="provider">The provider to look in.</param>
        /// <exception cref="UILayerNotBoundException">The scene does not bind this layer.</exception>
        public static RectTransform Background(this ITransformProvider provider) =>
            provider.Layer(UILayers.Background);

        /// <summary>Always-on gameplay interface. See <see cref="UILayers.Hud"/>.</summary>
        /// <param name="provider">The provider to look in.</param>
        /// <exception cref="UILayerNotBoundException">The scene does not bind this layer.</exception>
        public static RectTransform Hud(this ITransformProvider provider) =>
            provider.Layer(UILayers.Hud);

        /// <summary>Screens and dialogs the player navigated to. See <see cref="UILayers.Window"/>.</summary>
        /// <param name="provider">The provider to look in.</param>
        /// <exception cref="UILayerNotBoundException">The scene does not bind this layer.</exception>
        public static RectTransform Window(this ITransformProvider provider) =>
            provider.Layer(UILayers.Window);

        /// <summary>Modal interruptions over a window. See <see cref="UILayers.Popup"/>.</summary>
        /// <param name="provider">The provider to look in.</param>
        /// <exception cref="UILayerNotBoundException">The scene does not bind this layer.</exception>
        public static RectTransform Popup(this ITransformProvider provider) =>
            provider.Layer(UILayers.Popup);

        /// <summary>Short-lived anchored hints. See <see cref="UILayers.Tooltip"/>.</summary>
        /// <param name="provider">The provider to look in.</param>
        /// <exception cref="UILayerNotBoundException">The scene does not bind this layer.</exception>
        public static RectTransform Tooltip(this ITransformProvider provider) =>
            provider.Layer(UILayers.Tooltip);

        /// <summary>Full-screen effects over the interface. See <see cref="UILayers.Overlay"/>.</summary>
        /// <param name="provider">The provider to look in.</param>
        /// <exception cref="UILayerNotBoundException">The scene does not bind this layer.</exception>
        public static RectTransform Overlay(this ITransformProvider provider) =>
            provider.Layer(UILayers.Overlay);

        /// <summary>Boot and hand-off screens. See <see cref="UILayers.Splash"/>.</summary>
        /// <param name="provider">The provider to look in.</param>
        /// <exception cref="UILayerNotBoundException">The scene does not bind this layer.</exception>
        public static RectTransform Splash(this ITransformProvider provider) =>
            provider.Layer(UILayers.Splash);

        /// <summary>The last-resort top layer. See <see cref="UILayers.System"/>.</summary>
        /// <param name="provider">The provider to look in.</param>
        /// <exception cref="UILayerNotBoundException">The scene does not bind this layer.</exception>
        public static RectTransform System(this ITransformProvider provider) =>
            provider.Layer(UILayers.System);

        /// Lists what the provider does bind, so the exception can say what to fix rather than only
        /// what failed.
        private static string Describe(ITransformProvider provider)
        {
            var layers = provider.Layers;
            if (layers == null || layers.Count == 0) return "";

            var builder = new StringBuilder();
            for (var i = 0; i < layers.Count; i++)
            {
                if (builder.Length != 0) builder.Append(", ");
                builder.Append('\'').Append(layers[i].Key).Append('\'');
                if (layers[i].Transform == null) builder.Append(" (transform not set)");
            }

            return builder.ToString();
        }
    }
}
