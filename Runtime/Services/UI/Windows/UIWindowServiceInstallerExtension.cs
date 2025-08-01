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
            var provider = new UIWindowService();
            setup.Injector.ToValue<IUIWindowsProvider>(provider);
            setup.Injector.ToValue<IUIWindowsRegister>(provider);
            setup.AddService<IUIWindowService>(() => provider);
        }

        public static void AddWindow<TWidget>(this IUIContextServiceSetup setup, Action<WindowOptions> options)
            where TWidget : Widget =>
            setup.Resolve<IUIWindowsRegister>().AddWindow(
                lifetime: setup.Lifetime,
                type: typeof(TWidget),
                options: opt => options(
                    opt.SetInjector(setup.Injector)
                )
            );

        [Obsolete("Use AddWindow() instead.")]
        public static void RegisterWindow<TWidget>(this IUIContextServiceSetup setup, string path, bool isFullscreen,
            IUIComponentProvider provider = null)
            where TWidget : Widget =>
            setup.Resolve<IUIWindowsRegister>().Register(typeof(TWidget), path, isFullscreen, provider);
    }
}
