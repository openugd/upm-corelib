using System;
using System.Collections.Generic;

namespace OpenUGD.Services.UI.Hud
{
    public interface IUIHudProvider
    {
        IEnumerable<UIHudMap> Provide();
    }

    public interface IUIHudRegister
    {
        void Register(Type type, string path, IUIComponentProvider provider);
    }
}
