using System;
using System.Diagnostics.CodeAnalysis;

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
        /// <returns>The registration; terminate it to unregister.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is null.</exception>
        /// <exception cref="ArgumentException">No constructor of <typeparamref name="TCommand"/> can be satisfied;
        /// see <see cref="ICommandMapper.RegisterCommand(Type, bool)"/>.</exception>
        public static Lifetime.Definition RegisterCommand<[DynamicallyAccessedMembers(Trimming.Constructors)] TCommand>(
            this ICommandMapper mapper, bool oneTime = false)
            where TCommand : ICommand
        {
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            return mapper.RegisterCommand(typeof(TCommand), oneTime);
        }

        /// <summary>
        /// Maps <typeparamref name="TCommand"/> to <typeparamref name="TMessage"/> in one call.
        /// </summary>
        /// <typeparam name="TMessage">The message type. Routing is by exact runtime type: a message of a type
        /// derived from it does not reach this command.</typeparam>
        /// <typeparam name="TCommand">The command to run when it is told.</typeparam>
        /// <param name="map">The command map.</param>
        /// <param name="oneTime">Terminate the registration after it has run once.</param>
        /// <returns>The registration; terminate it to unregister.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="map"/> is null.</exception>
        /// <exception cref="ArgumentException">No constructor of <typeparamref name="TCommand"/> can be satisfied;
        /// see <see cref="ICommandMapper.RegisterCommand(Type, bool)"/>.</exception>
        public static Lifetime.Definition Map<TMessage, [DynamicallyAccessedMembers(Trimming.Constructors)] TCommand>(
            this IMapCommand map, bool oneTime = false)
            where TMessage : IMessage
            where TCommand : ICommand
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return map.Map<TMessage>().RegisterCommand(typeof(TCommand), oneTime);
        }

        /// <summary>
        /// Maps a command factory to <typeparamref name="TMessage"/> in one call: no reflection when the message
        /// is told.
        /// </summary>
        /// <example>
        /// <code>
        /// map.Map&lt;BuyMessage&gt;((message, lifetime) =&gt; new BuyCommand(message, shop));
        /// </code>
        /// </example>
        /// <typeparam name="TMessage">The message type. Routing is by exact runtime type.</typeparam>
        /// <param name="map">The command map.</param>
        /// <param name="factory">Builds the command from the message and the execution's
        /// <see cref="Lifetime"/>; see <see cref="ICommandMapper.RegisterCommand(Func{object, Lifetime, ICommand}, bool)"/>.</param>
        /// <param name="oneTime">Terminate the registration after it has run once.</param>
        /// <returns>The registration; terminate it to unregister.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="map"/> or <paramref name="factory"/> is
        /// null.</exception>
        public static Lifetime.Definition Map<TMessage>(this IMapCommand map,
            Func<TMessage, Lifetime, ICommand> factory, bool oneTime = false)
            where TMessage : IMessage
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return map.Map<TMessage>().RegisterCommand((message, lifetime) => factory((TMessage)message, lifetime),
                oneTime);
        }
    }
}
