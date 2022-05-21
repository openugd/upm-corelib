using System;
using UnityEngine.EventSystems;

namespace OpenUGD.Core
{
    public interface IContextInstanceProvider
    {
        EventSystem EventSystem { get; }
        int TargetDisplay { get; }
        bool IsSelected { get; }
        void Subscribe(Lifetime lifetime, Action listener);
    }
}
