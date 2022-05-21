using System;
using UnityEngine;

namespace OpenUGD.Utils.Components
{
    [ExecuteInEditMode]
    public class FillOrthographicComponent : MonoBehaviour
    {
        private float _orthographicSize;
        private int _scaledPixelHeight;
        private int _scaledPixelWidth;

        private void Awake() => Resize();

        private void Update()
        {
            if (_camera == null || _targets == null || _targets.Length == 0)
            {
                return;
            }

            var orthographicSize = _camera.orthographicSize;
            var scaledPixelWidth = _camera.scaledPixelWidth;
            var scaledPixelHeight = _camera.scaledPixelHeight;
            if (Math.Abs(orthographicSize - _orthographicSize) > float.Epsilon ||
                scaledPixelWidth != _scaledPixelWidth ||
                scaledPixelHeight != _scaledPixelHeight)
            {
                _orthographicSize = orthographicSize;
                _scaledPixelWidth = scaledPixelWidth;
                _scaledPixelHeight = scaledPixelHeight;
                Resize();
            }
        }

        private void OnDrawGizmos()
        {
            var lastColor = Gizmos.color;
            var index = 0;
            foreach (var target in _targets)
            {
                Gizmos.color = Color.Lerp(Color.green, Color.red, (float)index / _targets.Length);
                Gizmos.DrawWireCube(transform.position,
                    new Vector3(_size.x * target.localScale.x, _size.y * target.localScale.y, 0f));
                index++;
            }

            Gizmos.color = lastColor;
        }

        private void Resize()
        {
            var width = _size.x;
            var height = _size.y;

            var worldScreenHeight = _camera.orthographicSize * 2f;
            var worldScreenWidth = worldScreenHeight / _camera.scaledPixelHeight * _camera.scaledPixelWidth;

            var imgScale = new Vector3(1f, 1f, 1f);

            var ratio = new Vector2(width / height, height / width);
            if (worldScreenWidth / width > worldScreenHeight / height)
            {
                imgScale.x = worldScreenWidth / width;
                imgScale.y = imgScale.x * ratio.y;
            }
            else
            {
                imgScale.y = worldScreenHeight / height;
                imgScale.x = imgScale.y * ratio.x;
            }

            foreach (var target in _targets)
            {
                target.localScale = imgScale;
            }
        }
#pragma warning disable 649
        [SerializeField] private Camera _camera;

        [SerializeField] private Vector2 _size = Vector2.one;

        [SerializeField] private Transform[] _targets;
#pragma warning restore 649
    }
}
