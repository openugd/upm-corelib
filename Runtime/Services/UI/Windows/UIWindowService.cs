#pragma warning disable CS0649

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using OpenUGD.Core.Presenters;
using OpenUGD.Utils.Components;
using Assert = UnityEngine.Assertions.Assert;

namespace OpenUGD.Services.UI.Windows
{
    /// <summary>The default <see cref="IUIWindowService"/>, its registry and its provider.</summary>
    public class UIWindowService : Service, IUIWindowService, IUIWindowsProvider, IUIWindowsRegister
    {
        private readonly Dictionary<Type, UIWindowFactoryInfo> _maps = new();
        private readonly List<UIWindowReference> _opened = new();
        private readonly LinkedList<UIWindowReference> _queue = new();
        private ReadOnlyCollection<UIWindowReference> _openedReadOnly;

        private Signal<Type, UIWindowActionType> _onChanged;

        [Inject] private IUIWindowsProvider _provider;

        /// <inheritdoc />
        public IEnumerable<UIWindowReference> Queue => _queue;

        /// <inheritdoc />
        public ReadOnlyCollection<UIWindowReference> Opened =>
            _openedReadOnly ?? (_openedReadOnly = _opened.AsReadOnly());

        UIWindowFactoryInfo IUIWindowsProvider.Get(Type type) => _maps[type];

        void IUIWindowsRegister.AddWindow(Lifetime lifetime, Type type, Action<WindowOptions> options)
        {
            var option = new WindowOptions();
            var factoryInfo = new UIWindowFactoryInfo(type, option, options);
            _maps[type] = factoryInfo;
            lifetime.AddAction(() => _maps.Remove(type));
        }

        /// <inheritdoc />
        public UIWindowReference Open(Type type, Action<Presenter> onOpen, object model)
        {
            if (!type.IsSubclassOf(typeof(Presenter)))
            {
                throw new ArgumentException($"{nameof(type)} is not subclass of {typeof(Presenter)}");
            }

            var factoryInfo = _provider.Get(type);
            if (factoryInfo == null)
            {
                throw new ArgumentException($"No window factory registered for {type}");
            }

            var definition = Lifetime.Define(Lifetime);
            var reference = new UIWindowReference(definition, factoryInfo.Options, type, model);
            var context = new UIWindowContext(reference, definition);
            Enqueue(type, onOpen, context, model);
            return reference;
        }

        /// <inheritdoc />
        public void Subscribe(Lifetime lifetime, Action<Type, UIWindowActionType> listener) =>
            _onChanged.Subscribe(lifetime, listener);

        /// <inheritdoc />
        protected override Task OnAwake()
        {
            _onChanged = new Signal<Type, UIWindowActionType>(Lifetime);
            return base.OnAwake();
        }

        private void Enqueue(Type type, Action<Presenter> onOpen, UIWindowContext windowContext, object model)
        {
            var definition = windowContext.Definition;
            windowContext.Factory = callback => {
                var factoryInfo = _provider.Get(type);
                var options = factoryInfo.Options;
                var context = options.Context;
                var provider = options.Provider();
                context.Inject(provider);
                var presenter = (Presenter)context.Instantiate(type);
                var viewType = presenter.GetViewType();
                provider.Provide(definition.Lifetime, options, viewType,
                    providerContext => {
                        var view = providerContext.Component;

                        var signal = view.gameObject.GetComponent<SignalMonoBehaviour>();
                        if (ReferenceEquals(signal, null) || signal == null)
                        {
                            signal = view.gameObject.AddComponent<SignalMonoBehaviour>();
                        }

                        signal.DestroySignal.Subscribe(definition.Lifetime, definition.Terminate);

                        definition.Lifetime.AddAction(() => {
                            _opened.Remove(windowContext.Reference);
                            providerContext.Dispose();
                            _onChanged.Fire(type, UIWindowActionType.Closed);
                            windowContext.Reference.Presenter = null;
                        });

                        _opened.Add(windowContext.Reference);
                        windowContext.Reference.Presenter = presenter;

                        Presenter.Internal.Initialize(context, presenter, definition);

                        if (!definition.IsTerminated)
                        {
                            var mediatorModel = presenter as IPresenterWithModel;
                            if (model != null)
                            {
                                Assert.IsNotNull(mediatorModel);
                                mediatorModel.SetModel(model);
                            }

                            if (!definition.IsTerminated)
                            {
                                // Attaching the view is what runs OnViewAdded and then the first
                                // OnRefresh. There is nothing left to push afterwards, which is why
                                // the Presenter.Internal.Ready(presenter) call that stood here is gone
                                // rather than replaced.
                                var mediatorView = (IPresenterWithView)presenter;
                                var viewComponent = view.GetType() != mediatorView.ViewType
                                    ? view.GetComponent(mediatorView.ViewType)
                                    : view;
                                mediatorView.SetView(viewComponent);

                                if (!definition.IsTerminated && onOpen != null)
                                {
                                    onOpen(presenter);
                                }
                            }
                        }

                        _onChanged.Fire(type, UIWindowActionType.Opened);

                        callback();
                    });
            };

            _queue.AddLast(windowContext.Reference);
            definition.Lifetime.AddAction(() => { _queue.Remove(windowContext.Reference); });
            windowContext.Factory(() => { });
        }

        private class UIWindowContext
        {
            public Action<Action> Factory;

            public UIWindowContext(UIWindowReference reference, Lifetime.Definition definition)
            {
                Reference = reference;
                Definition = definition;
            }

            public Lifetime.Definition Definition { get; }

            public UIWindowReference Reference { get; }
        }
    }
}
