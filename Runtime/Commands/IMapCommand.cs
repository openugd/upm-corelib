namespace OpenUGD.Commands
{
    /// <summary>
    /// Hands out the <see cref="ICommandMapper"/> for a message type. The half of the command layer that
    /// installs behaviour, as opposed to <see cref="ITellMessage"/>, which only raises it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CommandMap"/> implements both, and <c>ServiceCollection.AddCommandMap</c> registers one
    /// instance under both contracts — unless an <see cref="IMapCommand"/> is already registered, which is how a
    /// router of your own replaces it.
    /// </para>
    /// <para>
    /// The typed spellings are extension methods; see <see cref="CommandMapperExtensions"/>.
    /// </para>
    /// </remarks>
    public interface IMapCommand
    {
        /// <summary>
        /// Returns the mapper that owns the command list for <typeparamref name="TMessage"/>, creating it on
        /// first use.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Repeat calls return the same mapper</b>, so registrations from different modules accumulate
        /// instead of replacing one another, and asking for a mapper you then register nothing on costs a
        /// dictionary entry and an empty mapper, nothing more.
        /// </para>
        /// <para>
        /// <b>Keyed on the exact type.</b> <c>Map&lt;Base&gt;()</c> and <c>Map&lt;Derived&gt;()</c> are two
        /// unrelated mappers, and <see cref="ITellMessage.Tell"/> reaches only the one matching the
        /// message's runtime type.
        /// </para>
        /// <para>
        /// For the common case of attaching a single command, prefer the one-line
        /// <c>CommandMapperExtensions.Map&lt;TMessage, TCommand&gt;</c>, which is this call followed by
        /// <see cref="ICommandMapper.RegisterCommand(System.Type, bool)"/>.
        /// </para>
        /// </remarks>
        /// <typeparam name="TMessage">The message type to register commands against.</typeparam>
        /// <returns>The mapper for <typeparamref name="TMessage"/>; never <c>null</c>.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// The map's scope has already terminated, so nothing registered through the returned mapper could
        /// ever run. It throws rather than handing back a mapper that would accept registrations and
        /// silently never dispatch to them.
        /// </exception>
        ICommandMapper Map<TMessage>() where TMessage : IMessage;
    }
}
