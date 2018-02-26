// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.ExceptionServices;

namespace System.Threading.Tasks
{
    internal sealed class ManualResetValueTaskSource
    {
        internal static readonly Action s_sentinel = new Action(() => { });
    }

    internal sealed class ManualResetValueTaskSource<T> : IValueTaskSource<T>
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

        public void OnCompleted(Action<object> continuation, object state, ValueTaskSourceOnCompletedFlags flags)
        {
            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                _executionContext = ExecutionContext.Capture();
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                _capturedContext = SynchronizationContext.Current;
            }

            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, continuation, null) != null)
            {
                SynchronizationContext sc = _capturedContext;
                if (sc != null)
                {
                    _capturedContext = null;
                    sc.Post(s =>
                    {
                        var tuple = (Tuple<Action<object>, object>)s;
                        tuple.Item1(tuple.Item2);
                    }, Tuple.Create(continuation, state));

                }
                else
                {
                    Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
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
            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, ManualResetValueTaskSource.s_sentinel, null) != null)
            {
                if (_executionContext != null)
                {
                    ExecutionContext.Run(_executionContext, s => ((ManualResetValueTaskSource<T>)s).InvokeContinuation(), this);
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
