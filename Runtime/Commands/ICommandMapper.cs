using System;

namespace OpenUGD.Commands
{
    /// <summary>
    /// Registers the commands that run when a single message type is told.
    /// </summary>
    /// <remarks>
    /// One method, because a command is now described by its <see cref="Type"/> rather than by a factory
    /// delegate: the mapper builds it with <see cref="Context.Instantiate(Type, object[])"/>, offering the
    /// message, the command's <see cref="Lifetime"/> and its <see cref="Lifetime.Definition"/> as constructor
    /// arguments and resolving everything else from the context. Nothing is written into the container to
    /// make that happen, so a command that throws cannot leave a registration behind.
    /// </remarks>
    public interface ICommandMapper
    {
        /// <summary>
        /// Registers <paramref name="commandType"/> to run whenever this mapper's message is told.
        /// </summary>
        /// <param name="commandType">
        /// A concrete class implementing <see cref="ICommand"/>. Instantiated once per message.
        /// </param>
        /// <param name="oneTime">
        /// When <see langword="true"/> the registration is terminated after the command has run once, whether
        /// or not <see cref="ICommand.Execute"/> threw.
        /// </param>
        /// <returns>
        /// The registration's scope. Terminating it — through the owning <see cref="Lifetime.Definition"/>,
        /// or via <see cref="ICommandMapperRemove"/> — unregisters the command.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="commandType"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="commandType"/> is not a concrete class implementing <see cref="ICommand"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// This mapper's scope has already terminated, so the registration could never run.
        /// </exception>
        Lifetime RegisterCommand(Type commandType, bool oneTime = false);
    }
}
