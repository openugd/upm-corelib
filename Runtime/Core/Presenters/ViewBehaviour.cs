using UnityEngine;

namespace OpenUGD.Core.Presenters
{
    /// <summary>
    /// A <see cref="MonoBehaviour"/> that owns a <see cref="OpenUGD.Lifetime"/> ending at <c>OnDestroy</c>,
    /// so anything scoped to this view is torn down with its GameObject.
    /// </summary>
    /// <remarks>
    /// This is how a Unity view ties a presenter's scope to its own destruction. A presenter is live while
    /// its <c>Lifetime</c> is running and it has a view; giving the view's <c>Lifetime</c> to the presenter
    /// is therefore all that is needed for <c>Refresh</c> to stop rather than write into a destroyed object.
    /// <para>
    /// <i>Changed in 2.0.0</i> - there is no <c>IWidgetView</c> interface any more. Requiring every view to
    /// implement one cut off every leaf presenter over a Unity component, because
    /// <c>UnityEngine.UI.Button</c> cannot implement our interface. Liveness is a property of the
    /// presenter's own scope, not of the view type.
    /// </para>
    /// </remarks>
    public class ViewBehaviour : MonoBehaviour
    {
        private Lifetime.Definition _definition;

        /// <summary>
        /// This view's scope. Terminates in <c>OnDestroy</c>.
        /// </summary>
        protected Lifetime Lifetime => _definition.Lifetime;

        private void Awake()
        {
            _definition = Lifetime.Define(Lifetime.Eternal);
            OnAwake();
        }

        private void OnDestroy() => _definition.Terminate();

        /// <summary>
        /// Called from <c>Awake</c>, once <see cref="Lifetime"/> exists.
        /// </summary>
        protected virtual void OnAwake()
        {
        }
    }
}
