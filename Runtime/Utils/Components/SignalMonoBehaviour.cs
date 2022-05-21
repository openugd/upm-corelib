using UnityEngine;

namespace OpenUGD.Utils.Components
{
    public class SignalMonoBehaviour : MonoBehaviour
    {
        private Lifetime.Definition _lifetime;
        private Signal _onAwake;
        private Signal _onDestroy;
        private Signal _onDisable;
        private Signal _onEnable;
        private Signal _onFixedUpdate;
        private Signal _onLateUpdate;
        private Signal _onStart;
        private Signal _onUpdate;

        public ISignal UpdateSignal => _onUpdate;
        public ISignal LateUpdateSignal => _onLateUpdate;
        public ISignal FixedUpdateSignal => _onFixedUpdate;
        public ISignal AwakeSignal => _onAwake;
        public ISignal StartSignal => _onStart;
        public ISignal EnableSignal => _onEnable;
        public ISignal DisableSignal => _onDisable;
        public ISignal DestroySignal => _onDestroy;


        private void Awake()
        {
            _lifetime = Lifetime.Define(Lifetime.Eternal);

            _onUpdate = new Signal(_lifetime.Lifetime);
            _onLateUpdate = new Signal(_lifetime.Lifetime);
            _onFixedUpdate = new Signal(_lifetime.Lifetime);

            _onAwake = new Signal(_lifetime.Lifetime);
            _onEnable = new Signal(_lifetime.Lifetime);
            _onDisable = new Signal(_lifetime.Lifetime);
            _onDestroy = new Signal(_lifetime.Lifetime);
            _onStart = new Signal(_lifetime.Lifetime);
            _onDestroy = new Signal(_lifetime.Lifetime);

            _onAwake.Fire();
        }

        private void Start() => _onStart.Fire();

        private void Update() => _onUpdate.Fire();

        private void FixedUpdate() => _onFixedUpdate.Fire();

        private void LateUpdate() => _onLateUpdate.Fire();

        private void OnEnable() => _onEnable.Fire();

        private void OnDisable() => _onDisable.Fire();

        private void OnDestroy()
        {
            _onDestroy.Fire();

            _lifetime.Terminate();
        }
    }
}
