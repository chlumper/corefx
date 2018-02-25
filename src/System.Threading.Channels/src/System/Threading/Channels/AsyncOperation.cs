// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace System.Threading.Channels
{
    internal static class ResettableValueTaskObjectState
    {
        internal static readonly Action Sentinel = () => throw new Exception(nameof(ResettableValueTaskObjectState) + "." + nameof(Sentinel));
    }

    internal abstract class ResettableValueTaskObject<T> : IValueTaskObject<T>, IValueTaskObject
    {
        private enum CompletionStates
        {
            Initialized = 0,
            CompletionReserved = 1,
            Completed = 2,
            Canceled = 3,
            Faulted = 4
        }

        private volatile int _completionState; // CompletionStates
        private volatile bool _getResultCalled;
        private T _result;
        private ExceptionDispatchInfo _error;
        private Action _continuation;
        private object _schedulingContext;
        private ExecutionContext _executionContext;

        public bool RunContinutationsAsynchronously { get; set; }
        public bool IsCompleted => _completionState >= (int)CompletionStates.Completed;
        public bool IsCompletedSuccessfully => _completionState == (int)CompletionStates.Completed;
        public bool GetResultCalled => _getResultCalled;

        private static void ThrowIncompleteOperationException() =>
            throw new InvalidOperationException(SR.InvalidOperation_IncompleteAsyncOperation);

        public T GetResult()
        {
            if (!IsCompleted)
            {
                ThrowIncompleteOperationException();
            }

            ExceptionDispatchInfo error = _error;
            T result = _result;

            _getResultCalled = true; // after fetching all needed data

            error?.Throw();
            return result;
        }

        void IValueTaskObject.GetResult()
        {
            if (!IsCompleted)
            {
                ThrowIncompleteOperationException();
            }

            ExceptionDispatchInfo error = _error;

            _getResultCalled = true; // after fetching all needed data

            error?.Throw();
        }

        public void Reset()
        {
            _completionState = (int)CompletionStates.Initialized;
            _continuation = null;
            _result = default;
            _error = null;
            _getResultCalled = false;
        }

        public void OnCompleted(Action continuation, ValueTaskObjectOnCompletedFlags flags)
        {
            if ((flags & ValueTaskObjectOnCompletedFlags.FlowExecutionContext) != 0)
            {
                _executionContext = ExecutionContext.Capture();
            }

            SynchronizationContext sc = null;
            TaskScheduler ts = null;
            if ((flags & ValueTaskObjectOnCompletedFlags.UseSchedulingContext) != 0)
            {
                sc = SynchronizationContext.Current;
                if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    _schedulingContext = sc;
                }
                else
                {
                    ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        _schedulingContext = ts;
                    }
                }
            }

            if (Interlocked.CompareExchange(ref _continuation, continuation, null) != null)
            {
                Debug.Assert(ReferenceEquals(_continuation, ResettableValueTaskObjectState.Sentinel), $"Expected continuation to be sentinel; it wasn't, and continuation==_continuation is {ReferenceEquals(_continuation, continuation)}");
                Debug.Assert(IsCompleted, $"Expected IsCompleted, got {(CompletionStates)_completionState}");
                if (sc != null)
                {
                    _schedulingContext = null;
                    sc.Post(s => ((Action)s)(), continuation);
                }
                else if (ts != null)
                {
                    _schedulingContext = null;
                    Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                }
                else
                {
                    Debug.Assert(_schedulingContext == null);
                    if (_executionContext != null)
                    {
                        _executionContext = null;
                        ThreadPool.QueueUserWorkItem(s => ((Action)s)(), continuation);
                    }
                    else
                    {
                        ThreadPool.UnsafeQueueUserWorkItem(s => ((Action)s)(), continuation);
                    }
                }
            }
        }

        public bool TrySetResult(T result)
        {
            if (Interlocked.CompareExchange(ref _completionState, (int)CompletionStates.CompletionReserved, (int)CompletionStates.Initialized) == (int)CompletionStates.Initialized)
            {
                _result = result;
                SignalCompletion(CompletionStates.Completed);
                return true;
            }

            return false;
        }

        public bool TrySetException(Exception error)
        {
            if (Interlocked.CompareExchange(ref _completionState, (int)CompletionStates.CompletionReserved, (int)CompletionStates.Initialized) == (int)CompletionStates.Initialized)
            {
                _error = ExceptionDispatchInfo.Capture(error);
                SignalCompletion(CompletionStates.Faulted);
                return true;
            }

            return false;
        }

        public bool TrySetCanceled(CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _completionState, (int)CompletionStates.CompletionReserved, (int)CompletionStates.Initialized) == (int)CompletionStates.Initialized)
            {
                _error = ExceptionDispatchInfo.Capture(new OperationCanceledException(cancellationToken));
                SignalCompletion(CompletionStates.Canceled);
                return true;
            }

            return false;
        }

        private void SignalCompletion(CompletionStates state)
        {
            _completionState = (int)state;
            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, ResettableValueTaskObjectState.Sentinel, null) != null)
            {
                if (_executionContext != null)
                {
                    ExecutionContext.Run(_executionContext, s => ((ResettableValueTaskObject<T>)s).InvokeContinuation(), this);
                }
                else
                {
                    InvokeContinuation();
                }
            }
        }

        private void InvokeContinuation()
        {
            object schedulingContext = _schedulingContext;
            Action continuation = _continuation;

            if (schedulingContext == null)
            {
                if (RunContinutationsAsynchronously)
                {
                    ThreadPool.QueueUserWorkItem(s => ((Action)s)(), continuation);
                    return;
                }
            }
            else if (schedulingContext is SynchronizationContext sc)
            {
                if (RunContinutationsAsynchronously || sc != SynchronizationContext.Current)
                {
                    sc.Post(s => ((Action)s)(), continuation);
                    return;
                }
            }
            else
            {
                TaskScheduler ts = (TaskScheduler)schedulingContext;
                if (RunContinutationsAsynchronously || ts != TaskScheduler.Current)
                {
                    Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                    return;
                }
            }

            continuation();
        }
    }

    /// <summary>The representation of an asynchronous operation that has a result value.</summary>
    /// <typeparam name="TResult">Specifies the type of the result.  May be <see cref="VoidResult"/>.</typeparam>
    internal class AsyncOperation<TResult> : ResettableValueTaskObject<TResult>
    {
        /// <summary>Registration in <see cref="CancellationToken"/> that should be disposed of when the operation has completed.</summary>
        private CancellationTokenRegistration _registration;

        /// <summary>Initializes the interactor.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the operation.</param>
        public AsyncOperation(bool runContinuationsAsynchronously, CancellationToken cancellationToken = default)
        {
            RunContinutationsAsynchronously = runContinuationsAsynchronously;
            CancellationToken = cancellationToken;
            _registration = cancellationToken.Register(s =>
            {
                var thisRef = (AsyncOperation<TResult>)s;
                thisRef.TrySetCanceled(thisRef.CancellationToken);
            }, this);
        }

        /// <summary>Next operation in the linked list of operations.</summary>
        public AsyncOperation<TResult> Next { get; set; }
        public CancellationToken CancellationToken { get; }

        /// <summary>Completes the interactor with a success state and the specified result.</summary>
        /// <param name="item">The result value.</param>
        /// <returns>true if the interactor could be successfully transitioned to a completed state; false if it was already completed.</returns>
        public bool Success(TResult item)
        {
            UnregisterCancellation();
            return TrySetResult(item);
        }

        /// <summary>Completes the interactor with a failed state and the specified error.</summary>
        /// <param name="exception">The error.</param>
        /// <returns>true if the interactor could be successfully transitioned to a completed state; false if it was already completed.</returns>
        public bool Fail(Exception exception)
        {
            UnregisterCancellation();
            return TrySetException(exception);
        }

        public void UnregisterCancellation() => _registration.Dispose();
    }

    /// <summary>The representation of an asynchronous operation that has a result value and carries additional data with it.</summary>
    /// <typeparam name="TData">Specifies the type of data being written.</typeparam>
    internal sealed class VoidAsyncOperationWithData<TData> : AsyncOperation<VoidResult>
    {
        /// <summary>Initializes the interactor.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the operation.</param>
        public VoidAsyncOperationWithData(bool runContinuationsAsynchronously, CancellationToken cancellationToken = default) : base(runContinuationsAsynchronously, cancellationToken)
        {
        }

        /// <summary>The item being written.</summary>
        public TData Item { get; set; }
    }
}
