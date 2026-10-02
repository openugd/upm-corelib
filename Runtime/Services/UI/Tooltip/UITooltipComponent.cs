using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OpenUGD.Services.UI.Tooltip
{
    /// <summary>
    /// The stock <see cref="IUITooltip"/> view: a behaviour on a <see cref="RectTransform"/> that turns
    /// Unity's pointer events into signals a presenter can subscribe to for a scope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the component a tooltip prefab carries: the default
    /// <see cref="UITooltipComponentProvider"/> instantiates that prefab and the service hands the result to
    /// the tooltip presenter as its view, which is why the pointer events reported here are the ones over
    /// the tooltip itself rather than over whatever triggered it. The three
    /// <c>IPointer*Handler</c> interfaces are implemented explicitly, so they are off this class's own
    /// surface: raising one by hand takes a cast to the interface first.
    /// </para>
    /// <para>
    /// <b>Signals are created on first subscription.</b> A component nobody has subscribed to allocates no
    /// signal and drops the pointer events it receives. There is no buffering of any kind: a subscriber
    /// sees only what happens after it subscribes, so subscribe in <c>OnViewAdded</c> rather than waiting
    /// for a first render.
    /// </para>
    /// <para>
    /// <b>Teardown.</b> The signals belong to a scope nested in <see cref="Lifetime.Eternal"/> that this
    /// component terminates in <c>OnDestroy</c>, so destroying the <c>GameObject</c> detaches every
    /// subscriber whatever scope each of them passed in. Terminating that scope also unregisters it from
    /// <see cref="Lifetime.Eternal"/>, so a scene full of destroyed tooltips leaves nothing behind.
    /// </para>
    /// </remarks>
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

        /// <summary>
        /// Subscribes <paramref name="listener"/> to pointer-enter for as long as
        /// <paramref name="lifetime"/> is alive.
        /// </summary>
        /// <remarks>
        /// The first call allocates the backing signal; later ones reuse it. The subscription also ends
        /// when this component is destroyed, so a listener bound to a longer-lived scope is still detached
        /// with the view rather than left holding it — and a call made after destruction registers nothing,
        /// silently, because the underlying signal's truthful result is not surfaced here.
        /// </remarks>
        /// <param name="lifetime">The subscriber's scope; the listener is detached when it ends.</param>
        /// <param name="listener">Invoked with Unity's <see cref="PointerEventData"/> on each enter.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="lifetime"/> or <paramref name="listener"/> is <c>null</c>.
        /// </exception>
        public void SubscribeOnEnter(Lifetime lifetime, Action<PointerEventData> listener)
        {
            if (_onEnter == null)
            {
                _onEnter = new Signal<PointerEventData>(_def.Lifetime);
            }

            _onEnter.Subscribe(lifetime, listener);
        }

        /// <summary>
        /// Subscribes <paramref name="listener"/> to pointer-exit for as long as
        /// <paramref name="lifetime"/> is alive.
        /// </summary>
        /// <remarks>
        /// Backed by its own signal, allocated on the first call here — subscribing to enter does not arm
        /// exit. An exit is not the mirror of an enter: this component detaches every subscriber in
        /// <c>OnDestroy</c>, so an exit that Unity has not delivered by the time the tooltip is destroyed
        /// is never delivered at all. Release whatever an enter acquired on
        /// <paramref name="lifetime"/> instead of here.
        /// </remarks>
        /// <param name="lifetime">The subscriber's scope; the listener is detached when it ends.</param>
        /// <param name="listener">Invoked with Unity's <see cref="PointerEventData"/> on each exit.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="lifetime"/> or <paramref name="listener"/> is <c>null</c>.
        /// </exception>
        public void SubscribeOnExit(Lifetime lifetime, Action<PointerEventData> listener)
        {
            if (_onExit == null)
            {
                _onExit = new Signal<PointerEventData>(_def.Lifetime);
            }

            _onExit.Subscribe(lifetime, listener);
        }

        /// <summary>
        /// Subscribes <paramref name="listener"/> to pointer-move for as long as
        /// <paramref name="lifetime"/> is alive.
        /// </summary>
        /// <remarks>
        /// The high-frequency one: it fires on every frame in which the pointer moves over this component,
        /// so keep the listener cheap and do not allocate in it. Subscribing costs one array rebuild;
        /// dispatch after that allocates nothing and takes no lock.
        /// </remarks>
        /// <param name="lifetime">The subscriber's scope; the listener is detached when it ends.</param>
        /// <param name="listener">Invoked with Unity's <see cref="PointerEventData"/> on each move.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="lifetime"/> or <paramref name="listener"/> is <c>null</c>.
        /// </exception>
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
