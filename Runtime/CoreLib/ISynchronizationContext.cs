using System;
using System.Threading;

namespace OpenUGD.Utils
{
    /// <summary>
    /// The ability to marshal a call onto another thread — normally Unity's main thread.
    /// </summary>
    /// <remarks>
    /// A one-type-wide seam over <see cref="SynchronizationContext"/>, so code that must hop threads can be
    /// unit-tested with an inline fake (one that simply invokes the callback) instead of a real Unity player
    /// loop. Two methods, matching the two the platform actually distinguishes.
    /// </remarks>
    public interface ISynchronizationContext
    {
        /// <summary>
        /// Dispatches <paramref name="action"/> and <b>blocks</b> until it has run.
        /// </summary>
        /// <remarks>
        /// Deadlocks if called from the target thread itself on a context that queues rather than inlines.
        /// Prefer <see cref="Post"/> unless the result is needed before returning.
        /// </remarks>
        /// <param name="action">The callback to run.</param>
        /// <param name="state">Opaque state handed to the callback.</param>
        void Send(SendOrPostCallback action, object state);

        /// <summary>
        /// Queues <paramref name="action"/> to run on the target thread and returns immediately.
        /// </summary>
        /// <remarks>
        /// An exception thrown by the callback surfaces on the target thread, not at this call site.
        /// </remarks>
        /// <param name="action">The callback to run.</param>
        /// <param name="state">Opaque state handed to the callback.</param>
        void Post(SendOrPostCallback action, object state);
    }

    /// <summary>
    /// The default <see cref="ISynchronizationContext"/>: a thin adapter over a real
    /// <see cref="SynchronizationContext"/>.
    /// </summary>
    /// <remarks>
    /// <b>Changed in 2.0.0.</b> The constructor now rejects a <c>null</c> context. Capturing
    /// <see cref="SynchronizationContext.Current"/> is the usual way to build one of these, and
    /// <c>Current</c> is <c>null</c> on any thread that has no installed context — including a plain
    /// <see cref="Thread"/> or a thread-pool thread. The null used to be stored and every later
    /// <see cref="Send"/>/<see cref="Post"/> threw a <see cref="NullReferenceException"/> with no trace of
    /// where the bad context came from. Capture it on the main thread, during boot.
    /// </remarks>
    public sealed class SynchronizationContextWrapper : ISynchronizationContext
    {
        /// <summary>
        /// Wraps <paramref name="context"/>.
        /// </summary>
        /// <param name="context">
        /// The context to marshal onto. Must not be <c>null</c> — see the type remarks.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is <c>null</c>.</exception>
        public SynchronizationContextWrapper(SynchronizationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context),
                    "SynchronizationContext cannot be null. SynchronizationContext.Current is null on any " +
                    "thread without an installed context - capture it on the main thread during boot.");
            }

            Context = context;
        }

        /// <summary>The wrapped context.</summary>
        public SynchronizationContext Context { get; }

        /// <inheritdoc/>
        public void Send(SendOrPostCallback action, object state) => Context.Send(action, state);

        /// <inheritdoc/>
        public void Post(SendOrPostCallback action, object state) => Context.Post(action, state);
    }
}
