using System;
using System.Collections.Generic;
using OpenUGD.Core.ContextBuilder;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Windows
{
    public static class UIWindowServiceInstallerExtension
    {
        public static void AddWindowsService(this IUIContextServiceSetup setup)
        {
            var provider = new WindowsProvider();
            setup.Injector.ToValue<IUIWindowsProvider>(provider);
            setup.Injector.ToValue<IUIWindowsRegister>(provider);

            setup.AddService<IUIWindowService>(() => new UIWindowService());
        }

        public static void RegisterWindow<TWidget>(this IUIContextServiceSetup setup, string path, bool isFullscreen,
            IUIComponentProvider provider = null)
            where TWidget : Widget =>
            setup.Resolve<IUIWindowsRegister>().Register(typeof(TWidget), path, isFullscreen, provider);

        private class WindowsProvider : IUIWindowsProvider, IUIWindowsRegister
        {
            private readonly List<UIWindowMap> _list = new();

            public IEnumerable<UIWindowMap> Provide() => _list;

            public void Register(Type type, string path, bool isFullscreen, IUIComponentProvider provider) =>
                _list.Add(new UIWindowMap(type, path, isFullscreen, provider));
        }
    }
}
