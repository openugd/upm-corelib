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
    /// <b>Building a command.</b> A command registered by type is checked and planned at registration: the
    /// constructor is chosen, and each argument's source decided, once. Every message then builds a fresh command
    /// with that constructor, offering the message, the registration's <see cref="Lifetime.Definition"/> and the
    /// execution's <see cref="Lifetime"/>, resolving the other parameters from the context, and filling
    /// <c>[Inject]</c> members if the type has any. A command registered with a factory is built by the factory.
    /// Either way each execution gets its own <see cref="Lifetime"/>, nested in the registration's and terminated
    /// when the command returns.
    /// </para>
    /// <para>
    /// <b>Failures.</b> A command that throws, or fails to be built, does not stop the others. Once every command
    /// has run, a single failure is rethrown as itself, with its original stack trace; two or more are thrown as
    /// one <see cref="AggregateException"/>, in the order they happened.
    /// </para>
    /// <para>
    /// <b>Not thread-safe.</b> Register, remove and tell from one thread — normally Unity's main thread. A
    /// command may register, remove or tell from inside <see cref="ICommand.Execute"/>: the list of commands is
    /// replaced, never changed in place, so a dispatch in progress keeps walking the list it started with, and
    /// skips a command removed in the meantime. A one-time registration runs once even when its command tells its
    /// own message again.
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
        /// <param name="context">The context commands registered by type are built from.</param>
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

            if (!typeof(ICommand).IsAssignableFrom(commandType) || commandType.IsAbstract ||
                commandType.IsInterface || commandType.IsValueType || commandType.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    "'" + commandType + "' cannot be registered against '" + _messageType +
                    "': a command must be a concrete, closed class implementing " + typeof(ICommand) + ".",
                    nameof(commandType));
            }

            ThrowIfEnded(commandType.Name);

            string reason;
            var activator = CommandActivator.Create(_context, commandType, _messageType, out reason);
            if (activator == null)
            {
                throw new ArgumentException(
                    "'" + commandType + "' cannot be registered against '" + _messageType + "': " + reason +
                    " A command's constructor may take the message, a Lifetime.Definition (the registration), a " +
                    "Lifetime (the execution) and any service registered in the context; its [Inject] members, " +
                    "services only.",
                    nameof(commandType));
            }

            return Add(new Entry(commandType, activator, null, oneTime, _lifetime.DefineNested(commandType.Name)));
        }

        /// <inheritdoc />
        public Lifetime.Definition RegisterCommand(Func<object, Lifetime, ICommand> factory, bool oneTime = false)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            ThrowIfEnded("a command factory");

            return Add(new Entry(null, null, factory, oneTime, _lifetime.DefineNested(_messageType.Name)));
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
        /// <exception cref="Exception">
        /// Exactly one command threw, failed to be built, or had clean-up on its execution's lifetime throw: that
        /// exception, rethrown with its original stack trace. Every other command still ran.
        /// </exception>
        /// <exception cref="AggregateException">Two or more did. Every command still ran.</exception>
        public void Tell(object message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            if (!_messageType.IsInstanceOfType(message))
            {
                throw new ArgumentException(
                    "This mapper dispatches '" + _messageType + "' but was told a '" + message.GetType() + "'.",
                    nameof(message));
            }

            Exception failure = null;
            List<Exception> failures = null;
            Dispatch(message, ref failure, ref failures);

            Failures.ThrowIfAny(failure, failures,
                " commands failed while handling '" + _messageType + "'. Every registered command still ran.");
        }

        // Runs the commands and adds each failure to the caller's collection instead of throwing, so CommandMap can
        // report them together with its listeners' as one flat list.
        internal void Dispatch(object message, ref Exception failure, ref List<Exception> failures)
        {
            // The array is never written to once published, so this is the snapshot: no copy per Tell.
            var commands = _commands;

            for (var i = 0; i < commands.Length; i++)
            {
                var entry = commands[i];

                // Unregistered while this dispatch was in flight: do not run it, exactly as Signal does not invoke
                // a handler unsubscribed mid-dispatch.
                if (entry.Definition.IsTerminated) continue;

                // Claimed before it runs, so a one-time command that tells its own message from Execute - or
                // anything else that dispatches again before the registration ends below - does not run it twice.
                if (entry.OneTime)
                {
                    if (entry.Claimed) continue;
                    entry.Claimed = true;
                }

                var execution = entry.Definition.Lifetime.DefineNested(entry.Name);
                try
                {
                    Build(entry, message, execution.Lifetime).Execute();
                }
                catch (Exception exception)
                {
                    Failures.Add(exception, ref failure, ref failures);
                }

                try
                {
                    execution.Terminate();
                }
                catch (Exception exception)
                {
                    Failures.Add(exception, ref failure, ref failures);
                }

                if (!entry.OneTime) continue;

                try
                {
                    entry.Definition.Terminate();
                }
                catch (Exception exception)
                {
                    Failures.Add(exception, ref failure, ref failures);
                }
            }
        }

        private ICommand Build(Entry entry, object message, Lifetime execution)
        {
            if (entry.Activator != null) return entry.Activator.Build(message, entry.Definition, execution);

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
            // Null for a factory registration; used only to find registrations to remove.
            internal readonly Type CommandType;

            // Exactly one of the two is set.
            internal readonly CommandActivator Activator;
            internal readonly Func<object, Lifetime, ICommand> Factory;

            internal readonly Lifetime.Definition Definition;
            internal readonly bool OneTime;
            internal readonly string Name;

            // A one-time registration that has started its one run.
            internal bool Claimed;

            internal Entry(Type commandType, CommandActivator activator, Func<object, Lifetime, ICommand> factory,
                bool oneTime, Lifetime.Definition definition)
            {
                CommandType = commandType;
                Activator = activator;
                Factory = factory;
                OneTime = oneTime;
                Definition = definition;
                Name = definition.Name;
            }
        }
    }
}
