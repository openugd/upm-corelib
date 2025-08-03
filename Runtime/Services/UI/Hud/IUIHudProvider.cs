using System;
using System.Collections.Generic;

namespace OpenUGD.Services.UI.Hud
{
    public interface IUIHudProvider
    {
        UIHudFactoryInfo Get(Type type);
    }

    public interface IUIHudRegister
    {
        void AddHud(Lifetime lifetime, Type type, Action<HudOptions> options);
    }
}