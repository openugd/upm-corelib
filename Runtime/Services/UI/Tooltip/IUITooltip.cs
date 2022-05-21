using System;
using UnityEngine.EventSystems;

namespace OpenUGD.Services.UI.Tooltip
{
    public interface IUITooltip
    {
        void SubscribeOnEnter(Lifetime lifetime, Action<PointerEventData> listener);
        void SubscribeOnExit(Lifetime lifetime, Action<PointerEventData> listener);
        void SubscribeOnMove(Lifetime lifetime, Action<PointerEventData> listener);
    }
}
