using System;

namespace OpenUGD.Commands
{
    /// <summary>
    /// Removes command registrations from an <see cref="ICommandMapper"/> by command type.
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="CommandMapper"/>. Until 2.0.0 this interface was declared but implemented by
    /// nothing, because registrations were keyed by an opaque factory delegate and there was no type to
    /// remove by.
    /// </remarks>
    public interface ICommandMapperRemove
    {
        /// <summary>Removes every registration of <typeparamref name="T"/>. Removing something that was never
        /// registered is not an error.</summary>
        /// <typeparam name="T">The command type to remove.</typeparam>
        void Remove<T>() where T : ICommand;

        /// <summary>Removes every registration of <paramref name="commandType"/>. Removing something that was
        /// never registered is not an error.</summary>
        /// <param name="commandType">The command type to remove.</param>
        /// <exception cref="ArgumentNullException"><paramref name="commandType"/> is null.</exception>
        void Remove(Type commandType);
    }
}
