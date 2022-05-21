using System;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Windows
{
    public static class UIWindowServiceExtension
    {
        public static void CloseAll(this IUIWindowService service)
        {
            foreach (var reference in service.Opened)
            {
                reference.Close();
            }
        }

        public static UIWindowReference Open<TWidget>(this IUIWindowService service)
            where TWidget : Widget =>
            service.Open(typeof(TWidget), null, null);

        public static UIWindowReference Open<TWidget, TModel>(this IUIWindowService service, TModel model)
            where TWidget : Widget, IWidgetWithModel<TModel>
            where TModel : class =>
            service.Open(typeof(TWidget), null, model);

        public static UIWindowReference Open<TWidget>(this IUIWindowService service, Action<TWidget> onOpen)
            where TWidget : Widget =>
            service.Open(typeof(TWidget), w => onOpen((TWidget)w), null);

        public static UIWindowReference Open<TWidget, TModel>(this IUIWindowService service, Action<TWidget> onOpen,
            TModel model)
            where TWidget : Widget, IWidgetWithModel<TModel>
            where TModel : class =>
            service.Open(typeof(TWidget), w => onOpen((TWidget)w), model);
    }
}
