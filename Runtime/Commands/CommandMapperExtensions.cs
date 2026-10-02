using System;

namespace OpenUGD.Commands
{
    /// <summary>
    /// The typed spellings of <see cref="ICommandMapper"/> and <see cref="IMapCommand"/>, kept off the
    /// interfaces so a consumer can add their own without either interface growing.
    /// </summary>
    public static class CommandMapperExtensions
    {
        /// <summary>Registers <typeparamref name="TCommand"/> against this mapper's message.</summary>
        /// <typeparam name="TCommand">A concrete command class.</typeparam>
        /// <param name="mapper">The mapper to register with.</param>
        /// <param name="oneTime">Terminate the registration after it has run once.</param>
        /// <returns>The registration's scope.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is null.</exception>
        public static Lifetime RegisterCommand<TCommand>(this ICommandMapper mapper, bool oneTime = false)
            where TCommand : ICommand
        {
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            return mapper.RegisterCommand(typeof(TCommand), oneTime);
        }

        /// <summary>
        /// Maps <typeparamref name="TCommand"/> to <typeparamref name="TMessage"/> in one call.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <typeparam name="TCommand">The command to run when it is told.</typeparam>
        /// <param name="map">The command map.</param>
        /// <param name="oneTime">Terminate the registration after it has run once.</param>
        /// <returns>The registration's scope.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="map"/> is null.</exception>
        public static Lifetime Map<TMessage, TCommand>(this IMapCommand map, bool oneTime = false)
            where TMessage : IMessage
            where TCommand : ICommand
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return map.Map<TMessage>().RegisterCommand(typeof(TCommand), oneTime);
        }
    }
}
