using System.Threading.Tasks;

namespace OpenUGD.Core.ContextBuilder
{
    public abstract class ContextStartup<TBuilderInterface, TContext>
        where TBuilderInterface : IContextSetup
        where TContext : IResolve
    {
        protected abstract void OnAwake(TBuilderInterface setup);
        protected abstract void OnConfigure(TContext context);
        protected abstract void OnStart(TContext context);

        public static async Task Install<TBuilderImpl>(
            ContextStartup<TBuilderInterface, TContext> startup,
            TContext instance,
            TBuilderImpl serviceBuilder
        )
            where TBuilderImpl : TBuilderInterface, IContextServiceBuilder
        {
            var context = instance;

            startup.OnAwake(serviceBuilder);
            if (serviceBuilder.Lifetime.IsTerminated)
            {
                return;
            }

            await serviceBuilder.AwakeServices();
            if (serviceBuilder.Lifetime.IsTerminated)
            {
                return;
            }

            startup.OnConfigure(context);

            await serviceBuilder.InitializeServices();
            if (serviceBuilder.Lifetime.IsTerminated)
            {
                return;
            }

            startup.OnStart(context);
        }
    }
}
