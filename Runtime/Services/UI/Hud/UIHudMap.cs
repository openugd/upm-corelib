using System;

namespace OpenUGD.Services.UI.Hud
{
    public class UIHudMap
    {
        public string Path;
        public IUIComponentProvider Provider;
        public Type Type;

        public UIHudMap(Type type, string path, IUIComponentProvider provider = null)
        {
            Type = type;
            Path = path;
            Provider = provider ?? new UIHudComponentProvider();
        }
    }
}
