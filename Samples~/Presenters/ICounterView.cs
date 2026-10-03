using System;

namespace OpenUGD.Samples.Presenters
{
    /// <summary>
    /// What <see cref="CounterPresenter"/> needs from a view. An interface, so the presenter compiles without
    /// UnityEngine and a test can hand it a plain object; <see cref="CounterView"/> is the Unity implementation.
    /// </summary>
    public interface ICounterView
    {
        /// <summary>A name for the log.</summary>
        string Name { get; }

        /// <summary>Raised when the user clicks the view.</summary>
        event Action Clicked;

        /// <summary>Displays <paramref name="text"/>.</summary>
        /// <param name="text">What to display.</param>
        void Show(string text);
    }
}
