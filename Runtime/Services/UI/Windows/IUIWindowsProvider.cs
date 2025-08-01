using System;

namespace OpenUGD.Services.UI.Windows
{
    public interface IUIWindowsProvider
    {
        UIWindowFactoryInfo Get(Type type);
    }

    public interface IUIWindowsRegister
    {
        void AddWindow(Lifetime lifetime, Type type, Action<WindowOptions> options);
    }
}
