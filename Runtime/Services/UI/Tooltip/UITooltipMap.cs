using System;

namespace OpenUGD.Services.UI.Tooltip
{
    /// <summary>
    /// One tooltip registration: the presenter type, where its view comes from, and what loads it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Produced by <c>ContextBuilder.RegisterTooltip</c> and handed to <c>UITooltipService</c> through
    /// <see cref="IUITooltipProvider.Provide"/>. The service copies these into a type-keyed table once, in
    /// the Initialize boot phase, so a map created after that is never seen and reassigning
    /// <see cref="Type"/> after that re-keys nothing.
    /// </para>
    /// <para>
    /// The fields are public and mutable because this is a plain registry record rather than a settings
    /// object with a fluent API — contrast <c>HudOptions</c>, which the HUD registry configures lazily.
    /// </para>
    /// </remarks>
    public class UITooltipMap
    {
        /// <summary>
        /// Where the view is loaded from, handed to <see cref="Provider"/> as <see cref="Options.Path"/>.
        /// Nothing here validates it: a wrong path fails when the tooltip is opened, not when it is
        /// registered.
        /// </summary>
        public string Path;

        /// <summary>
        /// Loads the view for this tooltip. One instance shared by every open of it: the service re-injects
        /// this same object before each call instead of building a new one, so an implementation must keep
        /// per-open state in locals and closures rather than in fields.
        /// </summary>
        public IUIComponentProvider Provider;

        /// <summary>
        /// The presenter type this registration is keyed by, and the type the service instantiates from the
        /// context when the tooltip opens.
        /// </summary>
        public Type Type;

        /// <summary>Creates a registration.</summary>
        /// <param name="type">The tooltip presenter type.</param>
        /// <param name="path">The resource path of its view.</param>
        /// <param name="provider">What loads the view, or <c>null</c> for a fresh
        /// <see cref="UITooltipComponentProvider"/> — which loads the prefab at <paramref name="path"/>
        /// from <c>Resources</c> and parents it under the layer keyed <see cref="UILayers.Tooltip"/>.</param>
        public UITooltipMap(Type type, string path, IUIComponentProvider provider = null)
        {
            Type = type;
            Path = path;
            Provider = provider ?? new UITooltipComponentProvider();
        }
    }
}
