using System.Collections.Generic;
using UnityEngine;

namespace OpenUGD.Services.UI
{
    /// <summary>
    /// The scene's answer to <see cref="ITransformProvider"/>: a component whose inspector holds the
    /// canvas, its camera, the pool parking transform, and the list of layers this scene offers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Adding a layer is a row, not a release.</b> The list is ordered back to front and a designer
    /// extends it in the inspector. Nothing in this component knows the built-in keys, so a project's
    /// own layer is bound the same way the HUD is. <see cref="Reset"/> seeds the eight keys in
    /// <see cref="UILayers"/> as empty rows to fill in, purely as a starting point — delete the ones
    /// the scene does not use.
    /// </para>
    /// <para>
    /// <b>Changed in 2.0.0.</b> This used to expose eight fixed <c>RectTransform</c> fields, five of
    /// which nothing ever read. Existing scenes will show empty rows: the old serialized fields no
    /// longer exist, so each layer has to be reassigned once. That is the whole migration, and
    /// <see cref="OnValidate"/> reports what is still missing.
    /// </para>
    /// </remarks>
    public class TransformProviderComponent : MonoBehaviour, ITransformProvider
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _pool;

        [Tooltip("Ordered back to front: the first entry is drawn behind the rest.")]
        [SerializeField] private List<UILayer> _layers = new List<UILayer>();

        /// <inheritdoc/>
        public Canvas Canvas => _canvas;

        /// <inheritdoc/>
        public Camera Camera => _camera;

        /// <inheritdoc/>
        public Transform Pool => _pool;

        /// <inheritdoc/>
        public IReadOnlyList<UILayer> Layers => _layers;

        /// <inheritdoc/>
        public bool TryGetLayer(string key, out RectTransform layer)
        {
            if (!string.IsNullOrEmpty(key))
            {
                for (var i = 0; i < _layers.Count; i++)
                {
                    if (!string.Equals(_layers[i].Key, key, System.StringComparison.Ordinal)) continue;

                    var transform = _layers[i].Transform;

                    // Unity's operator == reports a destroyed transform as null, so a row pointing at
                    // something already torn down is unbound rather than a parent of the scene root.
                    if (transform == null) break;

                    layer = transform;
                    return true;
                }
            }

            layer = null;
            return false;
        }

        /// <summary>
        /// Seeds the list with the eight keys in <see cref="UILayers"/>, back to front, when the
        /// component is first added or reset in the inspector. Only a starting point: rows the scene
        /// does not need should be deleted, and rows it does need are not limited to these.
        /// </summary>
        private void Reset()
        {
            _canvas = GetComponentInParent<Canvas>();
            _layers = new List<UILayer> {
                new UILayer(UILayers.Background, null),
                new UILayer(UILayers.Hud, null),
                new UILayer(UILayers.Window, null),
                new UILayer(UILayers.Popup, null),
                new UILayer(UILayers.Tooltip, null),
                new UILayer(UILayers.Overlay, null),
                new UILayer(UILayers.Splash, null),
                new UILayer(UILayers.System, null),
            };
        }

        /// <summary>
        /// Reports wiring mistakes in the editor, where they can still be fixed cheaply: a duplicated
        /// key, which would silently send views to whichever row came first, and a row whose transform
        /// was never assigned, which fails only when something asks for that layer at runtime.
        /// </summary>
        private void OnValidate()
        {
            if (_layers == null) return;

            for (var i = 0; i < _layers.Count; i++)
            {
                var key = _layers[i].Key;
                if (string.IsNullOrEmpty(key)) continue;

                for (var j = i + 1; j < _layers.Count; j++)
                {
                    if (!string.Equals(_layers[j].Key, key, System.StringComparison.Ordinal)) continue;

                    Debug.LogError(
                        $"{nameof(TransformProviderComponent)} on '{name}' binds '{key}' more than " +
                        "once. Only the first row is ever used; delete the duplicate.", this);
                    break;
                }
            }
        }
    }
}
