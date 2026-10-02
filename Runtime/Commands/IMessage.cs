namespace OpenUGD.Commands
{
    /// <summary>
    /// Marker for a type that may be mapped to commands and told. Declares no members.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It exists to constrain the typed entry points — <see cref="IMapCommand.Map{TMessage}"/> and the
    /// <see cref="CommandMapExtensions.Tell"/> extension — so that a type never meant to travel through the
    /// command layer is rejected by the compiler rather than routed to nothing at runtime.
    /// </para>
    /// <para>
    /// <b>The router itself is untyped.</b> <see cref="ITellMessage.Tell"/> takes <c>object</c>, and
    /// <see cref="CommandMap"/> finds its mapper by the message's exact runtime type. Implementing this
    /// interface is what makes a type reachable through the typed API, not what makes it routable.
    /// </para>
    /// <para>
    /// <b>Matching is by exact type.</b> A mapping registered for a base message never runs for a derived
    /// one, and a mapping for a derived message never runs for the base. Map each concrete message you
    /// intend to send.
    /// </para>
    /// </remarks>
    public interface IMessage
    {
    }
}
