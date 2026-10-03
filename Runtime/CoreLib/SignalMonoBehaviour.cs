using OpenUGD.Core;
using UnityEngine;

namespace OpenUGD.Utils.Components
{
    /// <summary>
    /// Exposes the Unity lifecycle messages of a <see cref="GameObject"/> as <see cref="ISignal"/>s, so a
    /// plain C# object can observe them without being a <see cref="MonoBehaviour"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attach it with <c>AddComponent&lt;SignalMonoBehaviour&gt;()</c> and subscribe. Signals are scoped to a
    /// <see cref="Lifetime"/> that this component owns, so subscribers are released with the object and no
    /// explicit unsubscribe is needed. The scope follows the rules of <see cref="LifetimeBehaviour"/>: created in
    /// <c>Awake</c>, nested in <see cref="PlaySession.Lifetime"/>, and ended in <c>OnDestroy</c> or when the play
    /// session ends, whichever comes first.
    /// </para>
    /// <para>
    /// <b>Awake first.</b> The signals exist from <c>Awake</c>, which Unity runs inside <c>AddComponent</c> on an
    /// active GameObject, and when an inactive one is first activated. Reading a signal before that throws
    /// <see cref="System.InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// <b>No per-frame signals.</b> Unity dispatches <c>Update</c> to every component that defines it, subscribers
    /// or not; subscribe to a central source such as <c>ContextBehaviour.OnUpdate</c> instead.
    /// </para>
    /// <para>
    /// <b>Subclassing.</b> <see cref="Awake"/>, <see cref="Start"/>, <see cref="OnEnable"/>,
    /// <see cref="OnDisable"/> and <see cref="OnDestroy"/> are <c>protected virtual</c>. Override one and call
    /// <c>base</c> — <c>base.Awake()</c> first, since it creates the signals — or its signal stops firing, and,
    /// for <see cref="OnDestroy"/>, the scope lasts until the play session ends. Declaring one without
    /// <c>override</c> hides it, which the compiler reports as warning CS0114.
    /// </para>
    /// </remarks>
    public class SignalMonoBehaviour : MonoBehaviour
    {
        private Lifetime.Definition _definition;
        private Signal _onDestroy;
        private Signal _onDisable;
        private Signal _onEnable;
        private Signal _onStart;

        /// <summary>Raised from Unity's <c>Start</c>, once, on the first frame this object is
        /// active.</summary>
        /// <exception cref="System.InvalidOperationException">Read before <c>Awake</c>.</exception>
        public ISignal StartSignal => _onStart ?? throw ComponentScope.NotAwake(this, nameof(StartSignal));

        /// <summary>Raised from Unity's <c>OnEnable</c>, on every activation.</summary>
        /// <remarks>
        /// The first activation happens inside <c>AddComponent</c>, before you hold the reference, so the
        /// initial enable is not observable. Later re-enables are.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Read before <c>Awake</c>.</exception>
        public ISignal EnableSignal => _onEnable ?? throw ComponentScope.NotAwake(this, nameof(EnableSignal));

        /// <summary>Raised from Unity's <c>OnDisable</c>, on every deactivation — including the one that
        /// precedes destruction, unless the play session has already ended the scope.</summary>
        /// <exception cref="System.InvalidOperationException">Read before <c>Awake</c>.</exception>
        public ISignal DisableSignal => _onDisable ?? throw ComponentScope.NotAwake(this, nameof(DisableSignal));

        /// <summary>
        /// Raised once, when this component's scope ends, immediately before the signals are released: from
        /// Unity's <c>OnDestroy</c>, or earlier, when the play session ends (the application quits or play mode
        /// is exited).
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Read before <c>Awake</c>.</exception>
        public ISignal DestroySignal => _onDestroy ?? throw ComponentScope.NotAwake(this, nameof(DestroySignal));

        /// <summary>
        /// Unity's <c>Awake</c>: creates the scope and the signals. <b>Call <c>base.Awake()</c> first</b> when
        /// overriding; until it has run, every signal property throws.
        /// </summary>
        protected virtual void Awake()
        {
            if (_definition != null) return;

            _definition = ComponentScope.Define(this);
            var lifetime = _definition.Lifetime;

            _onStart = new Signal(lifetime);
            _onEnable = new Signal(lifetime);
            _onDisable = new Signal(lifetime);
            var onDestroy = _onDestroy = new Signal(lifetime);

            // Registered after the signals, so it runs first when the scope ends, while they still deliver: on
            // OnDestroy, and on the end of the play session. Signal 2.0.0 runs every handler and then rethrows what
            // failed; the scope ends regardless, and Terminate reports the failure.
            lifetime.AddAction(onDestroy.Fire);
        }

        // Null-conditional: Unity raises these on any enabled component, and a subclass may skip base.Awake().

        /// <summary>
        /// Unity's <c>Start</c>: raises <see cref="StartSignal"/>. <b>Call <c>base.Start()</c></b> when
        /// overriding, or the signal never fires.
        /// </summary>
        protected virtual void Start() => _onStart?.Fire();

        /// <summary>
        /// Unity's <c>OnEnable</c>: raises <see cref="EnableSignal"/>. <b>Call <c>base.OnEnable()</c></b> when
        /// overriding, or the signal never fires.
        /// </summary>
        protected virtual void OnEnable() => _onEnable?.Fire();

        /// <summary>
        /// Unity's <c>OnDisable</c>: raises <see cref="DisableSignal"/>. <b>Call <c>base.OnDisable()</c></b> when
        /// overriding, or the signal never fires.
        /// </summary>
        protected virtual void OnDisable() => _onDisable?.Fire();

        /// <summary>
        /// Unity's <c>OnDestroy</c>: ends this component's scope, which raises <see cref="DestroySignal"/> and then
        /// releases every subscriber. <b>Call <c>base.OnDestroy()</c></b> when overriding, or neither happens until
        /// the play session ends.
        /// </summary>
        protected virtual void OnDestroy() => _definition?.Terminate();
    }
}
