using System;
using OpenUGD.Core.Presenters;
using UnityEngine.EventSystems;

namespace OpenUGD.Services.UI.Tooltip
{
    /// <summary>
    /// A pointer-aware view contract. Despite the name this is not a service — it is the <c>TView</c> of
    /// <c>Presenter&lt;IUITooltip, TModel&gt;</c>, and it sits alongside <c>Slider</c> and <c>Image</c>, not
    /// alongside a UI service. <c>UITooltipComponent</c> is its only implementation today.
    /// </summary>
    /// <remarks>
    /// This is the one place in the codebase where an interface - rather than a component - is used as a
    /// view, and that must keep working: a future non-Unity view (a native WebGL popup) is the same case. It
    /// is also why <see cref="Presenter{TView}"/> puts no constraint on <c>TView</c>: a constraint tight
    /// enough to demand liveness of a view would exclude either this interface or every Unity component.
    /// </remarks>
    public interface IUITooltip
    {
        /// <summary>Subscribes to pointer-enter for <paramref name="lifetime"/>.</summary>
        void SubscribeOnEnter(Lifetime lifetime, Action<PointerEventData> listener);

        /// <summary>Subscribes to pointer-exit for <paramref name="lifetime"/>.</summary>
        void SubscribeOnExit(Lifetime lifetime, Action<PointerEventData> listener);

        /// <summary>Subscribes to pointer-move for <paramref name="lifetime"/>.</summary>
        void SubscribeOnMove(Lifetime lifetime, Action<PointerEventData> listener);
    }
}
