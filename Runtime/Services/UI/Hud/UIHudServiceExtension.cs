using System;
using OpenUGD.Core.ContextBuilder;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Hud
{
    public static class UIHudServiceExtension
    {
        public static UIHudReference Open<TWidget>(
            this IHudService service,
            Action<TWidget> onOpen = null
        )
            where TWidget : Widget
        {
            return service.Open(typeof(TWidget), null, (w) => onOpen?.Invoke((TWidget)w));
        }

        public static UIHudReference Open<TWidget, TModel>(
            this IHudService service,
            TModel model,
            Action<TWidget> onOpen = null
        )
            where TWidget : Widget, IWidgetWithModel<TModel>
            where TModel : class
        {
            return service.Open(typeof(TWidget), model, (w) => onOpen?.Invoke((TWidget)w));
        }

        public static void AddHudService(this IUIContextServiceSetup setup)
        {
            var service = new UIHudService();
            setup.Injector.ToValue<IUIHudProvider>(service);
            setup.Injector.ToValue<IUIHudRegister>(service);
            setup.AddService(() => service);
        }

        public static void AddHud<TWidget>(
            this IUIContextServiceSetup setup,
            Action<HudOptions> options
        )
            where TWidget : Widget
        {
            setup.Resolve<IUIHudRegister>().AddHud(
                lifetime: setup.Lifetime,
                type: typeof(TWidget),
                options: opt => options(
                    opt.SetInjector(setup.Injector)
                )
            );
        }

        [Obsolete("Use AddHud instead.")]
        public static void RegisterHUD<TWidget>(
            this IUIContextServiceSetup setup,
            string path,
            IUIComponentProvider provider = null
        )
            where TWidget : Widget
            =>
                setup.AddHud<TWidget>(options =>
                {
                    if (provider != null)
                    {
                        options.Path = path;
                        options.Provider = () => provider;
                    }
                    else
                    {
                        options.LoadFromResources(path);
                    }

                    options.Injector = setup.Injector;
                });
    }
}