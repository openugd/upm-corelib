#pragma warning disable CS0649
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using OpenUGD.Core.Presenters;
using OpenUGD.Utils.Components;
using UnityEngine.Assertions;

namespace OpenUGD.Services.UI.Hud
{
    /// <summary>The default <see cref="IHudService"/>, its registry and its provider.</summary>
    public class UIHudService : Service, IHudService, IUIHudProvider, IUIHudRegister
    {
        private readonly Dictionary<Type, UIHudFactoryInfo> _map = new();
        private readonly List<UIHudReference> _opened = new();
        private readonly LinkedList<Action<Action>> _queue = new();
        private ReadOnlyCollection<UIHudReference> _openedReadOnly;

        private bool _inOpenProcess;
        private Signal<Type, UIHudActionType> _onChange;

        [Inject] private IUIHudProvider _provider;

        /// <inheritdoc />
        public ReadOnlyCollection<UIHudReference> Opened =>
            _openedReadOnly ?? (_openedReadOnly = _opened.AsReadOnly());

        UIHudFactoryInfo IUIHudProvider.Get(Type type) => _map[type];

        void IUIHudRegister.AddHud(Lifetime lifetime, Type type, Action<HudOptions> options)
        {
            var option = new HudOptions();
            var factoryInfo = new UIHudFactoryInfo(type, option, options);
            _map[type] = factoryInfo;
            lifetime.AddAction(() => _map.Remove(type));
        }

        /// <inheritdoc />
        public UIHudReference Open(Type type, object model = null, Action<Presenter> onOpen = null)
        {
            Assert.IsTrue(type.IsSubclassOf(typeof(Presenter)));

            var definition = Lifetime.Define(Lifetime);
            var reference = new UIHudReference(definition);
            Enqueue(type, onOpen, definition, model, reference);
            return reference;
        }

        /// <inheritdoc />
        public T Find<T>() where T : Presenter => _opened.Find(t => t.Presenter is T)?.Presenter as T;

        /// <inheritdoc />
        public void Subscribe(Lifetime lifetime, Action<Type, UIHudActionType> listener) =>
            _onChange.Subscribe(lifetime, listener);

        /// <inheritdoc />
        protected override Task OnAwake()
        {
            _onChange = new Signal<Type, UIHudActionType>(Lifetime);
            return base.OnAwake();
        }

        private void Enqueue(Type type,
            Action<Presenter> onOpen,
            Lifetime.Definition definition,
            object model,
            UIHudReference reference
        )
        {
            Action<Action> action = callback => {
                var factoryInfo = _provider.Get(type);
                var options = factoryInfo.Options;
                var context = options.Context;
                var provider = options.Provider();
                context.Inject(provider);
                var presenter = (Presenter)context.Instantiate(type);
                var viewType = presenter.GetViewType();
                provider.Provide(
                    lifetime: definition.Lifetime,
                    options: options,
                    targetType: viewType,
                    onResult: providerContext => {
                        var view = providerContext.Component;
                        view.gameObject.AddComponent<SignalMonoBehaviour>().DestroySignal
                            .Subscribe(definition.Lifetime, definition.Terminate);

                        definition.Lifetime.AddAction(() => {
                            _opened.Remove(reference);
                            providerContext.Dispose();
                            _onChange.Fire(type, UIHudActionType.Closed);
                            reference.Presenter = null;
                        });

                        _opened.Add(reference);
                        reference.Presenter = presenter;

                        Presenter.Internal.Initialize(context, presenter, definition);

                        if (!definition.IsTerminated)
                        {
                            var modelMediator = presenter as IPresenterWithModel;
                            if (model != null)
                            {
                                Assert.IsNotNull(modelMediator);
                                modelMediator.SetModel(model);
                            }

                            if (!definition.IsTerminated)
                            {
                                // Model first, then view: attaching the view runs OnViewAdded and then
                                // the first OnRefresh, with the model already in place. That is what
                                // replaced Presenter.Internal.Ready(presenter) here.
                                var viewMediator = (IPresenterWithView)presenter;
                                var viewComponent = view.GetType() != viewMediator.ViewType
                                    ? view.GetComponent(viewMediator.ViewType)
                                    : view;
                                viewMediator.SetView(viewComponent);

                                if (!definition.IsTerminated && onOpen != null)
                                {
                                    onOpen(presenter);
                                }
                            }
                        }

                        _onChange.Fire(type, UIHudActionType.Opened);

                        callback();
                    });
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
