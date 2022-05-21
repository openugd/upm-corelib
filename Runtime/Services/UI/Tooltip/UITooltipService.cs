using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenUGD.Core.Widgets;
using OpenUGD.Utils;
using OpenUGD.Utils.Components;
using UnityEngine.Assertions;

namespace OpenUGD.Services.UI.Tooltip
{
    public class UITooltipService : Service
    {
        private readonly Dictionary<Type, UITooltipMap> _map = new();
        private readonly List<Widget> _opened = new();
        private readonly LinkedList<Lifetime.Definition> _queue = new();
        [Inject] private IInjector _injector;
        private Signal _onChange;
        [Inject] private PrefabResourceManager _prefabResourceManager;
        [Inject] private IUITooltipProvider _providers;

        protected override Task OnAwake()
        {
            _onChange = new Signal(Lifetime);
            return base.OnAwake();
        }

        protected override Task OnInitialize()
        {
            foreach (var map in _providers.Provide())
            {
                _map[map.Type] = map;
                _injector.ToFactory(map.Type);
            }

            return base.OnInitialize();
        }

        public UITooltipReference Open<TWidget, TModel>(TModel model)
            where TWidget : Widget, IWidgetWithModel<TModel> =>
            Open(typeof(TWidget), model);

        public UITooltipReference Open(Type type, object model, Action<Widget> onOpen = null)
        {
            Assert.IsTrue(type.IsSubclassOf(typeof(Widget)));

            var definition = Lifetime.Define(Lifetime);
            var shell = new UITooltipReference(definition);
            Enqueue(type, onOpen, definition, model);
            return shell;
        }

        private void Enqueue(Type type, Action<Widget> onOpen, Lifetime.Definition lifetimeDefinition, object model)
        {
            var intersectLifetime = Lifetime.Intersection(lifetimeDefinition.Lifetime, Lifetime);
            Action<Action> action = callback => {
                var map = _map[type];
                if (map == null)
                {
                    var mediator = (Widget)_injector.Resolve(type);
                    _injector.Inject(mediator);
                    Widget.Internal.Initialize(_injector, mediator, intersectLifetime);
                    _opened.Add(mediator);
                    var modelMediator = mediator as IWidgetWithModel;
                    if (model != null)
                    {
                        Assert.IsNotNull(modelMediator);
                        modelMediator.SetModel(model);
                    }

                    Widget.Internal.Ready(mediator);
                    if (onOpen != null)
                    {
                        onOpen(mediator);
                    }

                    callback();
                    _onChange.Fire();
                    intersectLifetime.Lifetime.AddAction(() => {
                        //Widget.Internal.Close(mediator);
                        _opened.Remove(mediator);
                        _onChange.Fire();
                    });
                }
                else
                {
                    _injector.Inject(map.Provider);
                    var mediator = (Widget)_injector.Resolve(type);
                    var viewType = mediator.GetViewType();
                    map.Provider.Provide(intersectLifetime.Lifetime, map.Path, viewType, context => {
                        var tooltipComponent = context.Component;
                        tooltipComponent.gameObject.AddComponent<SignalMonoBehaviour>().DestroySignal
                            .Subscribe(intersectLifetime.Lifetime, intersectLifetime.Terminate);
                        Widget.Internal.Initialize(_injector, mediator, intersectLifetime);
                        _opened.Add(mediator);
                        var modelMediator = mediator as IWidgetWithModel;
                        if (model != null)
                        {
                            Assert.IsNotNull(modelMediator);
                            modelMediator.SetModel(model);
                        }

                        var viewMediator = (IWidgetWithView)mediator;
                        var viewComponent = tooltipComponent.GetType() != viewMediator.ViewType
                            ? tooltipComponent.GetComponent(viewMediator.ViewType)
                            : tooltipComponent;
                        viewMediator.SetView(viewComponent);

                        Widget.Internal.Ready(mediator);
                        if (onOpen != null)
                        {
                            onOpen(mediator);
                        }

                        callback();
                        _onChange.Fire();
                        intersectLifetime.Lifetime.AddAction(() => {
                            //Widget.Internal.Close(mediator);
                            _opened.Remove(mediator);
                            context.Terminate();
                            _onChange.Fire();
                        });
                    });
                }
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
