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
    /// A command registered by type is constructed fresh per message by
    /// <see cref="Context.Instantiate(Type, object[])"/>, which offers the message, the registration's
    /// <see cref="Lifetime.Definition"/> and the execution's <see cref="Lifetime"/> as constructor arguments and
    /// resolves every other parameter from the context; that this can succeed is checked at registration. A
    /// command registered with a factory is built by the factory, with no reflection. Either way each execution
    /// gets its own <see cref="Lifetime"/>, nested in the registration's and terminated when the command
    /// returns.
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
    /// <para>
    /// <b>Not thread-safe.</b> Register, remove and tell from one thread — normally Unity's main thread. A
    /// command may register, remove or tell from inside <see cref="ICommand.Execute"/>: the list of commands is
    /// replaced, never changed in place, so a dispatch in progress keeps walking the list it started with, and
    /// skips a command removed in the meantime.
    /// </para>
    /// </remarks>
    public class CommandMapper : ICommandMapper, ICommandMapperRemove, ITellMessage
    {
        private readonly Context _context;
        private readonly Lifetime _lifetime;
        private readonly Type _messageType;

        // Copy-on-write: registering and removing replace the array; Tell reads it once and walks it without a copy.
        private Entry[] _commands = Array.Empty<Entry>();

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
        public Lifetime.Definition RegisterCommand(
            [DynamicallyAccessedMembers(Trimming.Constructors)] Type commandType, bool oneTime = false)
        {
            if (commandType == null) throw new ArgumentNullException(nameof(commandType));

            // A struct passes the other tests, and Context.Instantiate refuses every value type, so it is refused
            // here rather than on every Tell.
            if (!typeof(ICommand).IsAssignableFrom(commandType) || commandType.IsAbstract ||
                commandType.IsInterface || commandType.IsValueType || commandType.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    "'" + commandType + "' cannot be registered against '" + _messageType +
                    "': a command must be a concrete, closed class implementing " + typeof(ICommand) + ".",
                    nameof(commandType));
            }

            ThrowIfEnded(commandType.Name);

            var reason = CommandConstructors.DescribeUnsatisfiable(_context, commandType, _messageType);
            if (reason != null)
            {
                throw new ArgumentException(
                    "'" + commandType + "' cannot be registered against '" + _messageType + "': " + reason +
                    " A command's constructor may take the message, a Lifetime.Definition (the registration), a " +
                    "Lifetime (the execution) and any service registered in the context.",
                    nameof(commandType));
            }

            return Add(new Entry(commandType, null, oneTime, _lifetime.DefineNested(commandType.Name)));
        }

        /// <inheritdoc />
        public Lifetime.Definition RegisterCommand(Func<object, Lifetime, ICommand> factory, bool oneTime = false)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            ThrowIfEnded("a command factory");

            return Add(new Entry(null, factory, oneTime, _lifetime.DefineNested(_messageType.Name)));
        }

        /// <inheritdoc />
        public void Remove<T>() where T : ICommand => Remove(typeof(T));

        /// <inheritdoc />
        public void Remove(Type commandType)
        {
            if (commandType == null) throw new ArgumentNullException(nameof(commandType));

            var commands = _commands;
            for (var i = 0; i < commands.Length; i++)
            {
                if (commands[i].CommandType == commandType) commands[i].Definition.Terminate();
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
        /// One or more commands threw, or failed to be built, or the clean-up registered on an execution's
        /// lifetime threw. Every command still ran; the failures are collected here.
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

            // The array is never written to once published, so this is the snapshot: no copy per Tell.
            var commands = _commands;
            List<Exception> failures = null;

            for (var i = 0; i < commands.Length; i++)
            {
                var entry = commands[i];

                // Unregistered while this dispatch was in flight: do not run it, exactly as Signal
                // 2.0.0 does not invoke a handler unsubscribed mid-dispatch.
                if (entry.Definition.IsTerminated) continue;

                var execution = entry.Definition.Lifetime.DefineNested(entry.Name);
                try
                {
                    Build(entry, message, execution.Lifetime).Execute();
                }
                catch (Exception exception)
                {
                    (failures ?? (failures = new List<Exception>())).Add(exception);
                }

                try
                {
                    execution.Terminate();
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

        private ICommand Build(Entry entry, object message, Lifetime execution)
        {
            if (entry.Factory == null)
            {
                return (ICommand)_context.Instantiate(entry.CommandType,
                    new object[] { message, entry.Definition, execution });
            }

            return entry.Factory(message, execution) ?? throw new InvalidOperationException(
                "The command factory registered against '" + _messageType + "' returned null. It must return the " +
                "command to run, or throw.");
        }

        private void ThrowIfEnded(string what)
        {
            if (!_lifetime.IsTerminated) return;

            throw new InvalidOperationException(
                "The scope of the command map for '" + _messageType + "' has already terminated, so registering " +
                what + " would silently never run. Register before the scope ends.");
        }

        private Lifetime.Definition Add(Entry entry)
        {
            _commands = Append(_commands, entry);
            entry.Definition.Lifetime.AddAction(() => _commands = Without(_commands, entry));
            return entry.Definition;
        }

        private static Entry[] Append(Entry[] entries, Entry entry)
        {
            var next = new Entry[entries.Length + 1];
            Array.Copy(entries, next, entries.Length);
            next[entries.Length] = entry;
            return next;
        }

        private static Entry[] Without(Entry[] entries, Entry entry)
        {
            var index = Array.IndexOf(entries, entry);
            if (index < 0) return entries;

            var next = new Entry[entries.Length - 1];
            Array.Copy(entries, 0, next, 0, index);
            Array.Copy(entries, index + 1, next, index, entries.Length - index - 1);
            return next;
        }

        private sealed class Entry
        {
            // Annotated like the parameter it comes from and the Context.Instantiate parameter it goes to, so
            // the linker follows the command type through the field instead of losing it here (IL2077). Null
            // for a factory registration.
            [DynamicallyAccessedMembers(Trimming.Constructors)]
            internal readonly Type CommandType;

            internal readonly Func<object, Lifetime, ICommand> Factory;
            internal readonly Lifetime.Definition Definition;
            internal readonly bool OneTime;
            internal readonly string Name;

            internal Entry([DynamicallyAccessedMembers(Trimming.Constructors)] Type commandType,
                Func<object, Lifetime, ICommand> factory, bool oneTime, Lifetime.Definition definition)
            {
                CommandType = commandType;
                Factory = factory;
                OneTime = oneTime;
                Definition = definition;
                Name = definition.Name;
            }
        }
    }
}
