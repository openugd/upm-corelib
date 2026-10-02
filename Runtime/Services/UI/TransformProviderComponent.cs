using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace OpenUGD.Services.UI
{
    /// <summary>
    /// The scene's answer to <see cref="ITransformProvider"/>: a component whose inspector holds the
    /// canvas, its camera, the pool parking transform, and the list of layers this scene offers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Adding a layer is a row, not a release.</b> The list is ordered back to front and a designer
    /// extends it in the inspector. Lookups give the built-in keys no special treatment, so a
    /// project's own layer is bound the same way the HUD is. The built-in keys appear only as starting
    /// rows: <see cref="Reset"/> seeds the eight keys in <see cref="UILayers"/> as empty rows to fill
    /// in — delete the ones the scene does not use — and the 0.6.x upgrade below produces the same
    /// eight.
    /// </para>
    /// <para>
    /// <b>Changed in 2.0.0, and data saved by 0.6.x is carried over.</b> This used to expose eight
    /// fixed <c>RectTransform</c> fields, five of which nothing ever read. Scenes and prefabs saved by
    /// 0.6.x are upgraded as they load, with nothing to reassign:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Canvas"/>, <see cref="Camera"/> and <see cref="Pool"/> are read from their 0.6.x
    /// serialized names through <see cref="FormerlySerializedAsAttribute"/>.
    /// </description></item>
    /// <item><description>
    /// The eight old layer fields become eight rows in their old order — background, hud, window,
    /// popup, tooltip, overlay, splash, system — keyed by <see cref="UILayers"/>, each keeping the
    /// transform it had. A field that was never assigned becomes a row with no transform, the same
    /// empty row <see cref="Reset"/> seeds; delete the rows the scene does not use. This happens only
    /// when the list is empty, so rows that already exist are never overwritten.
    /// </description></item>
    /// </list>
    /// <para>
    /// The upgrade runs in memory each time the old data is loaded and does not mark anything dirty.
    /// It becomes permanent once Unity writes the file again — when the scene or prefab is saved
    /// after an edit, or re-serialized with <c>AssetDatabase.ForceReserializeAssets</c>. Until then
    /// the file keeps the 0.6.x layout and keeps loading correctly. Data written by this version
    /// carries a format stamp and never goes through the upgrade, so a list a designer emptied on
    /// purpose stays empty. The hidden fields that read the old layout stay for all of 2.x.
    /// </para>
    /// </remarks>
    public class TransformProviderComponent : MonoBehaviour, ITransformProvider, ISerializationCallbackReceiver
    {
        /// The data layout this version writes. Data without the stamp was written by 0.6.x.
        private const int CurrentDataVersion = 1;

        [SerializeField, FormerlySerializedAs("<Canvas>k__BackingField")]
        private Canvas _canvas;

        [SerializeField, FormerlySerializedAs("<Camera>k__BackingField")]
        private Camera _camera;

        [SerializeField, FormerlySerializedAs("<Pool>k__BackingField")]
        private Transform _pool;

        [Tooltip("Ordered back to front: the first entry is drawn behind the rest.")]
        [SerializeField] private List<UILayer> _layers = new List<UILayer>();

        // 0 when the data came from 0.6.x, which had no such field; stamped to CurrentDataVersion on
        // every save. This, not the legacy fields, is what tells old data from new: in the editor an
        // unassigned object field can hold a placeholder that is not a null reference, and the
        // serialization callbacks may not ask Unity which one it is.
        [SerializeField, HideInInspector] private int _dataVersion;

        // The eight 0.6.x layer fields, read only to be moved into _layers by OnAfterDeserialize and
        // emptied there. Back to front, matching UILayers.
        [SerializeField, HideInInspector, FormerlySerializedAs("<Background>k__BackingField")]
        private RectTransform _legacyBackground;

        [SerializeField, HideInInspector, FormerlySerializedAs("<Hud>k__BackingField")]
        private RectTransform _legacyHud;

        [SerializeField, HideInInspector, FormerlySerializedAs("<Window>k__BackingField")]
        private RectTransform _legacyWindow;

        [SerializeField, HideInInspector, FormerlySerializedAs("<Popup>k__BackingField")]
        private RectTransform _legacyPopup;

        [SerializeField, HideInInspector, FormerlySerializedAs("<Tooltip>k__BackingField")]
        private RectTransform _legacyTooltip;

        [SerializeField, HideInInspector, FormerlySerializedAs("<Overlay>k__BackingField")]
        private RectTransform _legacyOverlay;

        [SerializeField, HideInInspector, FormerlySerializedAs("<Splash>k__BackingField")]
        private RectTransform _legacySplash;

        [SerializeField, HideInInspector, FormerlySerializedAs("<System>k__BackingField")]
        private RectTransform _legacySystem;

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

        /// <summary>Stamps the data with the layout this version writes.</summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            _dataVersion = CurrentDataVersion;
        }

        /// <summary>
        /// Upgrades data written by 0.6.x: moves the eight old layer fields into the list when the list
        /// is empty, then empties them. Data that carries the format stamp is left exactly as loaded.
        /// </summary>
        /// <remarks>
        /// Unity may call this off the main thread, where most of its API must not be touched, so the
        /// fields are only read and assigned. Nothing compares a field with Unity's
        /// <c>operator ==</c>: a field is moved as it is, whether it holds a live transform, a missing
        /// reference or nothing.
        /// </remarks>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (_dataVersion >= CurrentDataVersion) return;

            if (ReferenceEquals(_layers, null)) _layers = new List<UILayer>();

            if (_layers.Count == 0)
            {
                _layers.Add(new UILayer(UILayers.Background, _legacyBackground));
                _layers.Add(new UILayer(UILayers.Hud, _legacyHud));
                _layers.Add(new UILayer(UILayers.Window, _legacyWindow));
                _layers.Add(new UILayer(UILayers.Popup, _legacyPopup));
                _layers.Add(new UILayer(UILayers.Tooltip, _legacyTooltip));
                _layers.Add(new UILayer(UILayers.Overlay, _legacyOverlay));
                _layers.Add(new UILayer(UILayers.Splash, _legacySplash));
                _layers.Add(new UILayer(UILayers.System, _legacySystem));
            }

            _legacyBackground = null;
            _legacyHud = null;
            _legacyWindow = null;
            _legacyPopup = null;
            _legacyTooltip = null;
            _legacyOverlay = null;
            _legacySplash = null;
            _legacySystem = null;

            _dataVersion = CurrentDataVersion;
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
        /// Reports a duplicated key in the editor, where it can still be fixed cheaply: only the first
        /// row with a key is ever used, so a duplicate silently sends views to whichever row came first.
        /// </summary>
        /// <remarks>
        /// A row without a transform is not reported here. <see cref="Reset"/> and the 0.6.x upgrade
        /// both produce such rows on purpose. At runtime <see cref="TryGetLayer"/> treats the row as
        /// unbound, and the throwing lookups in <c>TransformProviderExtensions</c> — which every
        /// built-in provider uses — fail with a <see cref="UILayerNotBoundException"/> that lists every
        /// row and marks the ones without a transform.
        /// </remarks>
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
