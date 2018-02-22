// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace System.Threading.Channels
{
    internal sealed class ManualResetValueTaskObject
    {
        internal static readonly Action s_sentinel = new Action(() => { });
    }

    internal class ResettableValueTaskObject<T> : IValueTaskObject<T>, IValueTaskObject
    {
        private enum CompletionStates
        {
            Initialized = 0,
            CompletionReserved = 1,
            Completed = 2,
            Canceled = 3,
            Faulted = 4
        }

        private readonly ManualResetEventSlim _completeEvent = new ManualResetEventSlim();
        private volatile int _completionState; // CompletionStates
        private bool _continueOnCapturedContext = true;
        private volatile bool _getResultCalled;
        private T _result;
        private ExceptionDispatchInfo _error;
        private Action _continuation;
        private SynchronizationContext _capturedContext;
        private ExecutionContext _executionContext;
        private Task<T> _cachedTask;

        public bool RunContinutationsAsynchronously { get; set; }
        public bool IsCompleted => _completionState >= (int)CompletionStates.Completed;
        public bool IsCompletedSuccessfully => _completionState == (int)CompletionStates.Completed;
        public bool IsFaulted => _completionState == (int)CompletionStates.Faulted;
        public bool IsCanceled => _completionState == (int)CompletionStates.Canceled;
        public bool GetResultCalled => _getResultCalled;

        public ResettableValueTaskObject<T> ConfigureAwait(bool continueOnCapturedContext)
        {
            _continueOnCapturedContext = continueOnCapturedContext;
            return this;
        }

        IValueTaskObject<T> IValueTaskObject<T>.ConfigureAwait(bool continueOnCapturedContext) =>
            ConfigureAwait(continueOnCapturedContext);

        IValueTaskObject IValueTaskObject.ConfigureAwait(bool continueOnCapturedContext)
        {
            _continueOnCapturedContext = continueOnCapturedContext;
            return this;
        }

        public T GetResult()
        {
            if (!IsCompleted)
            {
                _completeEvent.Wait();
            }

            ExecutionContext ctx = _executionContext;
            if (ctx != null)
            {
                ctx.Dispose();
                _executionContext = null;
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
                _completeEvent.Wait();
            }

            ExecutionContext ctx = _executionContext;
            if (ctx != null)
            {
                ctx.Dispose();
                _executionContext = null;
            }

            ExceptionDispatchInfo error = _error;
            _getResultCalled = true; // after fetching all needed data

            error?.Throw();
        }

        public void Reset()
        {
            _continueOnCapturedContext = true;
            _completionState = (int)CompletionStates.Initialized;
            _continuation = null;
            _result = default;
            _error = null;
            _completeEvent.Reset();
            _cachedTask = null;
            _getResultCalled = false;
        }

        public void OnCompleted(Action continuation)
        {
            _executionContext = ExecutionContext.Capture();

            SynchronizationContext sc = null;
            if (_continueOnCapturedContext)
            {
                _capturedContext = sc = SynchronizationContext.Current;
            }

            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, continuation, null) != null)
            {
                Debug.Assert(ReferenceEquals(_continuation, ManualResetValueTaskObject.s_sentinel), $"Expected continuation to be sentinel; it wasn't, and continuation==_continuation is {ReferenceEquals(_continuation, continuation)}");
                Debug.Assert(IsCompleted, $"Expected IsCompleted, got {(CompletionStates)_completionState}");
                if (sc != null)
                {
                    _capturedContext = null;
                    sc.Post(s => ((Action)s)(), continuation);
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(s => ((Action)s)(), continuation);
                }
            }
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            SynchronizationContext sc = null;
            if (_continueOnCapturedContext)
            {
                _capturedContext = sc = SynchronizationContext.Current;
            }

            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, continuation, null) != null)
            {
                Debug.Assert(ReferenceEquals(_continuation, ManualResetValueTaskObject.s_sentinel), $"Expected continuation to be sentinel; it wasn't, and continuation==_continuation is {ReferenceEquals(_continuation, continuation)}");
                Debug.Assert(IsCompleted, $"Expected IsCompleted, got {(CompletionStates)_completionState}");
                if (sc != null)
                {
                    _capturedContext = null;
                    sc.Post(s => ((Action)s)(), continuation);
                }
                else
                {
                    ThreadPool.UnsafeQueueUserWorkItem(s => ((Action)s)(), continuation);
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
            _completeEvent.Set();
            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, ManualResetValueTaskObject.s_sentinel, null) != null)
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
            SynchronizationContext sc = _capturedContext;
            if (sc != null)
            {
                if (RunContinutationsAsynchronously || sc != SynchronizationContext.Current)
                {
                    sc.Post(s => ((Action)s)(), _continuation);
                }
                else
                {
                    _continuation();
                }
            }
            else if (RunContinutationsAsynchronously)
            {
                ThreadPool.QueueUserWorkItem(s => ((Action)s)(), _continuation);
            }
            else
            {
                _continuation();
            }
        }

        public Task<T> AsTask() => _cachedTask ?? (_cachedTask = AsTaskCore());

        private Task<T> AsTaskCore()
        {
            switch ((CompletionStates)_completionState)
            {
                case CompletionStates.Completed:
                    return Task.FromResult(GetResult());
                case CompletionStates.Faulted:
                    return Task.FromException<T>(_error.SourceException);
                case CompletionStates.Canceled:
                    return Task.FromCanceled<T>(_error.SourceException is OperationCanceledException oce ? oce.CancellationToken : new CancellationToken(true));
                default:
                    return new ValueTaskObjectCompletionSource(this).Task;
            }
        }

        private sealed class ValueTaskObjectCompletionSource : TaskCompletionSource<T>
        {
            private readonly ResettableValueTaskObject<T> _task;

            public ValueTaskObjectCompletionSource(ResettableValueTaskObject<T> task)
            {
                _task = task;
                task.UnsafeOnCompleted(new Action(Finish));
            }

            private void Finish()
            {
                switch ((CompletionStates)_task._completionState)
                {
                    case CompletionStates.Completed:
                        TrySetResult(_task._result);
                        break;
                    case CompletionStates.Faulted:
                        TrySetException(_task._error.SourceException);
                        break;
                    default:
                        Debug.Assert(_task._completionState == (int)CompletionStates.Canceled);
                        TrySetCanceled(_task._error.SourceException is OperationCanceledException oce ? oce.CancellationToken : new CancellationToken(true));
                        break;
                }
            }
        }
    }

    /// <summary>A base class for a blocked or waiting reader or writer.</summary>
    /// <typeparam name="T">Specifies the type of data passed to the reader or writer.</typeparam>
    internal abstract class Interactor<T> : ResettableValueTaskObject<T>
    {
        /// <summary>Initializes the interactor.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        protected Interactor(bool runContinuationsAsynchronously)
        {
            RunContinutationsAsynchronously = runContinuationsAsynchronously;
        }

        /// <summary>Completes the interactor with a success state and the specified result.</summary>
        /// <param name="item">The result value.</param>
        /// <returns>true if the interactor could be successfully transitioned to a completed state; false if it was already completed.</returns>
        internal bool Success(T item)
        {
            UnregisterCancellation();
            return TrySetResult(item);
        }

        /// <summary>Completes the interactor with a failed state and the specified error.</summary>
        /// <param name="exception">The error.</param>
        /// <returns>true if the interactor could be successfully transitioned to a completed state; false if it was already completed.</returns>
        internal bool Fail(Exception exception)
        {
            UnregisterCancellation();
            return TrySetException(exception);
        }

        /// <summary>Unregister cancellation in case cancellation was registered.</summary>
        internal virtual void UnregisterCancellation() { }

        /// <summary>Gets whether the interactor can be canceled.</summary>
        internal virtual bool CanBeCanceled => false;
    }

    /// <summary>A blocked or waiting reader.</summary>
    /// <typeparam name="T">Specifies the type of data being read.</typeparam>
    internal class ReaderInteractor<T> : Interactor<T>
    {
        internal ReaderInteractor<T> Next { get; set; }

        /// <summary>Initializes the reader.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        protected ReaderInteractor(bool runContinuationsAsynchronously) : base(runContinuationsAsynchronously) { }

        /// <summary>Creates a reader.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        /// <returns>The reader.</returns>
        public static ReaderInteractor<T> Create(bool runContinuationsAsynchronously) =>
            new ReaderInteractor<T>(runContinuationsAsynchronously);

        /// <summary>Creates a reader.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <returns>The reader.</returns>
        public static ReaderInteractor<T> Create(bool runContinuationsAsynchronously, CancellationToken cancellationToken) =>
            cancellationToken.CanBeCanceled ?
                new CancelableReaderInteractor<T>(runContinuationsAsynchronously, cancellationToken) :
                new ReaderInteractor<T>(runContinuationsAsynchronously);
    }

    /// <summary>A blocked or waiting writer.</summary>
    /// <typeparam name="T">Specifies the type of data being written.</typeparam>
    [DebuggerDisplay("Item={Item}")]
    internal class WriterInteractor<T> : Interactor<VoidResult>
    {
        /// <summary>Initializes the writer.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        protected WriterInteractor(bool runContinuationsAsynchronously) : base(runContinuationsAsynchronously) { }

        /// <summary>The item being written.</summary>
        internal T Item { get; private set; }

        /// <summary>Creates a writer.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        /// <param name="item">The item being written.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the read operation.</param>
        /// <returns>The reader.</returns>
        public static WriterInteractor<T> Create(bool runContinuationsAsynchronously, T item, CancellationToken cancellationToken)
        {
            WriterInteractor<T> w = cancellationToken.CanBeCanceled ?
                new CancelableWriter<T>(runContinuationsAsynchronously, cancellationToken) :
                new WriterInteractor<T>(runContinuationsAsynchronously);
            w.Item = item;
            return w;
        }
    }

    /// <summary>A blocked or waiting reader where the read can be canceled.</summary>
    /// <typeparam name="T">Specifies the type of data being read.</typeparam>
    internal sealed class CancelableReaderInteractor<T> : ReaderInteractor<T>
    {
        /// <summary>The token used for cancellation.</summary>
        private readonly CancellationToken _token;
        /// <summary>Registration in <see cref="_token"/> that should be disposed of when the operation has completed.</summary>
        private CancellationTokenRegistration _registration;

        /// <summary>Initializes the cancelable reader.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the read operation.</param>
        internal CancelableReaderInteractor(bool runContinuationsAsynchronously, CancellationToken cancellationToken) : base(runContinuationsAsynchronously)
        {
            _token = cancellationToken;
            _registration = cancellationToken.Register(s =>
            {
                var thisRef = (CancelableReaderInteractor<T>)s;
                thisRef.TrySetCanceled(thisRef._token);
            }, this);
        }

        /// <summary>Unregister cancellation in case cancellation was registered.</summary>
        internal override void UnregisterCancellation()
        {
            _registration.Dispose();
            _registration = default;
        }

        /// <summary>Gets whether the interactor can be canceled.</summary>
        internal override bool CanBeCanceled => _token.CanBeCanceled;
    }

    /// <summary>A blocked or waiting reader where the read can be canceled.</summary>
    /// <typeparam name="T">Specifies the type of data being read.</typeparam>
    internal sealed class CancelableWriter<T> : WriterInteractor<T>
    {
        /// <summary>The token used for cancellation.</summary>
        private CancellationToken _token;
        /// <summary>Registration in <see cref="_token"/> that should be disposed of when the operation has completed.</summary>
        private CancellationTokenRegistration _registration;

        /// <summary>Initializes the cancelable writer.</summary>
        /// <param name="runContinuationsAsynchronously">true if continuations should be forced to run asynchronously; otherwise, false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the read operation.</param>
        internal CancelableWriter(bool runContinuationsAsynchronously, CancellationToken cancellationToken) : base(runContinuationsAsynchronously)
        {
            _token = cancellationToken;
            _registration = cancellationToken.Register(s =>
            {
                var thisRef = (CancelableWriter<T>)s;
                thisRef.TrySetCanceled(thisRef._token);
            }, this);
        }

        /// <summary>Unregister cancellation in case cancellation was registered.</summary>
        internal override void UnregisterCancellation()
        {
            _registration.Dispose();
            _registration = default;
        }

        /// <summary>Gets whether the interactor can be canceled.</summary>
        internal override bool CanBeCanceled => _token.CanBeCanceled;
    }
}
