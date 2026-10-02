using System;
using System.Runtime.CompilerServices;
using OpenUGD.Commands;

namespace OpenUGD.Services.Commands
{
    /// <summary>Registers and reaches a <see cref="CommandMap"/> on a context.</summary>
    public static class CommandMapExtensions
    {
        /// <summary>
        /// Registers <see cref="CommandMap"/> as <see cref="ITellMessage"/> and <see cref="IMapCommand"/>,
        /// unless the consumer already registered their own.
        /// </summary>
        /// <param name="services">The collection to register into.</param>
        /// <param name="file">Filled by the compiler; reported if the registration fails to build.</param>
        /// <param name="line">Filled by the compiler; reported if the registration fails to build.</param>
        /// <returns><paramref name="services"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public static ServiceCollection AddCommandMap(this ServiceCollection services,
            [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            if (!services.Contains(typeof(IMapCommand)))
            {
                services.Add(context => new CommandMap(context.Lifetime, context), file, line)
                    .As<ITellMessage>()
                    .As<IMapCommand>();
            }

            return services;
        }

        /// <summary>Resolves the command map.</summary>
        /// <param name="context">The context.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        /// <exception cref="ContextException">No command map is registered.</exception>
        public static IMapCommand MapCommand(this Context context) => context.Resolve<IMapCommand>();

        /// <summary>Tells <paramref name="message"/> through the registered command map.</summary>
        /// <param name="context">The context.</param>
        /// <param name="message">The message.</param>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        /// <exception cref="ContextException">No command map is registered.</exception>
        public static void Tell(this Context context, IMessage message) =>
            context.Resolve<ITellMessage>().Tell(message);
    }
}
