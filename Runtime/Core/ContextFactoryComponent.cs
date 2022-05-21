using OpenUGD.Utils;
using UnityEngine;

namespace OpenUGD.Core
{
    public abstract class ContextFactoryComponent : MonoBehaviour, ICoroutineProvider
    {
        private Lifetime.Definition _lifetime;
        private Signal _onFixedUpdate;
        private Signal<bool> _onFocus;
        private Signal _onLateUpdate;
        private Signal<bool> _onPause;
        private Signal _onQuit;
        private Signal _onUpdate;

        public ISignal OnUpdate => _onUpdate;
        public ISignal OnLateUpdate => _onLateUpdate;
        public ISignal OnFixedUpdate => _onFixedUpdate;
        public ISignal OnQuit => _onQuit;
        public ISignal<bool> OnFocus => _onFocus;
        public ISignal<bool> OnPause => _onPause;
        public Lifetime Lifetime => _lifetime.Lifetime;

        private void Awake() => Create();

        private void Update() => _onUpdate.Fire();

        private void FixedUpdate() => _onFixedUpdate.Fire();

        private void LateUpdate() => _onLateUpdate.Fire();

        private void OnDestroy() => _lifetime.Terminate();

        private void OnApplicationFocus(bool focus) => _onFocus.Fire(focus);

        private void OnApplicationPause(bool pause) => _onPause.Fire(pause);

        private void OnApplicationQuit() => _onQuit.Fire();

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            _lifetime?.Terminate();
            Create();
        }

        protected abstract void CreateContext();

        private void Create()
        {
            _lifetime = Lifetime.Eternal.DefineNested(gameObject.name);

            _onUpdate = new Signal(_lifetime.Lifetime);
            _onLateUpdate = new Signal(_lifetime.Lifetime);
            _onFixedUpdate = new Signal(_lifetime.Lifetime);
            _onQuit = new Signal(_lifetime.Lifetime);
            _onFocus = new Signal<bool>(_lifetime.Lifetime);
            _onPause = new Signal<bool>(_lifetime.Lifetime);

            DontDestroyOnLoad(gameObject);

            CreateContext();
        }
    }

    public abstract class ContextFactoryComponent<T> : ContextFactoryComponent
        where T : IContext
    {
        public T Context { get; private set; }

        protected abstract T CreateContext(Lifetime lifetime);

        protected sealed override void CreateContext() => Context = CreateContext(Lifetime);
    }
}
