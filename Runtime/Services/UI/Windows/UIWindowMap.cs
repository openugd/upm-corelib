using System;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Windows
{
    public class UIWindowMap<T> : UIWindowMap where T : Widget
    {
        public UIWindowMap(string path, bool isFullscreen, IUIComponentProvider provider = null) : base(typeof(T), path,
            isFullscreen, provider)
        {
        }
    }

    public class UIWindowMap
    {
        public bool IsFullscreen;
        public string Path;
        public IUIComponentProvider Provider;
        public Type Type;

        public UIWindowMap(Type type, string path, bool isFullscreen, IUIComponentProvider provider = null)
        {
            Type = type;
            Path = path;
            IsFullscreen = isFullscreen;
            Provider = provider ?? new UIWindowComponentProvider();
        }
    }
}
