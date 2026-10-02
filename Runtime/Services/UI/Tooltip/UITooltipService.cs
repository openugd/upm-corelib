#pragma warning disable CS0649
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenUGD.Core.Presenters;
using OpenUGD.Utils.Components;
using UnityEngine.Assertions;

namespace OpenUGD.Services.UI.Tooltip
{
    /// <summary>Opens tooltip presenters, at most one at a time.</summary>
    public class UITooltipService : Service
    {
        private readonly Dictionary<Type, UITooltipMap> _map = new();
        private readonly List<Presenter> _opened = new();
        private readonly LinkedList<Lifetime.Definition> _queue = new();
        private Signal _onChange;

        [Inject] private OpenUGD.Context _context;
        [Inject] private IUITooltipProvider _providers;

        /// <inheritdoc />
        protected override Task OnAwake()
        {
            _onChange = new Signal(Lifetime);
            return base.OnAwake();
        }

        /// <inheritdoc />
        protected override Task OnInitialize()
        {
            // 0.6.1 also called _injector.ToFactory(map.Type) here so Resolve(type) could construct the
            // presenter. Context.Instantiate needs no registration, so the registry is all that is left.
            foreach (var map in _providers.Provide())
            {
                _map[map.Type] = map;
            }

            return base.OnInitialize();
        }

        /// <summary>Opens a registered tooltip presenter with a model.</summary>
        /// <typeparam name="TPresenter">The tooltip presenter type.</typeparam>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <param name="model">The model.</param>
        /// <returns>A handle that can close the tooltip.</returns>
        public UITooltipReference Open<TPresenter, TModel>(TModel model)
            where TPresenter : Presenter, IPresenterWithModel<TModel> =>
            Open(typeof(TPresenter), model);

        /// <summary>Opens a registered tooltip presenter.</summary>
        /// <param name="type">The presenter type; must derive from <see cref="Presenter"/>.</param>
        /// <param name="model">The model, or null.</param>
        /// <param name="onOpen">Invoked once the presenter, its view and its model are wired.</param>
        /// <returns>A handle that can close the tooltip.</returns>
        /// <exception cref="ArgumentException">
        /// No tooltip is registered for <paramref name="type"/>.
        /// </exception>
        public UITooltipReference Open(Type type, object model, Action<Presenter> onOpen = null)
        {
            Assert.IsTrue(type.IsSubclassOf(typeof(Presenter)));

            var definition = Lifetime.Define(Lifetime);
            var shell = new UITooltipReference(definition);
            Enqueue(type, onOpen, definition, model);
            return shell;
        }

        private void Enqueue(Type type, Action<Presenter> onOpen, Lifetime.Definition lifetimeDefinition, object model)
        {
            var intersectLifetime = Lifetime.Intersection(lifetimeDefinition.Lifetime, Lifetime);

            UITooltipMap map;
            if (!_map.TryGetValue(type, out map) || map == null)
            {
                throw new ArgumentException(
                    "No tooltip is registered for '" + type + "'. Call RegisterTooltip<" + type.Name +
                    ">(path) on the ContextBuilder before opening it.", nameof(type));
            }

            Action<Action> action = callback => {
                _context.Inject(map.Provider);
                var mediator = (Presenter)_context.Instantiate(type);
                var viewType = mediator.GetViewType();
                map.Provider.Provide(intersectLifetime.Lifetime, new Options { Path = map.Path }, viewType,
                    context => {
                        var tooltipComponent = context.Component;
                        tooltipComponent.gameObject.AddComponent<SignalMonoBehaviour>().DestroySignal
                            .Subscribe(intersectLifetime.Lifetime, intersectLifetime.Terminate);
                        Presenter.Internal.Initialize(_context, mediator, intersectLifetime);
                        _opened.Add(mediator);
                        var modelMediator = mediator as IPresenterWithModel;
                        if (model != null)
                        {
                            Assert.IsNotNull(modelMediator);
                            modelMediator.SetModel(model);
                        }

                        // Model first, then view: SetView runs OnViewAdded and then the first
                        // OnRefresh. Presenter.Internal.Ready(mediator) stood here and is gone, not
                        // replaced.
                        var viewMediator = (IPresenterWithView)mediator;
                        var viewComponent = tooltipComponent.GetType() != viewMediator.ViewType
                            ? tooltipComponent.GetComponent(viewMediator.ViewType)
                            : tooltipComponent;
                        viewMediator.SetView(viewComponent);

                        if (onOpen != null)
                        {
                            onOpen(mediator);
                        }

                        callback();
                        _onChange.Fire();
                        intersectLifetime.Lifetime.AddAction(() => {
                            _opened.Remove(mediator);
                            context.Dispose();
                            _onChange.Fire();
                        });
                    });
            };

            intersectLifetime.Lifetime.AddAction(() => _queue.Remove(intersectLifetime));
            if (!intersectLifetime.IsTerminated)
            {
                _queue.AddLast(intersectLifetime);

                action(() => {
                    if (_queue.Count > 1)
                    {
                        var first = _queue.First;
                        _queue.RemoveFirst();
                        first.Value.Terminate();
                    }
                });
            }
        }
    }
}
