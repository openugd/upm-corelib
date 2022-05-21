using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenUGD.Services.UI.Tooltip
{
    [RequireComponent(typeof(RectTransform))]
    public class UITooltipComponent : MonoBehaviour, IUITooltip, IPointerEnterHandler, IPointerExitHandler,
        IPointerMoveHandler
    {
        private readonly Lifetime.Definition _def = Lifetime.Eternal.DefineNested();
        private Signal<PointerEventData> _onEnter;
        private Signal<PointerEventData> _onExit;
        private Signal<PointerEventData> _onMove;

        private void OnDestroy() => _def.Terminate();

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData) => _onEnter?.Fire(eventData);

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData) => _onExit?.Fire(eventData);

        void IPointerMoveHandler.OnPointerMove(PointerEventData eventData) => _onMove?.Fire(eventData);

        public void SubscribeOnEnter(Lifetime lifetime, Action<PointerEventData> listener)
        {
            if (_onEnter == null)
            {
                _onEnter = new Signal<PointerEventData>(_def.Lifetime);
            }

            _onEnter.Subscribe(lifetime, listener);
        }

        public void SubscribeOnExit(Lifetime lifetime, Action<PointerEventData> listener)
        {
            if (_onExit == null)
            {
                _onExit = new Signal<PointerEventData>(_def.Lifetime);
            }

            _onExit.Subscribe(lifetime, listener);
        }

        public void SubscribeOnMove(Lifetime lifetime, Action<PointerEventData> listener)
        {
            if (_onMove == null)
            {
                _onMove = new Signal<PointerEventData>(_def.Lifetime);
            }

            _onMove.Subscribe(lifetime, listener);
        }
    }
}
