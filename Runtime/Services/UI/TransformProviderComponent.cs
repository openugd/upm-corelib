using UnityEngine;

namespace OpenUGD.Services.UI
{
    public class TransformProviderComponent : MonoBehaviour, ITransformProvider
    {
        // canvases
        [field: SerializeField] public Canvas Canvas { get; private set; }
        [field: SerializeField] public Camera Camera { get; private set; }

        // pool
        [field: SerializeField] public Transform Pool { get; private set; }

        // transforms
        [field: SerializeField] public RectTransform Background { get; private set; }
        [field: SerializeField] public RectTransform Hud { get; private set; }
        [field: SerializeField] public RectTransform Window { get; private set; }
        [field: SerializeField] public RectTransform Popup { get; private set; }
        [field: SerializeField] public RectTransform Tooltip { get; private set; }
        [field: SerializeField] public RectTransform Overlay { get; private set; }
        [field: SerializeField] public RectTransform Splash { get; private set; }
        [field: SerializeField] public RectTransform System { get; private set; }
    }
}
