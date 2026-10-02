using System.Collections.Generic;
using UnityEngine;

namespace OpenUGD.Services.UI
{
    /// <summary>
    /// The scene's UI stack, seen from code: one canvas, its camera, a parking transform for pooled
    /// views, and any number of named layers a provider parents its views under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it is for.</b> A view has to be parented somewhere, and "somewhere" is a decision about
    /// the scene, not about the presenter that renders into it. This interface is the seam: the scene
    /// answers it (see <see cref="TransformProviderComponent"/>), an <see cref="IUIComponentProvider"/>
    /// picks a layer off it, and no presenter ever names a transform. Bind a different implementation
    /// and every built-in provider follows it without a single presenter changing.
    /// </para>
    /// <para>
    /// <b>Layers are open-ended.</b> There are four members here, not eleven. The names this package
    /// uses — <c>Hud</c>, <c>Window</c>, <c>Tooltip</c> and five conventions beside them — are
    /// extension methods over <see cref="TryGetLayer"/> in <c>TransformProviderExtensions</c>, with
    /// their keys in <see cref="UILayers"/>. A project or a third-party package adds a layer exactly
    /// the same way, and nothing in this package is privileged:
    /// <code>
    /// public static class MinimapLayer
    /// {
    ///     public const string Key = "minimap";
    ///     public static RectTransform Minimap(this ITransformProvider provider) =&gt; provider.Layer(Key);
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// <b>Changed in 2.0.0.</b> This used to declare eight fixed <see cref="RectTransform"/> properties,
    /// of which five had no caller anywhere. Those five were not an extension point — an extension
    /// method has nowhere to store a transform, so a ninth layer meant editing this interface and
    /// breaking every implementation. They were spare slots shipped in the hope that eight would be
    /// enough. An implementation now answers four members instead of eleven, which also makes a test
    /// double trivial.
    /// </para>
    /// </remarks>
    public interface ITransformProvider
    {
        /// <summary>
        /// The canvas the layers live under. Read it when a view has to reason about the surface it is
        /// drawn on — render mode, scale factor, <c>sortingOrder</c>. Nothing in this package does.
        /// </summary>
        /// <remarks>
        /// Not a parent: views go under the layer transforms, never directly under this, and nothing
        /// checks that the layers are in fact its descendants.
        /// </remarks>
        Canvas Canvas { get; }

        /// <summary>
        /// The camera to pass to screen/world conversions against <see cref="Canvas"/>, such as
        /// <c>RectTransformUtility.ScreenPointToLocalPointInRectangle</c>.
        /// </summary>
        /// <remarks>
        /// Expected to be <c>null</c> for a Screen Space - Overlay canvas, which is exactly the argument
        /// those conversions want in that mode, so <c>null</c> here is not by itself a wiring mistake.
        /// Nothing in this package reads it.
        /// </remarks>
        Camera Camera { get; }

        /// <summary>
        /// Where a pooled view is parked instead of being destroyed. <c>UIWindowComponentProvider</c>
        /// reparents a closed window here when it was constructed with pooling enabled.
        /// </summary>
        /// <remarks>
        /// Reparenting is the whole operation — nothing deactivates the view on the way in — so this
        /// transform must itself be inactive, or outside <see cref="Canvas"/>, or every parked view
        /// keeps rendering. Deliberately a plain <see cref="Transform"/> and not a
        /// <see cref="RectTransform"/>: nothing under it is laid out or drawn, so it need not live in
        /// the canvas at all.
        /// </remarks>
        Transform Pool { get; }

        /// <summary>
        /// Every layer this provider offers, ordered back to front — the first entry is drawn behind
        /// the rest. Enumerate it to discover what a scene actually binds, which is what an editor tool
        /// or a diagnostic needs; use <see cref="TryGetLayer"/> to look one up by key.
        /// </summary>
        /// <remarks>
        /// The order is a description of the scene's sibling order, not an instruction to it. Nothing
        /// reorders the hierarchy to match, because draw order belongs to whoever built the scene.
        /// May be empty; a scene that binds no layers is valid, and every lookup then fails loudly.
        /// </remarks>
        IReadOnlyList<UILayer> Layers { get; }

        /// <summary>
        /// Looks a layer up by key, reporting absence rather than throwing.
        /// </summary>
        /// <param name="key">
        /// A key from <see cref="UILayers"/> or declared by the caller. Compared with ordinal equality,
        /// so case matters. A <c>null</c> or empty key matches nothing.
        /// </param>
        /// <param name="layer">The bound transform, or <c>null</c> when this returns <c>false</c>.</param>
        /// <returns>
        /// <c>true</c> only when the key is bound to a transform that still exists. A row whose
        /// transform was left empty in the inspector, or whose transform Unity has since destroyed,
        /// reports <c>false</c> — an unbound layer, never a layer whose parent is the scene root.
        /// </returns>
        bool TryGetLayer(string key, out RectTransform layer);
    }
}
