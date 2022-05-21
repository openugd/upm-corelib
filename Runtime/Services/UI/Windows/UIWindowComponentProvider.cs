using System;
using OpenUGD.Utils;
using UnityEngine;
using UnityEngine.Assertions;

namespace OpenUGD.Services.UI.Windows
{
    public class UIWindowComponentProvider : IUIComponentProvider
    {
        private readonly Func<ITransformProvider, Transform> _provider;
        private readonly bool _usePool;

        public UIWindowComponentProvider(bool usePool = true)
        {
            _usePool = usePool;
            _provider = p => p.Window;
        }

        public UIWindowComponentProvider(Func<ITransformProvider, Transform> provider, bool usePool = true)
        {
            Assert.IsNotNull(provider);
            _provider = provider;
            _usePool = usePool;
        }

        public void Provide(Lifetime lifetime, string path, Type type, Action<UIComponentProviderContext> onResult)
        {
            var def = lifetime.DefineNested();
            _prefabResourceManager.GetPrefab(path).LoadAsync(def.Lifetime, result => {
                def.Terminate();

                var parent = _provider(_transformProvider);
                var windowComponent = result.Instantiate(type, parent);

                if (parent == null)
                {
                    GameObject.DontDestroyOnLoad(windowComponent.gameObject);
                }

                var context = new UIComponentProviderContext(windowComponent, lifetime);
                context.Lifetime.AddAction(() => {
                    result.Release(windowComponent);
                    if (!_usePool)
                    {
                        result.Collect();
                    }
                    else
                    {
                        windowComponent.transform.SetParent(_transformProvider.Pool, false);
                    }
                });
                onResult(context);
            });
        }

#pragma warning disable 649
        [Inject] private PrefabResourceManager _prefabResourceManager;
        [Inject] private IInject _injector;
        [Inject] private ITransformProvider _transformProvider;
#pragma warning restore 649
    }
}
