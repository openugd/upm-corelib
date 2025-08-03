using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Windows
{
    public interface IUIWindowService : IResolve
    {
        IEnumerable<UIWindowReference> Queue { get; }
        ReadOnlyCollection<UIWindowReference> Opened { get; }
        UIWindowReference Open(Type type, Action<Widget> onOpen, object model);
        void Subscribe(Lifetime lifetime, Action<Type, UIWindowActionType> listener);
    }
}
