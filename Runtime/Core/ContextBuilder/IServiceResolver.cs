using System;
using OpenUGD.Services;

namespace OpenUGD.Core.ContextBuilder
{
    public interface IServiceResolver
    {
        Type[] Interfaces { get; }
        Func<IInjector, Service> Resolver { get; }
    }
}
