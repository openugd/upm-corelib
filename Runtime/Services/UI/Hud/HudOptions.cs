using System;

namespace OpenUGD.Services.UI.Hud
{
    /// <summary>Registration settings for one HUD presenter.</summary>
    public class HudOptions : Options
    {
        private static readonly Func<IUIComponentProvider> DefaultProvider = () => new UIHudComponentProvider();

        /// <summary>Creates options that load the view from <c>Resources</c>.</summary>
        public HudOptions() => SetProvider(DefaultProvider);

        /// <summary>Uses the default <c>Resources</c>-backed provider.</summary>
        /// <returns>This instance, for chaining.</returns>
        public HudOptions LoadFromResources() => SetProvider(DefaultProvider);

        /// <summary>Uses the default <c>Resources</c>-backed provider at <paramref name="path"/>.</summary>
        /// <param name="path">The resource path.</param>
        /// <returns>This instance, for chaining.</returns>
        public HudOptions LoadFromResources(string path) => SetPath(path).SetProvider(DefaultProvider);

        /// <inheritdoc cref="Options.SetPath" />
        public new HudOptions SetPath(string path) => (HudOptions)base.SetPath(path);

        /// <inheritdoc cref="Options.SetProvider" />
        public new HudOptions SetProvider(Func<IUIComponentProvider> provider) =>
            (HudOptions)base.SetProvider(provider);

        /// <inheritdoc cref="Options.SetContext" />
        public new HudOptions SetContext(OpenUGD.Context context) => (HudOptions)base.SetContext(context);
    }
}
