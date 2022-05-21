using UnityEngine;

namespace OpenUGD.Services.UI
{
    public interface ITransformProvider
    {
        // Canvas
        Canvas Canvas { get; }
        Camera Camera { get; }

        // Pool
        Transform Pool { get; }

        // Transforms
        RectTransform Background { get; }
        RectTransform Hud { get; }
        RectTransform Window { get; }
        RectTransform Popup { get; }
        RectTransform Tooltip { get; }
        RectTransform Overlay { get; }
        RectTransform Splash { get; }
        RectTransform System { get; }
    }
}
