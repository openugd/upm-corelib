using System;
using System.Collections.Generic;

namespace OpenUGD.Services.UI.Windows
{
    public interface IUIWindowsProvider
    {
        IEnumerable<UIWindowMap> Provide();
    }

    public interface IUIWindowsRegister
    {
        void Register(Type type, string path, bool isFullscreen, IUIComponentProvider provider);
    }
}
