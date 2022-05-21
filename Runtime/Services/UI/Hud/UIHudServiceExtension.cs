using System;
using System.Collections.Generic;
using OpenUGD.Core.ContextBuilder;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Hud
{
    public static class UIHudServiceExtension
    {
        public static void AddHudService(this IUIContextServiceSetup setup)
        {
            var provider = new HudProvider();
            setup.Injector.ToValue<IUIHudProvider>(provider);
            setup.Injector.ToValue<IUIHudRegister>(provider);
            setup.AddService(() => new UIHudService());
        }

        public static void RegisterHUD<TWidget>(this IUIContextServiceSetup setup, string path,
            IUIComponentProvider provider = null) where TWidget : Widget =>
            setup.Resolve<IUIHudRegister>().Register(typeof(TWidget), path, provider);


        private class HudProvider : IUIHudProvider, IUIHudRegister
        {
            private readonly List<UIHudMap> _list = new();

            public IEnumerable<UIHudMap> Provide() => _list;

            public void Register(Type type, string path, IUIComponentProvider provider) =>
                _list.Add(new UIHudMap(type, path, provider));
        }
    }
}
