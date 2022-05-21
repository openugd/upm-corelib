using System;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Windows
{
    public interface IUIWindowService : IResolve
    {
        UIWindowReference[] Opened { get; }
        UIWindowReference Open(Type type, Action<Widget> onOpen, object model);
        void SubscribeOnChanged(Lifetime lifetime, Action listener);
        void SubscribeOnChanged(Lifetime lifetime, Action<Type, UIWindowActionKind> listener);
    }
}
