// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace System.Threading.Channels
{
    /// <summary>Provides internal helper methods for implementing channels.</summary>
    internal static class ChannelUtilities
    {
        /// <summary>Sentinel object used to indicate being done writing.</summary>
        internal static readonly Exception s_doneWritingSentinel = new Exception(nameof(s_doneWritingSentinel));
        /// <summary>A cached task with a Boolean true result.</summary>
        internal static readonly Task<bool> s_trueTask = Task.FromResult(result: true);
        /// <summary>A cached task with a Boolean false result.</summary>
        internal static readonly Task<bool> s_falseTask = Task.FromResult(result: false);
        /// <summary>A cached task that never completes.</summary>
        internal static readonly Task s_neverCompletingTask = new TaskCompletionSource<bool>().Task;

        /// <summary>Completes the specified TaskCompletionSource.</summary>
        /// <param name="tcs">The source to complete.</param>
        /// <param name="error">
        /// The optional exception with which to complete.
        /// If this is null or the DoneWritingSentinel, the source will be completed successfully.
        /// If this is an OperationCanceledException, it'll be completed with the exception's token.
        /// Otherwise, it'll be completed as faulted with the exception.
        /// </param>
        internal static void Complete(TaskCompletionSource<VoidResult> tcs, Exception error = null)
        {
            if (error is OperationCanceledException oce)
            {
                tcs.TrySetCanceled(oce.CancellationToken);
            }
            else if (error != null && error != s_doneWritingSentinel)
            {
                tcs.TrySetException(error);
            }
            else
            {
                tcs.TrySetResult(default);
            }
        }

        /// <summary>Gets a value task representing an error.</summary>
        /// <typeparam name="T">Specifies the type of the value that would have been returned.</typeparam>
        /// <param name="error">The error.  This may be <see cref="s_doneWritingSentinel"/>.</param>
        /// <returns>The failed task.</returns>
        internal static ValueTask<T> GetInvalidCompletionValueTask<T>(Exception error)
        {
            Debug.Assert(error != null);

            Task<T> t =
                error == s_doneWritingSentinel ? Task.FromException<T>(CreateInvalidCompletionException()) :
                error is OperationCanceledException oce ? Task.FromCanceled<T>(oce.CancellationToken.IsCancellationRequested ? oce.CancellationToken : new CancellationToken(true)) :
                Task.FromException<T>(CreateInvalidCompletionException(error));

            return new ValueTask<T>(t);
        }

        /// <summary>Wake up all of the waiters and null out the field.</summary>
        /// <param name="waiters">The waiters.</param>
        /// <param name="result">The value with which to complete each waiter.</param>
        internal static void WakeUpWaiters(ref ReaderInteractor<bool> waiters, bool result)
        {
            Debug.Assert(error != null);
            while (!interactors.IsEmpty)
            {
                interactors.DequeueHead().Fail(error);
            }
        }

        /// <summary>Wake up all of the waiters and null out the field.</summary>
        /// <param name="listTail">The tail of the waiters list.</param>
        /// <param name="result">The success value with which to complete each waiter if <paramref name="error">error</paramref> is null.</param>
        /// <param name="error">The failure with which to complete each waiter, if non-null.</param>
        /// <param name="returnCache">If non-null, the cache to which to return waiters.</param>
        internal static void WakeUpWaiters(ref ReaderInteractor<bool> listTail, bool result, Exception error = null, ConcurrentQueue<ReaderInteractor<bool>> returnCache = null)
        {
            ReaderInteractor<bool> tail = listTail;
            if (tail != null)
            {
                listTail = null;

                ReaderInteractor<bool> head = tail.Next;
                ReaderInteractor<bool> c = head;
                do
                {
                    ReaderInteractor<bool> next = c.Next;
                    c.Next = null;

                    bool completed = error != null ? c.Fail(error) : c.Success(result);
                    Debug.Assert(completed || c.CanBeCanceled);
                    if (completed)
                    {
                        returnCache?.Enqueue(tail);
                    }

                    c = next;
                }
                while (c != head);
            }
        }

        /// <summary>Counts the number of waiters in a waiter chain that are still incomplete.</summary>
        /// <param name="tail">The waiters to count.</param>
        /// <returns>The number of waiters in the waiter chain that are still incomplete.</returns>
        internal static int CountWaiters(ReaderInteractor<bool> tail)
        {
            int count = 0;
            if (tail != null)
            {
                ReaderInteractor<bool> head = tail.Next;
                ReaderInteractor<bool> c = head;
                do
                {
                    if (!c.IsCompleted)
                    {
                        count++;
                    }
                    c = c.Next;
                }
                while (c != head);
            }
            return count;
        }

        /// <summary>Gets or creates a "waiter" (e.g. WaitForRead/WriteAsync) interactor.</summary>
        /// <param name="listTail">The field storing the tail of the waiter list.</param>
        /// <param name="runContinuationsAsynchronously">true to force continuations to run asynchronously; otherwise, false.</param>
        /// <param name="cancellationToken">The token to use to cancel the wait.</param>
        internal static ValueTask<bool> CreateAndQueueWaiter(ref ReaderInteractor<bool> listTail, bool runContinuationsAsynchronously, CancellationToken cancellationToken) =>
            QueueWaiter(ref listTail, ReaderInteractor<bool>.Create(runContinuationsAsynchronously, cancellationToken));

        internal static ValueTask<bool> QueueWaiter(ref ReaderInteractor<bool> tail, ReaderInteractor<bool> waiter)
        {
            ReaderInteractor<bool> c = tail;
            if (c == null)
            {
                waiter.Next = waiter;
            }
            else
            {
                waiter.Next = c.Next;
                c.Next = waiter;
            }
            tail = waiter;
            return new ValueTask<bool>(waiter);
        }

        internal static ReaderInteractor<bool> PopCachedInteractor(ConcurrentQueue<ReaderInteractor<bool>> waiters)
        {
            while (waiters.TryDequeue(out ReaderInteractor<bool> waiter))
            {
                if (waiter.GetResultCalled)
                {
                    return waiter;
                }
            }

            return null;
        }

        /// <summary>Creates and returns an exception object to indicate that a channel has been closed.</summary>
        internal static Exception CreateInvalidCompletionException(Exception inner = null) =>
            inner is OperationCanceledException ? inner :
            inner != null && inner != s_doneWritingSentinel ? new ChannelClosedException(inner) :
            new ChannelClosedException();
    }
}
