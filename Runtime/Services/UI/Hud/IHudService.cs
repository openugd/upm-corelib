using System;
using System.Collections.ObjectModel;
using OpenUGD.Core.Widgets;

namespace OpenUGD.Services.UI.Hud
{
#pragma warning disable CS0649
    public interface IHudService
    {
        ReadOnlyCollection<UIHudReference> Opened { get; }
        T Find<T>() where T : Widget;
        UIHudReference Open(Type type, object model = null, Action<Widget> onOpen = null);
        void Subscribe(Lifetime lifetime, Action<Type, UIHudActionType> listener);
    }
}