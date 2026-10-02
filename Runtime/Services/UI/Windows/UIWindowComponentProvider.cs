using System;
using OpenUGD.Utils;
using UnityEngine;
using UnityEngine.Assertions;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>Loads a window view from <c>Resources</c> and parents it under a chosen transform.</summary>
    public class UIWindowComponentProvider : IUIComponentProvider
    {
        private readonly Func<ITransformProvider, Transform> _provider;
        private readonly bool _usePool;

        /// <summary>
        /// Creates a provider that parents under the layer keyed <see cref="UILayers.Window"/>, and
        /// throws <see cref="UILayerNotBoundException"/> at open time if the scene does not bind it.
        /// </summary>
        /// <param name="usePool">Return the view to the pool transform instead of destroying it.</param>
        public UIWindowComponentProvider(bool usePool = true)
        {
            _usePool = usePool;
            _provider = p => p.Window();
        }

        /// <summary>Creates a provider that parents under a transform of your choosing.</summary>
        /// <param name="provider">Selects the parent transform.</param>
        /// <param name="usePool">Return the view to the pool transform instead of destroying it.</param>
        public UIWindowComponentProvider(Func<ITransformProvider, Transform> provider, bool usePool = true)
        {
            Assert.IsNotNull(provider);
            _provider = provider;
            _usePool = usePool;
        }

        /// <inheritdoc />
        public void Provide(Lifetime lifetime, Options options, Type targetType,
            Action<UIComponentProviderContext> onResult)
        {
            var def = lifetime.DefineNested();
            _prefabResourceManager.GetPrefab(options.Path).LoadAsync(def.Lifetime, result => {
                def.Terminate();

                var parent = _provider(_transformProvider);
                var windowComponent = result.Instantiate(targetType, parent);

                // A selector may deliberately return null to mean "scene root"; the default selector
                // cannot, because it asks for the window layer by key and that throws when unbound.
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
        [Inject] private ITransformProvider _transformProvider;
#pragma warning restore 649
    }
}
