using System;
using System.Collections.Generic;

namespace OpenUGD.Core.Presenters
{
    /// <summary>
    /// Everything a <see cref="Presenter"/> can do that is not one of its three methods. Extension methods,
    /// so that a consumer can add their own alongside these without the presenter type growing.
    /// </summary>
    public static class PresenterExtensions
    {
        /// <summary>
        /// The type of view <paramref name="presenter"/> expects, or <c>null</c> if it has no view.
        /// </summary>
        /// <remarks>
        /// This is how a presenter-opening service knows what component to load before the presenter itself
        /// exists in any typed form. <c>null</c> is a legitimate answer, not an error: a presenter with no
        /// view is a perfectly ordinary node in the tree.
        /// </remarks>
        /// <param name="presenter">The presenter to ask.</param>
        /// <returns><see cref="IPresenterWithView.ViewType"/>, or <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="presenter"/> is <c>null</c>.</exception>
        public static Type GetViewType(this Presenter presenter)
        {
            if (presenter == null)
                throw new ArgumentNullException(nameof(presenter), $"{nameof(presenter)} can't be null");

            return presenter is IPresenterWithView withView ? withView.ViewType : null;
        }

        /// <summary>
        /// Appends this presenter's direct children to <paramref name="children"/>.
        /// </summary>
        /// <remarks>
        /// A snapshot, unlike <see cref="Presenter.Children"/>: safe to iterate while closing what it holds.
        /// The list is appended to, never cleared.
        /// </remarks>
        /// <param name="presenter">The presenter whose children to collect.</param>
        /// <param name="children">The list to append to.</param>
        /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
        public static void GetChildren(this Presenter presenter, List<Presenter> children)
        {
            if (presenter == null)
                throw new ArgumentNullException(nameof(presenter), $"{nameof(presenter)} can't be null");
            if (children == null)
                throw new ArgumentNullException(nameof(children), $"{nameof(children)} can't be null");

            var source = presenter.Children;
            for (var i = 0; i < source.Count; i++)
            {
                children.Add(source[i]);
            }
        }

        /// <summary>
        /// Appends this presenter's children of type <typeparamref name="T"/> to <paramref name="children"/>,
        /// optionally searching the whole subtree.
        /// </summary>
        /// <remarks>
        /// Depth-first, parents before their own descendants, in attachment order. A snapshot; the list is
        /// appended to, never cleared. Allocation-free apart from growing <paramref name="children"/>.
        /// </remarks>
        /// <typeparam name="T">The presenter type to select.</typeparam>
        /// <param name="presenter">The presenter to search from. Itself is never included.</param>
        /// <param name="children">The list to append to.</param>
        /// <param name="recursively">
        /// <c>false</c> — direct children only. <c>true</c> — the entire subtree.
        /// </param>
        /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
        public static void GetChildren<T>(this Presenter presenter, List<T> children, bool recursively = false)
            where T : Presenter
        {
            if (presenter == null)
                throw new ArgumentNullException(nameof(presenter), $"{nameof(presenter)} can't be null");
            if (children == null)
                throw new ArgumentNullException(nameof(children), $"{nameof(children)} can't be null");

            var source = presenter.Children;
            for (var i = 0; i < source.Count; i++)
            {
                var child = source[i];
                if (child is T typed)
                {
                    children.Add(typed);
                }

                if (recursively)
                {
                    child.GetChildren(children, true);
                }
            }
        }
    }
}
