// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace System.Threading.Channels
{
    /// <summary>
    /// Provides a buffered channel of unbounded capacity for use by any number
    /// of writers but at most a single reader at a time.
    /// </summary>
    [DebuggerDisplay("Items={ItemsCountForDebugger}, Closed={ChannelIsClosedForDebugger}")]
    [DebuggerTypeProxy(typeof(DebugEnumeratorDebugView<>))]
    internal sealed class SingleConsumerUnboundedChannel<T> : Channel<T>, IDebugEnumerable<T>
    {
        /// <summary>Task that indicates the channel has completed.</summary>
        private readonly TaskCompletionSource<VoidResult> _completion;
        /// <summary>
        /// A concurrent queue to hold the items for this channel.  The queue itself supports at most
        /// one writer and one reader at a time; as a result, since this channel supports multiple writers,
        /// all write access to the queue must be synchronized by the channel.
        /// </summary>
        private readonly SingleProducerSingleConsumerQueue<T> _items = new SingleProducerSingleConsumerQueue<T>();
        /// <summary>Whether to force continuations to be executed asynchronously from producer writes.</summary>
        private readonly bool _runContinuationsAsynchronously;

        /// <summary>non-null if the channel has been marked as complete for writing.</summary>
        private volatile Exception _doneWriting;

        /// <summary>A waiting reader (e.g. WaitForReadAsync) if there is one.</summary>
        private ReaderInteractor<bool> _waitingReader;
        private ReaderInteractor<bool> _readerSingleton;

        /// <summary>Initialize the channel.</summary>
        /// <param name="runContinuationsAsynchronously">Whether to force continuations to be executed asynchronously.</param>
        internal SingleConsumerUnboundedChannel(bool runContinuationsAsynchronously)
        {
            _runContinuationsAsynchronously = runContinuationsAsynchronously;
            _completion = new TaskCompletionSource<VoidResult>(runContinuationsAsynchronously ? TaskCreationOptions.RunContinuationsAsynchronously : TaskCreationOptions.None);
            
            Reader = new UnboundedChannelReader(this);
            Writer = new UnboundedChannelWriter(this);
        }

        [DebuggerDisplay("Items={ItemsCountForDebugger}")]
        [DebuggerTypeProxy(typeof(DebugEnumeratorDebugView<>))]
        private sealed class UnboundedChannelReader : ChannelReader<T>, IDebugEnumerable<T>
        {
            internal readonly SingleConsumerUnboundedChannel<T> _parent;
            internal UnboundedChannelReader(SingleConsumerUnboundedChannel<T> parent) => _parent = parent;

            public override Task Completion => _parent._completion.Task;

            public override bool TryRead(out T item)
            {
                SingleConsumerUnboundedChannel<T> parent = _parent;
                if (parent._items.TryDequeue(out item))
                {
                    if (parent._doneWriting != null && parent._items.IsEmpty)
                    {
                        ChannelUtilities.Complete(parent._completion, parent._doneWriting);
                    }
                    return true;
                }
                return false;
            }

            public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
            {
                // Outside of the lock, check if there are any items waiting to be read.  If there are, we're done.
                return
                    cancellationToken.IsCancellationRequested ? new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken)) :
                    !_parent._items.IsEmpty ? new ValueTask<bool>(true) :
                    WaitToReadAsyncCore(cancellationToken);

                ValueTask<bool> WaitToReadAsyncCore(CancellationToken ct)
                {
                    SingleConsumerUnboundedChannel<T> parent = _parent;
                    ReaderInteractor<bool> newWaiter, oldWaiter = null;
                    lock (parent.SyncObj)
                    {
                        // Again while holding the lock, check to see if there are any items available.
                        if (!parent._items.IsEmpty)
                        {
                            return new ValueTask<bool>(true);
                        }

                        // There aren't any items; if we're done writing, there never will be more items.
                        if (parent._doneWriting != null)
                        {
                            return parent._doneWriting != ChannelUtilities.s_doneWritingSentinel ?
                                new ValueTask<bool>(Task.FromException<bool>(parent._doneWriting)) :
                                new ValueTask<bool>(false);
                        }

                        oldWaiter = parent._waitingReader;
                        if (ct.CanBeCanceled)
                        {
                            parent._waitingReader = newWaiter = ReaderInteractor<bool>.Create(_parent._runContinuationsAsynchronously, ct);
                        }
                        else
                        {
                            newWaiter = _parent._readerSingleton;
                            if (newWaiter == null || !newWaiter.GetResultCalled)
                            {
                                _parent._readerSingleton = newWaiter = ReaderInteractor<bool>.Create(_parent._runContinuationsAsynchronously);
                            }
                            else
                            {
                                if (newWaiter == oldWaiter)
                                {
                                    oldWaiter = null;
                                }
                                newWaiter.Reset();
                            }

                            _parent._waitingReader = newWaiter;
                        }
                    }

                    oldWaiter?.TrySetCanceled();
                    return new ValueTask<bool>(newWaiter);
                }
            }

            /// <summary>Gets the number of items in the channel.  This should only be used by the debugger.</summary>
            private int ItemsCountForDebugger => _parent._items.Count;

            /// <summary>Gets an enumerator the debugger can use to show the contents of the channel.</summary>
            IEnumerator<T> IDebugEnumerable<T>.GetEnumerator() => _parent._items.GetEnumerator();
        }

        [DebuggerDisplay("Items={ItemsCountForDebugger}")]
        [DebuggerTypeProxy(typeof(DebugEnumeratorDebugView<>))]
        private sealed class UnboundedChannelWriter : ChannelWriter<T>, IDebugEnumerable<T>
        {
            internal readonly SingleConsumerUnboundedChannel<T> _parent;
            internal UnboundedChannelWriter(SingleConsumerUnboundedChannel<T> parent) => _parent = parent;

            public override bool TryComplete(Exception error)
            {
                ReaderInteractor<bool> waitingReader = null;
                bool completeTask = false;

                SingleConsumerUnboundedChannel<T> parent = _parent;
                lock (parent.SyncObj)
                {
                    // If we're already marked as complete, there's nothing more to do.
                    if (parent._doneWriting != null)
                    {
                        return false;
                    }

                    // Mark as complete for writing.
                    parent._doneWriting = error ?? ChannelUtilities.s_doneWritingSentinel;

                    // If we have no more items remaining, then the channel needs to be marked as completed
                    // and readers need to be informed they'll never get another item.  All of that needs
                    // to happen outside of the lock to avoid invoking continuations under the lock.
                    if (parent._items.IsEmpty)
                    {
                        completeTask = true;

                        if (parent._waitingReader != null)
                        {
                            waitingReader = parent._waitingReader;
                            parent._waitingReader = null;
                        }
                    }
                }

                // Complete the channel task if necessary
                if (completeTask)
                {
                    ChannelUtilities.Complete(parent._completion, error);
                }

                // Complete a waiting reader if necessary.
                if (waitingReader != null)
                {
                    if (error != null)
                    {
                        waitingReader.Fail(error);
                    }
                    else
                    {
                        waitingReader.Success(item: false);
                    }
                }

                // Successfully completed the channel
                return true;
            }

            public override bool TryWrite(T item)
            {
                SingleConsumerUnboundedChannel<T> parent = _parent;
                ReaderInteractor<bool> waitingReader = null;

                lock (parent.SyncObj)
                {
                    // If writing is completed, exit out without writing.
                    if (parent._doneWriting != null)
                    {
                        return false;
                    }

                    // Queue the item being written; then if there's a waiting
                    // reader, store it for notification outside of the lock.
                    parent._items.Enqueue(item);

                    waitingReader = parent._waitingReader;
                    if (waitingReader == null)
                    {
                        return true;
                    }
                    parent._waitingReader = null;
                }

                // If we get here, we grabbed a waiting reader.
                // Notify it that an item was written and exit.
                Debug.Assert(waitingReader != null, "Expected a waiting reader");
                waitingReader.Success(item: true);
                return true;
            }

            public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken)
            {
                Exception doneWriting = _parent._doneWriting;
                return
                    cancellationToken.IsCancellationRequested ? new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken)) :
                    doneWriting == null ? new ValueTask<bool>(true) :
                    doneWriting != ChannelUtilities.s_doneWritingSentinel ? new ValueTask<bool>(Task.FromException<bool>(doneWriting)) :
                    new ValueTask<bool>(false);
            }

            public override ValueTask WriteAsync(T item, CancellationToken cancellationToken) =>
                // Writing always succeeds (unless we've already completed writing or cancellation has been requested),
                // so just TryWrite and return a completed task.
                cancellationToken.IsCancellationRequested ? new ValueTask(Task.FromCanceled(cancellationToken)) :
                TryWrite(item) ? default :
                new ValueTask(Task.FromException(ChannelUtilities.CreateInvalidCompletionException(_parent._doneWriting)));

            /// <summary>Gets the number of items in the channel. This should only be used by the debugger.</summary>
            private int ItemsCountForDebugger => _parent._items.Count;

            /// <summary>Gets an enumerator the debugger can use to show the contents of the channel.</summary>
            IEnumerator<T> IDebugEnumerable<T>.GetEnumerator() => _parent._items.GetEnumerator();
        }

        private object SyncObj => _items;

        /// <summary>Gets the number of items in the channel.  This should only be used by the debugger.</summary>
        private int ItemsCountForDebugger => _items.Count;

        /// <summary>Report if the channel is closed or not. This should only be used by the debugger.</summary>
        private bool ChannelIsClosedForDebugger => _doneWriting != null;

        /// <summary>Gets an enumerator the debugger can use to show the contents of the channel.</summary>
        IEnumerator<T> IDebugEnumerable<T>.GetEnumerator() => _items.GetEnumerator();
    }
}
