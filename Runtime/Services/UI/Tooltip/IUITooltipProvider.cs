using System;
using System.Collections.Generic;

namespace OpenUGD.Services.UI.Tooltip
{
    public interface IUITooltipProvider
    {
        IEnumerable<UITooltipMap> Provide();
    }

    public interface IUITooltipRegister
    {
        void Register(Type type, string path, IUIComponentProvider provider);
    }
}
