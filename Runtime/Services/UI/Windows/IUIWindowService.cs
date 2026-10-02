using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OpenUGD.Core.Presenters;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>Opens, tracks and reports window presenters.</summary>
    /// <remarks>
    /// No longer <c>IResolve</c>: a window service that is also a service locator is the thing the v2 reset
    /// exists to remove. Resolve from the <see cref="Context"/> instead.
    /// </remarks>
    public interface IUIWindowService
    {
        /// <summary>Every live handle, opened or still loading.</summary>
        IEnumerable<UIWindowReference> Queue { get; }

        /// <summary>The windows currently open.</summary>
        ReadOnlyCollection<UIWindowReference> Opened { get; }

        /// <summary>Opens a registered window presenter.</summary>
        /// <param name="type">The presenter type; must derive from <see cref="Presenter"/>.</param>
        /// <param name="onOpen">Invoked once the presenter, its view and its model are wired.</param>
        /// <param name="model">The model, or null.</param>
        /// <returns>A handle that can close the window.</returns>
        UIWindowReference Open(Type type, Action<Presenter> onOpen, object model);

        /// <summary>Subscribes to open and close notifications.</summary>
        /// <param name="lifetime">The scope the subscription is bound to.</param>
        /// <param name="listener">Receives the presenter type and what happened.</param>
        void Subscribe(Lifetime lifetime, Action<Type, UIWindowActionType> listener);
    }
}
