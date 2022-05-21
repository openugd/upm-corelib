using System;

namespace OpenUGD.Core.Widgets
{
    public static class WidgetExtensions
    {
        public static Type GetViewType(this Widget widget)
        {
            var view = widget as IWidgetWithView;
            if (view != null)
            {
                return view.ViewType;
            }

            return null;
        }
    }
}
