using System;
using System.Collections.Generic;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Windows
{
    public interface IUIWindowService : IResolve
    {
        IEnumerable<UIWindowReference> Queue { get; }
        IEnumerable<UIWindowReference> Opened { get; }
        UIWindowReference Open(Type type, Action<Widget> onOpen, object model);
        void SubscribeOnChanged(Lifetime lifetime, Action<Type, UIWindowActionKind> listener);
    }
}
