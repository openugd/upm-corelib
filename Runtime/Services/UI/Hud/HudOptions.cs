using System;

namespace OpenUGD.Services.UI.Hud
{
    public class HudOptions : Options
    {
        private static readonly Func<IUIComponentProvider> DefaultProvider = () => new UIHudComponentProvider();

        public HudOptions() => SetProvider(DefaultProvider);

        public HudOptions LoadFromResources() => SetProvider(DefaultProvider);

        public HudOptions LoadFromResources(string path)
            => SetPath(path).SetProvider(DefaultProvider);

        public new HudOptions SetPath(string path)
            => (HudOptions)base.SetPath(path);

        public new HudOptions SetProvider(Func<IUIComponentProvider> provider)
            => (HudOptions)base.SetProvider(provider);

        public new HudOptions SetInjector(IInjector injector)
            => (HudOptions)base.SetInjector(injector);
    }
}