using System;
using OpenUGD.Core.ContextBuilder;
using OpenUGD.Core.Loggers;

namespace OpenUGD.Services.UI
{
    public interface IUIContextServiceSetup : IContextServiceSetup
    {
        static IUIContextServiceBuilder Default(
            Logger logger,
            Lifetime lifetime,
            IInjector injector,
            IServicesObserverRegister observerRegister = null,
            Action<ContextServiceBuilderOptions> options = null
        ) =>
            new UIContextServiceBuilder(
                logger,
                lifetime,
                injector,
                observerRegister,
                options
            );

        private class UIContextServiceBuilder : ContextServiceBuilder, IUIContextServiceBuilder
        {
            public UIContextServiceBuilder(
                Logger logger,
                Lifetime lifetime,
                IInjector injector,
                Action<ContextServiceBuilderOptions> options = null
            ) : base(
                logger: logger,
                lifetime: lifetime,
                injector: injector,
                observer: null,
                options: options
            )
            {
            }

            public UIContextServiceBuilder(
                Logger logger,
                Lifetime lifetime,
                IInjector injector,
                IServicesObserverRegister observerRegister,
                Action<ContextServiceBuilderOptions> options = null
            ) : base(
                logger: logger,
                lifetime: lifetime,
                injector: injector,
                observer: observerRegister,
                options: options
            )
            {
            }
        }
    }

    public interface IUIContextServiceBuilder : IUIContextServiceSetup, IContextServiceBuilder
    {
        static IUIContextServiceBuilder Default(
            Logger logger,
            Lifetime lifetime,
            IInjector injector,
            IServicesObserverRegister observerRegister = null,
            Action<ContextServiceBuilderOptions> options = null
        ) => IUIContextServiceSetup.Default(logger, lifetime, injector, observerRegister, options);
    }
}