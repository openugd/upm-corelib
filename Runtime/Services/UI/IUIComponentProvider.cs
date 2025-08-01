using System;

namespace OpenUGD.Services.UI
{
    public interface IUIComponentProvider
    {
        void Provide(
            Lifetime lifetime,
            Options options,
            Type targetType,
            Action<UIComponentProviderContext> onResult
        );
    }
}
