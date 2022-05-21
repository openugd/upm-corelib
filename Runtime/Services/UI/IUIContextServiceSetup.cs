using OpenUGD.Core.ContextBuilder;
using OpenUGD.Core.Loggers;

namespace OpenUGD.Services.UI
{
    public interface IUIContextServiceSetup : IContextServiceSetup
    {
        public static IUIContextServiceBuilder Default(Logger logger, Lifetime lifetime, IInjector injector) =>
            new UIContextServiceBuilder(logger, lifetime, injector);

        public static IUIContextServiceBuilder Default(Logger logger, Lifetime lifetime, IInjector injector,
            IServicesObserverRegister observerRegister) =>
            new UIContextServiceBuilder(logger, lifetime, injector, observerRegister);

        private class UIContextServiceBuilder : ContextServiceBuilder, IUIContextServiceBuilder
        {
            public UIContextServiceBuilder(
                Logger logger,
                Lifetime lifetime,
                IInjector injector
            ) : base(logger,
                lifetime,
                injector)
            {
            }

            public UIContextServiceBuilder(
                Logger logger,
                Lifetime lifetime,
                IInjector injector,
                IServicesObserverRegister observerRegister
            ) : base(logger, lifetime, injector, observerRegister)
            {
            }
        }
    }

    public interface IUIContextServiceBuilder : IUIContextServiceSetup, IContextServiceBuilder
    {
    }
}
