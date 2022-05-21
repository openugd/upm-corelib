using System;
using System.Threading.Tasks;
using OpenUGD.Core.Loggers;

namespace OpenUGD.Core.ContextBuilder
{
    public interface IContextServiceBuilder : IContextServiceSetup
    {
        static IContextServiceBuilder Default(
            Logger logger,
            Lifetime lifetime,
            IInjector injector,
            Action<ContextServiceBuilderOptions> options = null
        ) =>
            new ContextServiceBuilder(
                logger,
                lifetime,
                injector,
                null,
                options
            );

        static IContextServiceBuilder Default(
            Logger logger,
            Lifetime lifetime,
            IInjector injector,
            IServicesObserverRegister observerRegister,
            Action<ContextServiceBuilderOptions> options = null
        ) =>
            new ContextServiceBuilder(
                logger,
                lifetime,
                injector,
                observerRegister,
                options
            );

        Task AwakeServices();
        Task InitializeServices();
    }
}
