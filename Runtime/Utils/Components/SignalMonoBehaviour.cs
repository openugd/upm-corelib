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
    /// <see cref="Lifetime"/> that this component owns and terminates in <c>OnDestroy</c>, so subscribers are
    /// released with the object and no explicit unsubscribe is needed.
    /// </para>
    /// <para>
    /// <b>Breaking changes in 2.0.0.</b> <c>UpdateSignal</c>, <c>LateUpdateSignal</c>,
    /// <c>FixedUpdateSignal</c> and <c>AwakeSignal</c> are removed. The three per-frame signals cost an
    /// engine message dispatch on every instance every frame whether or not anyone subscribed, because Unity
    /// dispatches to any component that merely defines the method; subscribe to a central update source
    /// instead. <c>AwakeSignal</c> could never fire to a subscriber at all: <c>AddComponent</c> runs
    /// <c>Awake</c> before it returns the reference you would subscribe through.
    /// </para>
    /// <para>
    /// <b>Caveat.</b> Unity does not run <c>Awake</c>, <c>Start</c> or <c>OnDestroy</c> on a component
    /// attached to an inactive <see cref="GameObject"/> that is never activated. The signals are still
    /// non-null and safe to subscribe to, but such an object destroyed while still inactive will not raise
    /// <see cref="DestroySignal"/> — the engine never tells it that it was destroyed.
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
        public ISignal StartSignal
        {
            get
            {
                Initialize();
                return _onStart;
            }
        }

        /// <summary>Raised from Unity's <c>OnEnable</c>, on every activation.</summary>
        /// <remarks>
        /// The first activation happens inside <c>AddComponent</c>, before you hold the reference, so the
        /// initial enable is not observable. Later re-enables are.
        /// </remarks>
        public ISignal EnableSignal
        {
            get
            {
                Initialize();
                return _onEnable;
            }
        }

        /// <summary>Raised from Unity's <c>OnDisable</c>, on every deactivation — including the one that
        /// precedes destruction.</summary>
        public ISignal DisableSignal
        {
            get
            {
                Initialize();
                return _onDisable;
            }
        }

        /// <summary>Raised from Unity's <c>OnDestroy</c>, once, immediately before this component's
        /// <see cref="Lifetime"/> terminates.</summary>
        public ISignal DestroySignal
        {
            get
            {
                Initialize();
                return _onDestroy;
            }
        }

        private void Initialize()
        {
            if (_definition != null)
            {
                return;
            }

            _definition = Lifetime.Define(Lifetime.Eternal);
            var lifetime = _definition.Lifetime;

            _onStart = new Signal(lifetime);
            _onEnable = new Signal(lifetime);
            _onDisable = new Signal(lifetime);
            _onDestroy = new Signal(lifetime);
        }

        private void Awake() => Initialize();

        private void Start()
        {
            Initialize();
            _onStart.Fire();
        }

        private void OnEnable()
        {
            Initialize();
            _onEnable.Fire();
        }

        private void OnDisable()
        {
            Initialize();
            _onDisable.Fire();
        }

        private void OnDestroy()
        {
            // Never initialised - Unity destroyed an object that was never activated. Nothing to fire, and no
            // scope was ever allocated, so there is nothing to release either.
            if (_definition == null)
            {
                return;
            }

            // Signal 2.0.0 aggregates handler exceptions and rethrows them. Without the finally, one bad
            // subscriber would skip Terminate() and strand this definition on Lifetime.Eternal forever.
            try
            {
                _onDestroy.Fire();
            }
            finally
            {
                _definition.Terminate();
            }
        }
    }
}
