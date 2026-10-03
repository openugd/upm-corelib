using UnityEngine;

namespace OpenUGD.Core
{
    /// <summary>
    /// The lifetime of a GameObject: a scope created in <c>Awake</c>, nested in <see cref="PlaySession.Lifetime"/>,
    /// and ended in <c>OnDestroy</c> — or when the play session ends, if that comes first. Reach it with
    /// <see cref="GameObjectLifetimeExtensions.GetLifetime(GameObject)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one adapter.</b> Binding something to a GameObject's destruction takes a component that receives
    /// <c>OnDestroy</c>, and every hand-written copy of that component has had to get the same four things right
    /// (audit LS-14): the scope is created in <c>Awake</c>, never from a field initializer or a getter, because
    /// Unity never sends <c>OnDestroy</c> to an object that was never activated; it is nested in the play session,
    /// not in <see cref="Lifetime.Eternal"/>; it ends in <c>OnDestroy</c>; and reading it before <c>Awake</c>
    /// throws instead of handing back a scope that nothing will end. This is that component, and
    /// <see cref="Presenters.ViewBehaviour"/>, <see cref="Utils.Components.SignalMonoBehaviour"/> and
    /// <see cref="ContextBehaviour"/> follow the same rules.
    /// </para>
    /// <para>
    /// <b>Sealed</b>, with Unity's messages private, so no subclass can hide them. To give a component of your own
    /// a scope, read <c>gameObject.GetLifetime()</c> from its <c>Awake</c> or later, or derive from
    /// <see cref="Presenters.ViewBehaviour"/>, whose <c>Lifetime</c> follows the same rules.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("OpenUGD/Lifetime Behaviour")]
    public sealed class LifetimeBehaviour : MonoBehaviour
    {
        private Lifetime.Definition _definition;

        /// <summary>
        /// The GameObject's scope. Alive from <c>Awake</c> until <c>OnDestroy</c> or the end of the play session;
        /// once ended it stays readable, and whatever is registered on it then runs at once.
        /// </summary>
        /// <exception cref="System.InvalidOperationException"><c>Awake</c> has not run: the GameObject has never
        /// been active.</exception>
        public Lifetime Lifetime =>
            _definition != null ? _definition.Lifetime : throw ComponentScope.NotAwake(this, nameof(Lifetime));

        private void Awake() => _definition = ComponentScope.Define(this);

        private void OnDestroy() => _definition?.Terminate();
    }
}
