// © 2025 OpenUGD

using System;

namespace OpenUGD.Services.UI.Windows
{
    public class WindowOptions : Options
    {
        private static readonly Func<IUIComponentProvider> DefaultProvider = () => new UIWindowComponentProvider();

        public static class OptionsKeys
        {
            public const string Fullscreen = nameof(Fullscreen);
        }

        public WindowOptions() => SetProvider(DefaultProvider);

        public bool Fullscreen {
            get {
                return TryGetValue(OptionsKeys.Fullscreen, out var value)
                       && value is bool
                       && (bool)value;
            }
            set => this[OptionsKeys.Fullscreen] = value;
        }

        public WindowOptions SetFullscreen(bool isFullscreen = true)
        {
            this[OptionsKeys.Fullscreen] = isFullscreen;
            return this;
        }

        public WindowOptions LoadFromResources() => SetProvider(DefaultProvider);

        public WindowOptions LoadFromResources(string path)
            => SetPath(path).SetProvider(DefaultProvider);

        public new WindowOptions SetPath(string path)
            => (WindowOptions)base.SetPath(path);

        public new WindowOptions SetProvider(Func<IUIComponentProvider> provider)
            => (WindowOptions)base.SetProvider(provider);

        public new WindowOptions SetInjector(IInjector injector)
            => (WindowOptions)base.SetInjector(injector);
    }
}
