using System;
using OpenUGD.Services;

namespace OpenUGD.Core.ContextBuilder
{
    public class ServiceResolver : IServiceResolver
    {
        public ServiceResolver(Type[] interfaces, Func<IInjector, Service> resolver)
        {
            Interfaces = interfaces;
            Resolver = resolver;
        }

        public ServiceResolver(Func<IInjector, Service> resolver) => Resolver = resolver;

        public Type[] Interfaces { get; }
        public Func<IInjector, Service> Resolver { get; }
    }
}
