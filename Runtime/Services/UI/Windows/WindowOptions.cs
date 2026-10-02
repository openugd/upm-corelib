// © 2025 OpenUGD

using System;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>Registration settings for one window presenter.</summary>
    public class WindowOptions : Options
    {
        private static readonly Func<IUIComponentProvider> DefaultProvider = () => new UIWindowComponentProvider();

        /// <summary>The dictionary keys this type adds.</summary>
        public new static class OptionsKeys
        {
            /// <summary>Key for <see cref="WindowOptions.Fullscreen"/>.</summary>
            public const string Fullscreen = nameof(Fullscreen);
        }

        /// <summary>Creates options that load the view from <c>Resources</c>.</summary>
        public WindowOptions() => SetProvider(DefaultProvider);

        /// <summary>Whether the window covers the screen. Defaults to <see langword="false"/>.</summary>
        public bool Fullscreen {
            get {
                object value;
                return TryGetValue(OptionsKeys.Fullscreen, out value) && value is bool && (bool)value;
            }
            set => this[OptionsKeys.Fullscreen] = value;
        }

        /// <summary>Sets <see cref="Fullscreen"/>.</summary>
        /// <param name="isFullscreen">The value.</param>
        /// <returns>This instance, for chaining.</returns>
        public WindowOptions SetFullscreen(bool isFullscreen = true)
        {
            this[OptionsKeys.Fullscreen] = isFullscreen;
            return this;
        }

        /// <summary>Uses the default <c>Resources</c>-backed provider.</summary>
        /// <returns>This instance, for chaining.</returns>
        public WindowOptions LoadFromResources() => SetProvider(DefaultProvider);

        /// <summary>Uses the default <c>Resources</c>-backed provider at <paramref name="path"/>.</summary>
        /// <param name="path">The resource path.</param>
        /// <returns>This instance, for chaining.</returns>
        public WindowOptions LoadFromResources(string path) => SetPath(path).SetProvider(DefaultProvider);

        /// <inheritdoc cref="Options.SetPath" />
        public new WindowOptions SetPath(string path) => (WindowOptions)base.SetPath(path);

        /// <inheritdoc cref="Options.SetProvider" />
        public new WindowOptions SetProvider(Func<IUIComponentProvider> provider) =>
            (WindowOptions)base.SetProvider(provider);

        /// <inheritdoc cref="Options.SetContext" />
        public new WindowOptions SetContext(OpenUGD.Context context) => (WindowOptions)base.SetContext(context);
    }
}
