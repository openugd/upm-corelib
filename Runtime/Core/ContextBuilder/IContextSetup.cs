namespace OpenUGD.Core.ContextBuilder
{
    public interface IContextSetup : IInjectorProvider, ILoggerProvider, ILifetimeProvider, IResolve
    {
    }
}
