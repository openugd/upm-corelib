using System;
using Object = UnityEngine.Object;

namespace OpenUGD.Utils
{
    public class ResourceBatchLoader
    {
        private readonly Lifetime _lifetime;
        private readonly ResourceManager _resourceManager;
        private KeepReference _keep;
        private Lifetime.Definition _loaderKeep;

        public ResourceBatchLoader(Lifetime lifetime, ResourceManager resourceManager, Action onComplete)
        {
            _lifetime = lifetime;
            _resourceManager = resourceManager;
            _keep = new KeepReference(lifetime);
            _keep.AddAction(onComplete);
            _loaderKeep = _keep.Keep();
        }

        public void Load()
        {
            _loaderKeep.Terminate();
            _loaderKeep = null;
            _keep = null;
        }

        public Lifetime.Definition Keep() => _keep.Keep();

        public void Add<T>(string path, Action<T> onComplete) where T : Object
        {
            var def = _keep.Keep();
            _resourceManager.Get<T>(path).LoadAsync(_lifetime, result => {
                onComplete(result.Result);
                def.Terminate();
            });
        }

        public void Add(Action<Action> onReady)
        {
            var def = _keep.Keep();
            onReady(def.Terminate);
        }
    }
}
