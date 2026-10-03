using System;
using OpenUGD.Core;
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
    /// <b>Its scope</b> follows the rules of <see cref="LifetimeBehaviour"/>: created in <c>Awake</c>, nested in
    /// <see cref="PlaySession.Lifetime"/>, ended in <c>OnDestroy</c>, and an exception if read before <c>Awake</c>.
    /// </para>
    /// <para>
    /// <b>Subclassing.</b> <see cref="Awake"/> and <see cref="OnDestroy"/> are <c>protected virtual</c>.
    /// Override them and call <c>base</c> — <c>base.Awake()</c> first, since it creates <see cref="Lifetime"/>.
    /// Declaring either without <c>override</c> hides it, which the compiler reports as warning CS0114.
    /// </para>
    /// </remarks>
    public class ViewBehaviour : MonoBehaviour
    {
        private Lifetime.Definition _definition;

        /// <summary>
        /// This view's scope: created in <c>Awake</c>, nested in <see cref="PlaySession.Lifetime"/>, and
        /// terminated in <c>OnDestroy</c> or when the play session ends, whichever comes first. Hand it to whatever
        /// must end with the view — <c>presenter.CloseWith(view.Lifetime)</c>, for a presenter.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <c>Awake</c> has not run: the GameObject has never been active, or a subclass's <c>Awake</c> did not
        /// call <c>base.Awake()</c>.
        /// </exception>
        public Lifetime Lifetime =>
            _definition != null ? _definition.Lifetime : throw ComponentScope.NotAwake(this, nameof(Lifetime));

        /// <summary>
        /// Unity's <c>Awake</c>: creates <see cref="Lifetime"/>. <b>Call <c>base.Awake()</c> first</b> when
        /// overriding.
        /// </summary>
        protected virtual void Awake()
        {
            if (_definition == null)
            {
                _definition = ComponentScope.Define(this);
            }
        }

        /// <summary>
        /// Unity's <c>OnDestroy</c>: terminates <see cref="Lifetime"/>, running everything bound to it — a
        /// presenter's <c>Close</c>, say. <b>Call <c>base.OnDestroy()</c></b> when overriding, or none of it runs.
        /// </summary>
        protected virtual void OnDestroy() => _definition?.Terminate();
    }
}
