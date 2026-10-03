using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace OpenUGD.Commands
{
    // The family's failure policy, as Lifetime and Signal apply it: every handler runs; then one failure is rethrown
    // as itself, with its original stack trace, and two or more are thrown as one AggregateException in the order
    // they happened. Nothing is allocated until a second failure.
    internal static class Failures
    {
        internal static void Add(Exception exception, ref Exception first, ref List<Exception> all)
        {
            if (first == null) first = exception;
            else (all ??= new List<Exception> { first }).Add(exception);
        }

        // what follows the count in the aggregate's message, e.g. " commands failed while handling 'Buy'.".
        internal static void ThrowIfAny(Exception first, List<Exception> all, string what)
        {
            if (first == null) return;
            if (all == null) ExceptionDispatchInfo.Capture(first).Throw();

            throw new AggregateException(all.Count + what, all);
        }
    }
}
