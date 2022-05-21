using UnityEngine;

namespace OpenUGD.Core.Widgets
{
    public class WidgetView : MonoBehaviour
    {
        private Lifetime.Definition _definition;

        protected Lifetime Lifetime => _definition.Lifetime;

        private void Awake()
        {
            _definition = Lifetime.Define(Lifetime.Eternal);
            OnAwake();
        }

        private void OnDestroy() => _definition.Terminate();

        protected virtual void OnAwake()
        {
        }
    }
}
