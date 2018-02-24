// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.ExceptionServices;

namespace System.Threading.Tasks
{
    internal sealed class ManualResetValueTaskObject
    {
        internal static readonly Action s_sentinel = new Action(() => { });
    }

    internal sealed class ManualResetValueTaskObject<T> : IValueTaskObject<T>
    {
        private readonly ManualResetEventSlim _completeEvent = new ManualResetEventSlim();
        private bool _completed;
        private T _result;
        private ExceptionDispatchInfo _error;
        private Action _continuation;
        private SynchronizationContext _capturedContext;
        private ExecutionContext _executionContext;

        public bool IsCompleted => _completed;
        public bool IsCompletedSuccessfully => _completed && _error == null;

        public T GetResult()
        {
            if (!_completed)
            {
                _completeEvent.Wait();
                _completeEvent.Reset();
            }

            ExecutionContext ctx = _executionContext;
            if (ctx != null)
            {
                ctx.Dispose();
                _executionContext = null;
            }

            _error?.Throw();
            return _result;
        }

        public void Reset()
        {
            _completed = false;
            _continuation = null;
            _result = default;
            _error = null;
        }

        public void OnCompleted(Action continuation, bool continueOnCapturedContext)
        {
            _executionContext = ExecutionContext.Capture();

            if (continueOnCapturedContext)
            {
                _capturedContext = SynchronizationContext.Current;
            }

            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, continuation, null) != null)
            {
                SynchronizationContext sc = _capturedContext;
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

        public void UnsafeOnCompleted(Action continuation, bool continueOnCapturedContext)
        {
            if (continueOnCapturedContext)
            {
                _capturedContext = SynchronizationContext.Current;
            }

            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, continuation, null) != null)
            {
                SynchronizationContext sc = _capturedContext;
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

        public void SetResult(T result)
        {
            _result = result;
            SignalCompletion();
        }

        public void SetException(Exception error)
        {
            _error = ExceptionDispatchInfo.Capture(error);
            SignalCompletion();
        }

        private void SignalCompletion()
        {
            _completed = true;
            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, ManualResetValueTaskObject.s_sentinel, null) != null)
            {
                if (_executionContext != null)
                {
                    ExecutionContext.Run(_executionContext, s => ((ManualResetValueTaskObject<T>)s).InvokeContinuation(), this);
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
                _capturedContext = null;
                sc.Post(s => ((Action)s)(), _continuation);
            }
            else
            {
                _continuation();
            }
        }
    }
}
