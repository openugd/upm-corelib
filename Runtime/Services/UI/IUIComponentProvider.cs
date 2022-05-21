using System;

namespace OpenUGD.Services.UI
{
    public interface IUIComponentProvider
    {
        void Provide(Lifetime lifetime, string path, Type type, Action<UIComponentProviderContext> onResult);
    }
}
