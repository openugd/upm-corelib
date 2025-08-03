#pragma warning disable CS0649

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using OpenUGD.Core.Widgets;
using OpenUGD.Utils.Components;
using Assert = UnityEngine.Assertions.Assert;

namespace OpenUGD.Services.UI.Windows
{
    public class UIWindowService : Service, IUIWindowService, IUIWindowsProvider, IUIWindowsRegister
    {
        private readonly Dictionary<Type, UIWindowFactoryInfo> _maps = new();
        private readonly List<UIWindowReference> _opened = new();
        private readonly LinkedList<UIWindowReference> _queue = new();
        private ReadOnlyCollection<UIWindowReference> _openedReadOnly;

        private Signal<Type, UIWindowActionType> _onChanged;

        [Inject] private IUIWindowsProvider _provider;

        public IEnumerable<UIWindowReference> Queue => _queue;
        public ReadOnlyCollection<UIWindowReference> Opened => _openedReadOnly ?? (_openedReadOnly = _opened.AsReadOnly());

        UIWindowFactoryInfo IUIWindowsProvider.Get(Type type) => _maps[type];

        void IUIWindowsRegister.AddWindow(Lifetime lifetime, Type type, Action<WindowOptions> options)
        {
            var option = new WindowOptions();
            var factoryInfo = new UIWindowFactoryInfo(type, option, options);
            _maps[type] = factoryInfo;
            lifetime.AddAction(() => _maps.Remove(type));
        }

        public UIWindowReference Open(Type type, Action<Widget> onOpen, object model)
        {
            if (!type.IsSubclassOf(typeof(Widget)))
            {
                throw new ArgumentException($"{nameof(type)} is not subclass of {typeof(Widget)}");
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

        public void Subscribe(Lifetime lifetime, Action<Type, UIWindowActionType> listener) =>
            _onChanged.Subscribe(lifetime, listener);

        protected override Task OnAwake()
        {
            _onChanged = new Signal<Type, UIWindowActionType>(Lifetime);
            return base.OnAwake();
        }

        private void Enqueue(Type type, Action<Widget> onOpen, UIWindowContext context, object model)
        {
            var definition = context.Definition;
            context.Factory = callback => {
                var factoryInfo = _provider.Get(type);
                var injector = factoryInfo.Options.Injector;
                var provider = factoryInfo.Options.Provider();
                injector.Inject(provider);
                var widget = (Widget)injector.Resolve(type);
                var viewType = widget.GetViewType();
                provider.Provide(definition.Lifetime, factoryInfo.Options, viewType,
                    providerContext => {
                        var view = providerContext.Component;

                        var signal = view.gameObject.GetComponent<SignalMonoBehaviour>();
                        if (ReferenceEquals(signal, null) || signal == null)
                        {
                            signal = view.gameObject.AddComponent<SignalMonoBehaviour>();
                        }

                        signal.DestroySignal.Subscribe(definition.Lifetime, definition.Terminate);

                        definition.Lifetime.AddAction(() => {
                            //Widget.Internal.Close(mediator);
                            _opened.Remove(context.Reference);
                            providerContext.Dispose();
                            _onChanged.Fire(type, UIWindowActionType.Closed);
                            context.Reference.Widget = null;
                        });

                        _opened.Add(context.Reference);
                        context.Reference.Widget = widget;

                        Widget.Internal.Initialize(injector, widget, definition);

                        if (!definition.IsTerminated)
                        {
                            var mediatorModel = widget as IWidgetWithModel;
                            if (model != null)
                            {
                                Assert.IsNotNull(mediatorModel);
                                mediatorModel.SetModel(model);
                            }

                            if (!definition.IsTerminated)
                            {
                                var mediatorView = (IWidgetWithView)widget;
                                var viewComponent = view.GetType() != mediatorView.ViewType
                                    ? view.GetComponent(mediatorView.ViewType)
                                    : view;
                                mediatorView.SetView(viewComponent);

                                Widget.Internal.Ready(widget);
                                if (!definition.IsTerminated)
                                {
                                    if (onOpen != null)
                                    {
                                        onOpen(widget);
                                    }
                                }
                            }
                        }
                        _onChanged.Fire(type, UIWindowActionType.Opened);

                        callback();
                    });
            };
            
            _queue.AddLast(context.Reference);
            definition.Lifetime.AddAction(() => { _queue.Remove(context.Reference); });
            context.Factory(() => { });
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
