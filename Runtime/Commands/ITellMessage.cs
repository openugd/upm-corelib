namespace OpenUGD.Commands
{
    /// <summary>
    /// Raises a message. The write-only half of the command layer: holding this lets you send, and gives
    /// you no way to see or change what is mapped to what.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately split from <see cref="IMapCommand"/>, the half that installs behaviour. Hand a caller
    /// that only needs to announce something this interface and it cannot rewire the map behind your back.
    /// <see cref="CommandMap"/> implements both and <c>ServiceCollection.AddCommandMap</c> registers it
    /// under both contracts, so the split costs a consumer nothing.
    /// </para>
    /// <para>
    /// It is also the shape a <i>listener</i> implements: <see cref="CommandMap.Subscribe"/> takes an
    /// <see cref="ITellMessage"/> and forwards every message to it, whatever its type. That second role is
    /// why the interface is this small and why <see cref="Tell"/> takes <c>object</c>.
    /// </para>
    /// </remarks>
    public interface ITellMessage
    {
        /// <summary>
        /// Delivers <paramref name="message"/> to whatever this implementation routes to, synchronously and
        /// before returning. There is no queue and no frame delay.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The parameter is <c>object</c>, not <see cref="IMessage"/>,</b> because an implementation may
        /// be a listener that forwards anything it is given. The typed door is
        /// <c>OpenUGD.Services.Commands.CommandMapExtensions.Tell</c>, which constrains the argument to
        /// <see cref="IMessage"/>; use that in game code and keep this for plumbing.
        /// </para>
        /// <para>
        /// <b>What the implementations here do.</b> <see cref="CommandMap"/> looks the message's exact
        /// runtime type up in its map and runs those commands, then forwards to every subscriber; a message
        /// nothing is mapped to and nobody subscribed to is silently dropped, which is not an error.
        /// <see cref="CommandMapper"/> instead rejects anything that is not an instance of its one message
        /// type.
        /// </para>
        /// <para>
        /// <b>One failing handler does not stop the others.</b> Every command and every subscriber runs; the
        /// failures are collected and rethrown together at the end.
        /// </para>
        /// </remarks>
        /// <param name="message">The message to raise. Never <c>null</c>.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is <c>null</c>. Both
        /// implementations in this package check, because a null here would otherwise surface as a
        /// <see cref="System.NullReferenceException"/> from a <c>GetType</c> call with no clue who sent
        /// it.</exception>
        /// <exception cref="System.ArgumentException">
        /// <see cref="CommandMapper"/> only: <paramref name="message"/> is not an instance of the message
        /// type that mapper dispatches.
        /// </exception>
        /// <exception cref="System.AggregateException">
        /// One or more handlers threw. All of them still ran; call
        /// <see cref="System.AggregateException.Flatten"/> for the leaves.
        /// </exception>
        void Tell(object message);
    }
}
