using System;
using UnityEngine;

namespace OpenUGD.Utils.Components
{
    [ExecuteInEditMode]
    public class SpriteRendererFillOrthographicComponent : MonoBehaviour
    {
        private float _orthographicSize;
        private int _scaledPixelHeight;
        private int _scaledPixelWidth;

        private void Awake() => Resize();

        private void Update()
        {
            if (_camera == null || _spriteRenderer == null)
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
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position,
                new Vector3(_spriteRenderer.size.x * _spriteRenderer.transform.localScale.x,
                    _spriteRenderer.size.y * _spriteRenderer.transform.localScale.y, 0f));
            Gizmos.color = lastColor;
        }

        private void Resize()
        {
            _spriteRenderer.transform.localScale = new Vector3(1, 1, 1);

            var width = _spriteRenderer.size.x;
            var height = _spriteRenderer.size.y;

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

            _spriteRenderer.transform.localScale = imgScale;
        }
#pragma warning disable 649
        [SerializeField] private Camera _camera;

        [SerializeField] private SpriteRenderer _spriteRenderer;
#pragma warning restore 649
    }
}
