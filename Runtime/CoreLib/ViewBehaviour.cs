using System;
using UnityEngine;

namespace OpenUGD.Presenters
{
    /// <summary>
    /// A <see cref="MonoBehaviour"/> that owns a <see cref="OpenUGD.Lifetime"/> ending at <c>OnDestroy</c>,
    /// so anything scoped to this view is torn down with its GameObject.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is how a Unity view ties a presenter's scope to its own destruction: <c>presenter.CloseWith(view.Lifetime)</c>
    /// closes the presenter when the view is destroyed. A presenter renders only while it is live, so that is
    /// all it takes for <c>Refresh</c> to stop rather than write into a destroyed object.
    /// </para>
    /// <para>
    /// <b>Subclassing.</b> <see cref="Awake"/> and <see cref="OnDestroy"/> are <c>protected virtual</c>.
    /// Override them and call <c>base</c> — <c>base.Awake()</c> first, since it creates <see cref="Lifetime"/>.
    /// Declaring either without <c>override</c> hides it, which the compiler reports as warning CS0114.
    /// </para>
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
        /// This view's scope: created in <c>Awake</c>, terminated in <c>OnDestroy</c>. Hand it to whatever must
        /// end with the view — <c>presenter.CloseWith(view.Lifetime)</c>, for a presenter.
        /// </summary>
        /// <remarks>
        /// <i>Changed in 2.0.0</i> — this was <c>protected</c>, so nothing outside the view could bind to its
        /// destruction, although that was the documented purpose of the class (audit CC-5).
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <c>Awake</c> has not run: the GameObject has never been active, or a subclass's <c>Awake</c> did not
        /// call <c>base.Awake()</c>.
        /// </exception>
        public Lifetime Lifetime =>
            _definition != null
                ? _definition.Lifetime
                : throw new InvalidOperationException(
                    $"{GetType().Name}.Lifetime was read before Awake() ran, so there is no scope yet. Unity runs " +
                    "Awake when the GameObject is first active; activate it before binding anything to the view. " +
                    "If a subclass overrides Awake, it must call base.Awake().");

        /// <summary>
        /// Unity's <c>Awake</c>: creates <see cref="Lifetime"/>. <b>Call <c>base.Awake()</c> first</b> when
        /// overriding.
        /// </summary>
        /// <remarks>
        /// <i>Changed in 2.0.0</i> — this replaces the <c>OnAwake</c> hook, which existed only because
        /// <c>Awake</c> was private.
        /// </remarks>
        protected virtual void Awake()
        {
            if (_definition == null)
            {
                _definition = Lifetime.Eternal.DefineNested(name);
            }
        }

        /// <summary>
        /// Unity's <c>OnDestroy</c>: terminates <see cref="Lifetime"/>, running everything bound to it — a
        /// presenter's <c>Close</c>, say. <b>Call <c>base.OnDestroy()</c></b> when overriding, or none of it runs.
        /// </summary>
        protected virtual void OnDestroy() => _definition?.Terminate();
    }
}
