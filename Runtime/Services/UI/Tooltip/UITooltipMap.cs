using System;

namespace OpenUGD.Services.UI.Tooltip
{
    public class UITooltipMap
    {
        public string Path;
        public IUIComponentProvider Provider;
        public Type Type;

        public UITooltipMap(Type type, string path, IUIComponentProvider provider = null)
        {
            Type = type;
            Path = path;
            Provider = provider ?? new UITooltipComponentProvider();
        }
    }
}
