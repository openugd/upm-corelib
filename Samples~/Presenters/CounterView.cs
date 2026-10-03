using System;
using OpenUGD.Presenters;
using UnityEngine;

namespace OpenUGD.Samples.Presenters
{
    /// <summary>
    /// A view: it shows text and reports clicks, and knows nothing about presenters. Its <c>Lifetime</c>, from
    /// <see cref="ViewBehaviour"/>, ends when the GameObject is destroyed.
    /// </summary>
    public sealed class CounterView : ViewBehaviour, ICounterView
    {
        [SerializeField] private string _text;

        /// <inheritdoc />
        public event Action Clicked;

        /// <inheritdoc />
        public string Name => name;

        /// <summary>What the view displays; shown in the inspector.</summary>
        public string Text => _text;

        /// <inheritdoc />
        public void Show(string text) => _text = text;

        /// <summary>Clicks the view. Also in the component's context menu, in play mode.</summary>
        [ContextMenu("Click")]
        public void Click()
        {
            if (Clicked == null)
            {
                Debug.Log($"{name}: clicked, but nothing is listening", this);
                return;
            }

            Clicked();
        }
    }
}
