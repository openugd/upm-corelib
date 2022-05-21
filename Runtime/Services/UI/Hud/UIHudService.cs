#pragma warning disable CS0649
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenUGD.Core.Widgets;
using OpenUGD.Utils;
using OpenUGD.Utils.Components;
using UnityEngine.Assertions;

namespace OpenUGD.Services.UI.Hud
{
    public class UIHudService : Service
    {
        private readonly Dictionary<Type, UIHudMap> _map = new();
        private readonly List<Widget> _opened = new();
        private readonly LinkedList<Action<Action>> _queue = new();
        [Inject] private IInjector _injector;
        private bool _inOpenProcess;
        private Signal _onChange;
        [Inject] private PrefabResourceManager _prefabResourceManager;
        [Inject] private IUIHudProvider _providers;

        public Widget[] Opened => _opened.ToArray();

        public UIHudReference Open<TWidget>(Action<TWidget> onOpen = null) where TWidget : Widget
        {
            var definition = Lifetime.Define(Lifetime);
            var shell = new UIHudReference(definition);
            Enqueue(typeof(TWidget), widget => onOpen?.Invoke((TWidget)widget), definition, null);
            return shell;
        }

        public UIHudReference Open<TWidget, TModel>(TModel model, Action<TWidget> onOpen = null)
            where TWidget : Widget, IWidgetWithModel<TModel>
            where TModel : class
        {
            var definition = Lifetime.Define(Lifetime);
            var shell = new UIHudReference(definition);
            Enqueue(typeof(TWidget), widget => onOpen?.Invoke((TWidget)widget), definition, model);
            return shell;
        }

        public UIHudReference Open(Type type, Action<Widget> onOpen = null)
        {
            Assert.IsTrue(type.IsSubclassOf(typeof(Widget)));

            var definition = Lifetime.Define(Lifetime);
            var shell = new UIHudReference(definition);
            Enqueue(type, onOpen, definition, null);
            return shell;
        }

        public T Get<T>() where T : Widget => (T)_opened.Find(t => t is T);

        public void SubscribeOnChange(Lifetime lifetime, Action listener) =>
            _onChange.Subscribe(lifetime, listener);

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

        private void Enqueue(Type type, Action<Widget> onOpen, Lifetime.Definition definition, object model)
        {
            Action<Action> action = callback => {
                var map = _map[type];
                if (map == null)
                {
                    var mediator = (Widget)_injector.Resolve(type);
                    _injector.Inject(mediator);

                    definition.Lifetime.AddAction(() => {
                        //Widget.Internal.Close(mediator);
                        _opened.Remove(mediator);
                        _onChange.Fire();
                    });

                    _opened.Add(mediator);

                    Widget.Internal.Initialize(_injector, mediator, definition);

                    if (!definition.IsTerminated)
                    {
                        var modelMediator = mediator as IWidgetWithModel;
                        if (model != null)
                        {
                            Assert.IsNotNull(modelMediator);
                            modelMediator.SetModel(model);
                        }

                        if (!definition.IsTerminated)
                        {
                            Widget.Internal.Ready(mediator);

                            if (onOpen != null)
                            {
                                onOpen(mediator);
                            }
                        }
                    }

                    callback();
                    _onChange.Fire();
                }
                else
                {
                    _injector.Inject(map.Provider);
                    var mediator = (Widget)_injector.Resolve(type);
                    var viewType = mediator.GetViewType();
                    map.Provider.Provide(definition.Lifetime, map.Path, viewType, providerContext => {
                        var view = providerContext.Component;
                        view.gameObject.AddComponent<SignalMonoBehaviour>().DestroySignal
                            .Subscribe(definition.Lifetime, definition.Terminate);

                        definition.Lifetime.AddAction(() => {
                            //Widget.Internal.Close(mediator);
                            _opened.Remove(mediator);
                            providerContext.Terminate();
                            _onChange.Fire();
                        });

                        _opened.Add(mediator);

                        Widget.Internal.Initialize(_injector, mediator, definition);

                        if (!definition.IsTerminated)
                        {
                            var modelMediator = mediator as IWidgetWithModel;
                            if (model != null)
                            {
                                Assert.IsNotNull(modelMediator);
                                modelMediator.SetModel(model);
                            }

                            if (!definition.IsTerminated)
                            {
                                var viewMediator = (IWidgetWithView)mediator;
                                var viewComponent = view.GetType() != viewMediator.ViewType
                                    ? view.GetComponent(viewMediator.ViewType)
                                    : view;
                                viewMediator.SetView(viewComponent);
                                if (!definition.IsTerminated)
                                {
                                    Widget.Internal.Ready(mediator);

                                    if (onOpen != null)
                                    {
                                        onOpen(mediator);
                                    }
                                }
                            }
                        }

                        callback();

                        _onChange.Fire();
                    });
                }
            };

            _queue.AddLast(action);
            definition.Lifetime.AddAction(() => {
                _queue.Remove(action);
                if (_queue.Count == 0)
                {
                    _inOpenProcess = false;
                }
            });

            if (!_inOpenProcess)
            {
                OpenProcess();
            }
        }

        private void OpenProcess() => Execute();

        private void Execute()
        {
            if (_queue.Count != 0)
            {
                var first = _queue.First.Value;
                _queue.RemoveFirst();
                first(() => {
                    if (_queue.Count != 0)
                    {
                        OpenProcess();
                    }
                    else
                    {
                        _inOpenProcess = false;
                    }
                });
            }
        }
    }
}
