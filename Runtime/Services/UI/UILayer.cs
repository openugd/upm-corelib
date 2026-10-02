using System;
using UnityEngine;

namespace OpenUGD.Services.UI
{
    /// <summary>
    /// One named parent in the scene's UI stack: a key application code asks for, and the
    /// <see cref="RectTransform"/> that answers it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A layer carries no behaviour. It is a transform plus a promise about what sits above and below
    /// it, and the promise is kept by the scene's sibling order, because inside one canvas Unity draws
    /// siblings in hierarchy order. <see cref="ITransformProvider.Layers"/> is ordered back to front to
    /// describe that order; nothing here enforces it, deliberately — draw order is a decision about the
    /// scene, and a component that reordered the hierarchy behind the designer's back would be worse
    /// than one that reports what is there.
    /// </para>
    /// <para>
    /// Unity-serializable, so a scene component can expose a list of these directly rather than one
    /// serialized field per layer.
    /// </para>
    /// </remarks>
    [Serializable]
    public struct UILayer
    {
        [SerializeField] private string _key;
        [SerializeField] private RectTransform _transform;

        /// <summary>Creates a binding between a key and the transform that answers it.</summary>
        /// <param name="key">The lookup key. Compared with ordinal equality, so case matters.</param>
        /// <param name="transform">The parent views of this layer are instantiated under.</param>
        public UILayer(string key, RectTransform transform)
        {
            _key = key;
            _transform = transform;
        }

        /// <summary>
        /// The lookup key, as passed to <see cref="ITransformProvider.TryGetLayer"/>. May be
        /// <c>null</c> or empty on a row a designer has not filled in yet; such a row never matches.
        /// </summary>
        public string Key => _key;

        /// <summary>
        /// The parent transform. May be <c>null</c> — an unfilled inspector slot, or a transform Unity
        /// has already destroyed, which its <c>operator ==</c> also reports as <c>null</c>. A layer in
        /// that state is treated as unbound rather than as a layer whose parent is the scene root.
        /// </summary>
        public RectTransform Transform => _transform;
    }

    /// <summary>
    /// The keys of the layers this package knows about. Nothing privileges them: they are ordinary
    /// strings, and a project or a third-party package declares its own the same way.
    /// </summary>
    /// <remarks>
    /// Listed back to front, which is the order <see cref="TransformProviderComponent"/> ships them in
    /// and the order the matching accessors in <c>TransformProviderExtensions</c> are documented in.
    /// Only <see cref="Hud"/>, <see cref="Window"/> and <see cref="Tooltip"/> are read by this package;
    /// the rest are conventions, and a scene that binds none of them is a valid scene.
    /// </remarks>
    public static class UILayers
    {
        /// <summary>Full-screen art behind all interface, never interacted with.</summary>
        public const string Background = "background";

        /// <summary>Always-on gameplay interface: health bars, counters, minimaps.</summary>
        public const string Hud = "hud";

        /// <summary>Screens and dialogs the player navigated to: inventory, settings, a shop.</summary>
        public const string Window = "window";

        /// <summary>Modal interruptions over a window: confirmations, errors, reward pop-ups.</summary>
        public const string Popup = "popup";

        /// <summary>Short-lived hints anchored to something on screen: tooltips, callouts.</summary>
        public const string Tooltip = "tooltip";

        /// <summary>Full-screen effects over the whole interface: fades, veils, input blockers.</summary>
        public const string Overlay = "overlay";

        /// <summary>Boot and hand-off screens that own the display: splash, curtains, update walls.</summary>
        public const string Splash = "splash";

        /// <summary>Last resort, kept empty: connection-lost banners, fatal errors, a debug console.</summary>
        public const string System = "system";
    }
}
