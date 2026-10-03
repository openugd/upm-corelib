using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace OpenUGD.Commands
{
    /// <summary>
    /// Dispatches one message type to every command registered for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each command is constructed fresh per message by <see cref="Context.Instantiate(Type, object[])"/>.
    /// The message, the registration's <see cref="Lifetime.Definition"/> and its <see cref="Lifetime"/> are
    /// offered as constructor arguments; every other parameter is resolved from the context.
    /// </para>
    /// <para>
    /// This is what replaced the 0.6.1 sequence "register the message into the injector, inject, execute,
    /// unregister": a throwing <see cref="ICommand.Execute"/> skipped both unregister calls, so the message
    /// stayed registered forever and the remaining commands never ran. There is now no container mutation to
    /// fail to undo, and a throwing command no longer stops the others: every failure is collected and
    /// surfaced together as an <see cref="AggregateException"/>, even when only one command failed. That last
    /// part differs from <see cref="Lifetime"/> 2.0.0 and <c>Signal</c> 2.0.0, which rethrow a single failure
    /// as itself.
    /// </para>
    /// </remarks>
    public class CommandMapper : ICommandMapper, ICommandMapperRemove, ITellMessage
    {
        private readonly List<Entry> _commands = new List<Entry>();
        private readonly Context _context;
        private readonly Lifetime _lifetime;
        private readonly Type _messageType;

        /// <summary>Creates a mapper for one message type.</summary>
        /// <param name="lifetime">The scope every registration is nested under.</param>
        /// <param name="messageType">The message type this mapper dispatches.</param>
        /// <param name="context">The context commands are instantiated from.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public CommandMapper(Lifetime lifetime, Type messageType, Context context)
        {
            if (lifetime == null) throw new ArgumentNullException(nameof(lifetime));
            if (messageType == null) throw new ArgumentNullException(nameof(messageType));
            if (context == null) throw new ArgumentNullException(nameof(context));

            _lifetime = lifetime;
            _messageType = messageType;
            _context = context;
        }

        /// <summary>The message type this mapper dispatches.</summary>
        public Type MessageType => _messageType;

        /// <inheritdoc />
        public Lifetime RegisterCommand([DynamicallyAccessedMembers(Trimming.Constructors)] Type commandType,
            bool oneTime = false)
        {
            if (commandType == null) throw new ArgumentNullException(nameof(commandType));

            if (!typeof(ICommand).IsAssignableFrom(commandType) || commandType.IsAbstract ||
                commandType.IsInterface || commandType.IsGenericTypeDefinition)
            {
                throw new ArgumentException(
                    "'" + commandType + "' cannot be registered against '" + _messageType +
                    "': a command must be a concrete, closed class implementing " + typeof(ICommand) + ".",
                    nameof(commandType));
            }

            if (_lifetime.IsTerminated)
            {
                throw new InvalidOperationException(
                    "The scope of the command map for '" + _messageType +
                    "' has already terminated, so registering '" + commandType +
                    "' would silently never run. Register before the scope ends.");
            }

            var definition = _lifetime.DefineNested(commandType.Name);
            var entry = new Entry(commandType, oneTime, definition);
            _commands.Add(entry);
            definition.Lifetime.AddAction(() => _commands.Remove(entry));
            return definition.Lifetime;
        }

        /// <inheritdoc />
        public void Remove<T>() where T : ICommand => Remove(typeof(T));

        /// <inheritdoc />
        public void Remove(Type commandType)
        {
            if (commandType == null) throw new ArgumentNullException(nameof(commandType));

            var snapshot = _commands.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i].CommandType == commandType) snapshot[i].Definition.Terminate();
            }
        }

        /// <summary>
        /// Runs every registered command against <paramref name="message"/>, in registration order.
        /// </summary>
        /// <param name="message">An instance of <see cref="MessageType"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="message"/> is not an instance of <see cref="MessageType"/>.
        /// </exception>
        /// <exception cref="AggregateException">
        /// One or more commands threw. Every command still ran; the failures are collected here.
        /// </exception>
        public void Tell(object message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            if (!_messageType.IsInstanceOfType(message))
            {
                throw new ArgumentException(
                    "This mapper dispatches '" + _messageType + "' but was told a '" + message.GetType() + "'.",
                    nameof(message));
            }

            if (_commands.Count == 0) return;

            var snapshot = _commands.ToArray();
            List<Exception> failures = null;

            for (var i = 0; i < snapshot.Length; i++)
            {
                var entry = snapshot[i];

                // Unregistered while this dispatch was in flight: do not run it, exactly as Signal
                // 2.0.0 does not invoke a handler unsubscribed mid-dispatch.
                if (entry.Definition.IsTerminated) continue;

                try
                {
                    var command = (ICommand)_context.Instantiate(
                        entry.CommandType,
                        new object[] { message, entry.Definition, entry.Definition.Lifetime });

                    command.Execute();
                }
                catch (Exception exception)
                {
                    (failures ?? (failures = new List<Exception>())).Add(exception);
                }

                if (!entry.OneTime) continue;

                try
                {
                    entry.Definition.Terminate();
                }
                catch (Exception exception)
                {
                    (failures ?? (failures = new List<Exception>())).Add(exception);
                }
            }

            if (failures == null) return;

            throw new AggregateException(
                failures.Count + " command(s) failed while handling '" + _messageType +
                "'. Every registered command still ran; call Flatten() for the leaves.", failures);
        }

        private sealed class Entry
        {
            // Annotated like the parameter it comes from and the Context.Instantiate parameter it goes to, so
            // the linker follows the command type through the field instead of losing it here (IL2077).
            [DynamicallyAccessedMembers(Trimming.Constructors)]
            internal readonly Type CommandType;

            internal readonly Lifetime.Definition Definition;
            internal readonly bool OneTime;

            internal Entry([DynamicallyAccessedMembers(Trimming.Constructors)] Type commandType, bool oneTime,
                Lifetime.Definition definition)
            {
                CommandType = commandType;
                OneTime = oneTime;
                Definition = definition;
            }
        }
    }
}
