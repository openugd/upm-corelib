using System;
using System.Threading.Tasks;
using OpenUGD.Core.Loggers;

namespace OpenUGD.Services
{
    public enum ServiceState
    {
        Created,
        Awaking,
        WokeUp,
        Initializing,
        Initialized,
        Terminated
    }

    public abstract class Service : IResolve
    {
        private IResolve _resolve;

        public Lifetime Lifetime { get; private set; }
        public Logger Logger { get; private set; }
        public ServiceState State { get; private set; }

        object IResolve.Resolve(Type type) => _resolve.Resolve(type);

        protected virtual Task OnAwake() => Task.CompletedTask;

        protected virtual Task OnInitialize() => Task.CompletedTask;

        protected T Resolve<T>() => (T)_resolve.Resolve(typeof(T));

        public static class Internal
        {
            public static void Inject(Service service, Lifetime lifetime, Logger logger, IResolve resolve)
            {
                service._resolve = resolve;
                service.Logger = logger;
                service.Lifetime = lifetime;
                lifetime.AddAction(() => { service.State = ServiceState.Terminated; });
            }

            public static Task OnAwake(Service service)
            {
                if (service.Lifetime.IsTerminated)
                {
                    throw new InvalidOperationException($"{service}.{nameof(OnAwake)} cannot be terminated");
                }

                if (service.State != ServiceState.Created)
                {
                    throw new InvalidOperationException(
                        $"{service} expects a {ServiceState.Created} state, but receives {service.State}");
                }

                service.State = ServiceState.Awaking;

                var task = service.OnAwake();

                async void OnComplete(Task t)
                {
                    await t;
                    if (service.State != ServiceState.Awaking)
                    {
                        throw new InvalidOperationException(
                            $"{service} expects an {ServiceState.Awaking} state, but receives {service.State}");
                    }

                    service.State = ServiceState.WokeUp;
                }

                OnComplete(task);
                return task;
            }

            public static Task OnInitialize(Service service)
            {
                if (service.Lifetime.IsTerminated)
                {
                    throw new InvalidOperationException($"{service}.{nameof(OnInitialize)} cannot be terminated");
                }

                if (service.State != ServiceState.WokeUp)
                {
                    throw new InvalidOperationException(
                        $"{service} expects a {ServiceState.WokeUp} state, but receives {service.State}");
                }

                service.State = ServiceState.Initializing;

                var task = service.OnInitialize();

                async void OnComplete(Task t)
                {
                    await t;
                    if (service.State != ServiceState.Initializing)
                    {
                        throw new InvalidOperationException(
                            $"{service} expects an {ServiceState.Initializing} state, but receives {service.State}");
                    }

                    service.State = ServiceState.Initialized;
                }

                OnComplete(task);
                return task;
            }
        }
    }
}
