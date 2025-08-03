#pragma warning disable CS0649
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using OpenUGD.Core.Widgets;
using OpenUGD.Utils.Components;
using UnityEngine.Assertions;

namespace OpenUGD.Services.UI.Hud
{
    public class UIHudService : Service, IHudService, IUIHudProvider, IUIHudRegister
    {
        private readonly Dictionary<Type, UIHudFactoryInfo> _map = new();
        private readonly List<UIHudReference> _opened = new();
        private readonly LinkedList<Action<Action>> _queue = new();
        private ReadOnlyCollection<UIHudReference> _openedReadOnly;

        private bool _inOpenProcess;
        private Signal<Type, UIHudActionType> _onChange;

        [Inject] private IUIHudProvider _provider;

        public ReadOnlyCollection<UIHudReference> Opened
            => _openedReadOnly ?? (_openedReadOnly = _opened.AsReadOnly());

        UIHudFactoryInfo IUIHudProvider.Get(Type type) => _map[type];

        void IUIHudRegister.AddHud(Lifetime lifetime, Type type, Action<HudOptions> options)
        {
            var option = new HudOptions();
            var factoryInfo = new UIHudFactoryInfo(type, option, options);
            _map[type] = factoryInfo;
            lifetime.AddAction(() => _map.Remove(type));
        }

        public UIHudReference Open(Type type, object model = null, Action<Widget> onOpen = null)
        {
            Assert.IsTrue(type.IsSubclassOf(typeof(Widget)));

            var definition = Lifetime.Define(Lifetime);
            var reference = new UIHudReference(definition);
            Enqueue(type, onOpen, definition, model, reference);
            return reference;
        }
        
        public T Find<T>() where T : Widget
            => _opened.Find(t => t.Widget is T)?.Widget as T;

        public void Subscribe(Lifetime lifetime, Action<Type, UIHudActionType> listener) =>
            _onChange.Subscribe(lifetime, listener);

        protected override Task OnAwake()
        {
            _onChange = new Signal<Type, UIHudActionType>(Lifetime);
            return base.OnAwake();
        }

        private void Enqueue(Type type,
            Action<Widget> onOpen,
            Lifetime.Definition definition,
            object model,
            UIHudReference reference
        )
        {
            Action<Action> action = callback =>
            {
                var factoryInfo = _provider.Get(type);
                var injector = factoryInfo.Options.Injector;
                var provider = factoryInfo.Options.Provider();
                injector.Inject(provider);
                var widget = (Widget)injector.Resolve(type);
                var viewType = widget.GetViewType();
                provider.Provide(
                    lifetime: definition.Lifetime,
                    options: factoryInfo.Options,
                    targetType: viewType,
                    onResult: providerContext =>
                    {
                        var view = providerContext.Component;
                        view.gameObject.AddComponent<SignalMonoBehaviour>().DestroySignal
                            .Subscribe(definition.Lifetime, definition.Terminate);

                        definition.Lifetime.AddAction(() =>
                        {
                            _opened.Remove(reference);
                            providerContext.Dispose();
                            _onChange.Fire(type, UIHudActionType.Closed);
                            reference.Widget = null;
                        });

                        _opened.Add(reference);
                        reference.Widget = widget;

                        Widget.Internal.Initialize(injector, widget, definition);

                        if (!definition.IsTerminated)
                        {
                            var modelMediator = widget as IWidgetWithModel;
                            if (model != null)
                            {
                                Assert.IsNotNull(modelMediator);
                                modelMediator.SetModel(model);
                            }

                            if (!definition.IsTerminated)
                            {
                                var viewMediator = (IWidgetWithView)widget;
                                var viewComponent = view.GetType() != viewMediator.ViewType
                                    ? view.GetComponent(viewMediator.ViewType)
                                    : view;
                                viewMediator.SetView(viewComponent);
                                if (!definition.IsTerminated)
                                {
                                    Widget.Internal.Ready(widget);

                                    if (onOpen != null)
                                    {
                                        onOpen(widget);
                                    }
                                }
                            }
                        }

                        _onChange.Fire(type, UIHudActionType.Opened);
                        
                        callback();
                    });
            };

            _queue.AddLast(action);
            definition.Lifetime.AddAction(() =>
            {
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
                first(() =>
                {
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