using System;
using System.Collections.Generic;

namespace OpenUGD.Commands
{
    /// <summary>
    /// Routes messages to the commands mapped for their type, and to any listener that asked for all of them.
    /// </summary>
    /// <remarks>
    /// <b>Not thread-safe.</b> Map, subscribe and tell from one thread — normally Unity's main thread. A command or
    /// a listener may map, subscribe, unsubscribe or tell while it is being told.
    /// </remarks>
    public class CommandMap : ITellMessage, IMapCommand
    {
        private readonly Context _context;
        private readonly Lifetime _lifetime;
        private readonly Dictionary<Type, CommandMapper> _map = new Dictionary<Type, CommandMapper>();

        // Copy-on-write, so Tell walks it without copying and a listener may unsubscribe while being told.
        private ITellMessage[] _tellMessages = Array.Empty<ITellMessage>();

        /// <summary>Creates a command map bound to a scope and a context.</summary>
        /// <param name="lifetime">The scope every mapper and registration is nested under. When it terminates, the
        /// map forgets its mappers and listeners.</param>
        /// <param name="context">The context commands registered by type are built from.</param>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        public CommandMap(Lifetime lifetime, Context context)
        {
            if (lifetime == null) throw new ArgumentNullException(nameof(lifetime));
            if (context == null) throw new ArgumentNullException(nameof(context));

            _lifetime = lifetime;
            _context = context;

            lifetime.AddAction(() => {
                _map.Clear();
                _tellMessages = Array.Empty<ITellMessage>();
            });
        }

        /// <summary>
        /// Returns the mapper for <typeparamref name="TMessage"/>, creating it on first use.
        /// </summary>
        /// <typeparam name="TMessage">The message type to map commands against.</typeparam>
        /// <returns>The mapper for <typeparamref name="TMessage"/>; the same instance on every call.</returns>
        /// <exception cref="InvalidOperationException">This map's scope has already terminated.</exception>
        public ICommandMapper Map<TMessage>() where TMessage : IMessage
        {
            if (_lifetime.IsTerminated)
            {
                throw new InvalidOperationException(
                    "This CommandMap's scope has already terminated, so mapping '" + typeof(TMessage) +
                    "' would silently never run.");
            }

            CommandMapper mapper;
            if (!_map.TryGetValue(typeof(TMessage), out mapper))
            {
                _map[typeof(TMessage)] = mapper = new CommandMapper(_lifetime, typeof(TMessage), _context);
            }

            return mapper;
        }

        /// <summary>
        /// Dispatches <paramref name="message"/> to the commands mapped for its exact runtime type, in registration
        /// order, then to every listener, in subscription order.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Exact type.</b> The mapper is looked up by <c>message.GetType()</c>: a mapping for a base class or an
        /// interface the message implements does not run, and neither does one for a type derived from it. A
        /// message nothing is mapped to and nobody listens to is dropped, which is not an error.
        /// </para>
        /// <para>
        /// <b>Failures.</b> Every command and every listener runs. The failures of both are then reported as one
        /// flat list: a single failure is rethrown as itself, two or more as one <see cref="AggregateException"/>
        /// whose inner exceptions are the failures themselves, commands' first.
        /// </para>
        /// </remarks>
        /// <param name="message">The message to dispatch.</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
        /// <exception cref="Exception">Exactly one command or listener failed: that exception, rethrown with its
        /// original stack trace.</exception>
        /// <exception cref="AggregateException">Two or more failed.</exception>
        public void Tell(object message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            Exception failure = null;
            List<Exception> failures = null;

            CommandMapper mapper;
            if (_map.TryGetValue(message.GetType(), out mapper))
            {
                mapper.Dispatch(message, ref failure, ref failures);
            }

            // Read once: the array is replaced, never changed, so a listener that unsubscribes while being told
            // does not disturb the walk, and nothing is copied per Tell.
            var listeners = _tellMessages;
            for (var i = 0; i < listeners.Length; i++)
            {
                try
                {
                    listeners[i].Tell(message);
                }
                catch (Exception exception)
                {
                    Failures.Add(exception, ref failure, ref failures);
                }
            }

            Failures.ThrowIfAny(failure, failures,
                " handlers failed while telling '" + message.GetType() + "'. Every handler still ran.");
        }

        /// <summary>
        /// Subscribes a listener to every message, for as long as <paramref name="lifetime"/> lives.
        /// </summary>
        /// <param name="lifetime">The scope the subscription is bound to. If it has already
        /// terminated, nothing is subscribed.</param>
        /// <param name="tellMessage">The listener. Subscribing the same listener twice makes it receive every
        /// message twice.</param>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        public void Subscribe(Lifetime lifetime, ITellMessage tellMessage)
        {
            if (lifetime == null) throw new ArgumentNullException(nameof(lifetime));
            if (tellMessage == null) throw new ArgumentNullException(nameof(tellMessage));

            if (lifetime.IsTerminated) return;

            var current = _tellMessages;
            var next = new ITellMessage[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = tellMessage;
            _tellMessages = next;

            lifetime.AddAction(() => {
                var listeners = _tellMessages;
                var index = Array.IndexOf(listeners, tellMessage);
                if (index < 0) return;

                var remaining = new ITellMessage[listeners.Length - 1];
                Array.Copy(listeners, 0, remaining, 0, index);
                Array.Copy(listeners, index + 1, remaining, index, listeners.Length - index - 1);
                _tellMessages = remaining;
            });
        }
    }
}
