namespace OpenUGD.Commands
{
    /// <summary>
    /// Raises a message. The write-only half of the command layer: holding this lets you send, and gives
    /// you no way to see or change what is mapped to what.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IMapCommand"/> is the other half, the one that installs behaviour. <see cref="CommandMap"/>
    /// implements both, and <c>ServiceCollection.AddCommandMap</c> registers it under both contracts.
    /// </para>
    /// <para>
    /// It is also the shape a <i>listener</i> implements: <see cref="CommandMap.Subscribe"/> takes an
    /// <see cref="ITellMessage"/> and forwards every message to it, whatever its type.
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
        /// <see cref="CommandMapExtensions.Tell"/>, which constrains the argument to
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
        /// <b>One failing handler does not stop the others.</b> Every command and every subscriber runs; then a
        /// single failure is rethrown as itself and two or more are thrown together.
        /// </para>
        /// </remarks>
        /// <param name="message">The message to raise. Never <c>null</c>.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is <c>null</c>; both
        /// implementations in this package check.</exception>
        /// <exception cref="System.ArgumentException">
        /// <see cref="CommandMapper"/> only: <paramref name="message"/> is not an instance of the message
        /// type that mapper dispatches.
        /// </exception>
        /// <exception cref="System.Exception">Exactly one handler threw: that exception, rethrown with its original
        /// stack trace.</exception>
        /// <exception cref="System.AggregateException">Two or more handlers threw. All of them still ran, and the
        /// inner exceptions are the failures themselves.</exception>
        void Tell(object message);
    }
}
