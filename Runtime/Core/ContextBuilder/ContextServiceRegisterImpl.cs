using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenUGD.Core.Loggers;
using OpenUGD.Services;

namespace OpenUGD.Core.ContextBuilder
{
    public class ContextServiceRegisterImpl : IServiceRegister
    {
        private readonly IServicesObserverRegister _observer;
        private readonly ContextServiceBuilderOptions _options;
        private readonly List<IServiceResolver> _serviceFactories = new();
        private List<Service> _services;

        public ContextServiceRegisterImpl(
            IInjector injector,
            IServicesObserverRegister observer,
            Action<ContextServiceBuilderOptions> options
        )
        {
            Injector = injector;
            _observer = observer;
            _options = new ContextServiceBuilderOptions();
            if (options != null)
            {
                options(_options);
            }
        }

        public ContextServiceRegisterImpl(IInjector injector) => Injector = injector;

        public IInjector Injector { get; }

        public void AddService(IServiceResolver resolver) => _serviceFactories.Add(resolver);

        internal async Task Awake(Lifetime lifetime, Logger logger)
        {
            if ((logger.LogFlag & LoggerFlag.Verbose) != 0)
            {
                logger.V("Begin to prepare services");
            }

            _services = new List<Service>(_serviceFactories.Count);
            foreach (var binder in _serviceFactories)
            {
                var service = binder.Resolver(Injector);
                if (_observer != null)
                {
                    _observer.Register(service);
                }

                if (service == null)
                {
                    var interfaces = "";
                    if (binder.Interfaces != null && binder.Interfaces.Length != 0)
                    {
                        interfaces = string.Join(",", binder.Interfaces.Select(t => t.Name));
                    }

                    throw new NullReferenceException($"Service {interfaces} cannot be null");
                }

                if (binder.Interfaces != null && binder.Interfaces.Length != 0)
                {
                    foreach (var binderInterface in binder.Interfaces)
                    {
                        Injector.ToValue(binderInterface, service);
                    }
                }
                else
                {
                    Injector.ToValue(service);
                }

                Service.Internal.Inject(service, lifetime, logger.WithTag(service.GetType()), Injector);
                _services.Add(service);
            }

            if ((logger.LogFlag & LoggerFlag.Verbose) != 0)
            {
                logger.V($"Services [\n{string.Join(",\n", _services.Select(x => x.GetType().Name))}\n]");
            }

            foreach (var service in _services)
            {
                Injector.Inject(service);
            }

            await Task.Yield();
            if (lifetime.IsTerminated)
            {
                return;
            }

            var tasks = new List<Task>(_services.Count);
            foreach (var service in _services)
            {
                if ((logger.LogFlag & LoggerFlag.Verbose) != 0)
                {
                    logger.V($"{service.GetType().Name}.OnAwake");
                }

                var task = Service.Internal.OnAwake(service);
                tasks.Add(task);
                if ((logger.LogFlag & LoggerFlag.Verbose) != 0)
                {
                    OnComplete(logger, task, $"{service.GetType().Name}.OnAwake->Completed");
                }

                if (_options.InitializationStrategy == ContextServiceInitializationStrategy.Sequential)
                {
                    await task;
                }

                if (lifetime.IsTerminated)
                {
                    return;
                }
            }

            await Task.WhenAll(tasks);
        }

        internal async Task Initialize(Logger logger)
        {
            var tasks = new List<Task>(_services.Count);
            foreach (var service in _services)
            {
                if (service.Lifetime.IsTerminated)
                {
                    return;
                }

                if ((logger.LogFlag & LoggerFlag.Verbose) != 0)
                {
                    logger.V($"{service.GetType().Name}.OnInitialize");
                }

                var task = Service.Internal.OnInitialize(service);
                tasks.Add(task);
                if ((logger.LogFlag & LoggerFlag.Verbose) != 0)
                {
                    OnComplete(logger, task, $"{service.GetType().Name}.OnInitialize->Completed");
                }

                if (_options.InitializationStrategy == ContextServiceInitializationStrategy.Sequential)
                {
                    await task;
                }

                if (service.Lifetime.IsTerminated)
                {
                    return;
                }
            }

            await Task.WhenAll(tasks);
        }

        private async void OnComplete(Logger logger, Task task, string message)
        {
            try
            {
                await task;
                logger.V(message);
            }
            catch (Exception e)
            {
                logger.E($"{e}\n{message}");
                throw;
            }
        }
    }
}
