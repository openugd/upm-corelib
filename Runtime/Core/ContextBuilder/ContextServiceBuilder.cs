using System;
using System.Threading.Tasks;
using OpenUGD.Core.Loggers;

namespace OpenUGD.Core.ContextBuilder
{
    public class ContextServiceBuilder : IContextServiceBuilder
    {
        private readonly ContextServiceRegisterImpl _serviceImpl;

        public ContextServiceBuilder(
            Logger logger,
            Lifetime lifetime,
            IInjector injector,
            IServicesObserverRegister observer = null,
            Action<ContextServiceBuilderOptions> options = null
        )
        {
            Logger = logger;
            Lifetime = lifetime;
            Injector = injector;
            _serviceImpl = new ContextServiceRegisterImpl(injector, observer, options);
        }

        public Logger Logger { get; }
        public Lifetime Lifetime { get; }
        public IInjector Injector { get; }

        public void AddService(IServiceResolver resolver) => _serviceImpl.AddService(resolver);

        public async Task AwakeServices() => await _serviceImpl.Awake(Lifetime, Logger);

        public async Task InitializeServices() => await _serviceImpl.Initialize(Logger);

        public object Resolve(Type type) => Injector.Resolve(type);
    }
}
