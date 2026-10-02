using System;
using System.Collections.Generic;

namespace OpenUGD.Commands
{
    /// <summary>
    /// Routes messages to the commands mapped for their type, and to any listener that asked for all of them.
    /// </summary>
    /// <remarks>
    /// A child scope is now a child <see cref="Context"/>, so the hand-rolled child <c>Injector</c> that
    /// 0.6.1 built here — and the teardown action that walked it unregistering every binding one at a time —
    /// are both gone.
    /// </remarks>
    public class CommandMap : ITellMessage, IMapCommand
    {
        private readonly Context _context;
        private readonly Lifetime _lifetime;
        private readonly Dictionary<Type, CommandMapper> _map = new Dictionary<Type, CommandMapper>();
        private readonly List<ITellMessage> _tellMessages = new List<ITellMessage>();

        /// <summary>Creates a command map bound to a scope and a context.</summary>
        /// <param name="lifetime">The scope every mapper and registration is nested under.</param>
        /// <param name="context">The context commands are instantiated from.</param>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        public CommandMap(Lifetime lifetime, Context context)
        {
            if (lifetime == null) throw new ArgumentNullException(nameof(lifetime));
            if (context == null) throw new ArgumentNullException(nameof(context));

            _lifetime = lifetime;
            _context = context;

            lifetime.AddAction(() => {
                _map.Clear();
                _tellMessages.Clear();
            });
        }

        /// <summary>
        /// Returns the mapper for <typeparamref name="TMessage"/>, creating it on first use.
        /// </summary>
        /// <typeparam name="TMessage">The message type to map commands against.</typeparam>
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
        /// Dispatches <paramref name="message"/> to the mapper for its exact runtime type, then to every
        /// subscriber.
        /// </summary>
        /// <param name="message">The message to dispatch.</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
        /// <exception cref="AggregateException">
        /// One or more commands or subscribers threw. All of them still ran.
        /// </exception>
        public void Tell(object message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            List<Exception> failures = null;

            CommandMapper mapper;
            if (_map.TryGetValue(message.GetType(), out mapper))
            {
                try
                {
                    mapper.Tell(message);
                }
                catch (Exception exception)
                {
                    (failures ?? (failures = new List<Exception>())).Add(exception);
                }
            }

            if (_tellMessages.Count != 0)
            {
                // Snapshot, so a listener that unsubscribes while being told does not disturb the walk.
                // 0.6.1 borrowed a pooled list here and leaked it whenever a listener threw — the same
                // defect measured and fixed in Signal 2.0.0.
                var listeners = _tellMessages.ToArray();
                for (var i = 0; i < listeners.Length; i++)
                {
                    try
                    {
                        listeners[i].Tell(message);
                    }
                    catch (Exception exception)
                    {
                        (failures ?? (failures = new List<Exception>())).Add(exception);
                    }
                }
            }

            if (failures == null) return;

            throw new AggregateException(
                failures.Count + " handler(s) failed while telling '" + message.GetType() +
                "'. Every handler still ran; call Flatten() for the leaves.", failures);
        }

        /// <summary>
        /// Subscribes a listener to every message, for as long as <paramref name="lifetime"/> lives.
        /// </summary>
        /// <param name="lifetime">The scope the subscription is bound to. If it has already
        /// terminated the subscription is removed immediately, so nothing is ever told.</param>
        /// <param name="tellMessage">The listener.</param>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        public void Subscribe(Lifetime lifetime, ITellMessage tellMessage)
        {
            if (lifetime == null) throw new ArgumentNullException(nameof(lifetime));
            if (tellMessage == null) throw new ArgumentNullException(nameof(tellMessage));

            _tellMessages.Add(tellMessage);
            lifetime.AddAction(() => _tellMessages.Remove(tellMessage));
        }
    }
}
