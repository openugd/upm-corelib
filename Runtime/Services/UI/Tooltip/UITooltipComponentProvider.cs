using System;
using OpenUGD.Utils;
using UnityEngine;

namespace OpenUGD.Services.UI.Tooltip
{
    /// <summary>Loads a tooltip view from <c>Resources</c> and parents it under the tooltip
    /// transform.</summary>
    public class UITooltipComponentProvider : IUIComponentProvider
    {
        /// <inheritdoc />
        public void Provide(Lifetime lifetime, Options options, Type targetType,
            Action<UIComponentProviderContext> onResult)
        {
            var def = lifetime.DefineNested();
            _prefabResourceManager.GetPrefab(options.Path).LoadAsync(def.Lifetime, result => {
                def.Terminate();

                var view = result.Instantiate(targetType, _transformProvider.Tooltip());

                var context = new UIComponentProviderContext(view, lifetime);
                context.Lifetime.AddAction(() => {
                    result.Release(view);
                    result.Collect();
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
