// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
    /// <summary>Provides an awaiter for a <see cref="ValueTask"/>.</summary>
    public readonly struct ValueTaskAwaiter : ICriticalNotifyCompletion, IValueTaskAwaiter
    {
        /// <summary>The value being awaited.</summary>
        private readonly ValueTask _value;

        /// <summary>Initializes the awaiter.</summary>
        /// <param name="value">The value to be awaited.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTaskAwaiter(ValueTask value) => _value = value;

        /// <summary>Gets whether the <see cref="ValueTask"/> has completed.</summary>
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value.IsCompleted;
        }

        /// <summary>Gets the result of the ValueTask.</summary>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetResult()
        {
            if (_value._obj != null)
            {
                if (_value.ObjectIsTask)
                {
                    _value.UnsafeTask.GetAwaiter().GetResult();
                }
                else
                {
                    _value.UnsafeValueTaskObject.GetResult();
                }
            }
        }

        /// <summary>Schedules the continuation action for this ValueTask.</summary>
        public void OnCompleted(Action continuation)
        {
            if (_value.ObjectIsTask)
            {
                _value.UnsafeTask.GetAwaiter().OnCompleted(continuation);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskObject.OnCompleted(continuation, continueOnCapturedContext: true);
            }
            else
            {
                Task.CompletedTask.GetAwaiter().OnCompleted(continuation);
            }
        }

        /// <summary>Schedules the continuation action for this ValueTask.</summary>
        public void UnsafeOnCompleted(Action continuation)
        {
            if (_value.ObjectIsTask)
            {
                _value.UnsafeTask.GetAwaiter().UnsafeOnCompleted(continuation);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskObject.UnsafeOnCompleted(continuation, continueOnCapturedContext: true);
            }
            else
            {
                Task.CompletedTask.GetAwaiter().UnsafeOnCompleted(continuation);
            }
        }

        /// <summary>Gets the task underlying <see cref="_value"/>.</summary>
        internal Task AsTask() => _value.AsTask();

        /// <summary>Gets the task underlying the incomplete <see cref="_value"/>.</summary>
        /// <remarks>This method is used when awaiting and IsCompleted returned false; thus we expect the value task to be wrapping a non-null task.</remarks>
        Task IValueTaskAwaiter.GetTask() => _value.ObjectIsTask ? _value.UnsafeTask : null;
    }

    /// <summary>Provides an awaiter for a <see cref="ValueTask{TResult}"/>.</summary>
    public readonly struct ValueTaskAwaiter<TResult> : ICriticalNotifyCompletion, IValueTaskAwaiter
    {
        /// <summary>The value being awaited.</summary>
        private readonly ValueTask<TResult> _value;

        /// <summary>Initializes the awaiter.</summary>
        /// <param name="value">The value to be awaited.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueTaskAwaiter(ValueTask<TResult> value) => _value = value;

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> has completed.</summary>
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value.IsCompleted;
        }

        /// <summary>Gets the result of the ValueTask.</summary>
        [StackTraceHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult GetResult() =>
            _value._obj == null ? _value._result :
            _value.ObjectIsTask ? _value.UnsafeTask.GetAwaiter().GetResult() :
            _value.UnsafeValueTaskObject.GetResult();

        /// <summary>Schedules the continuation action for this ValueTask.</summary>
        public void OnCompleted(Action continuation)
        {
            if (_value.ObjectIsTask)
            {
                _value.UnsafeTask.GetAwaiter().OnCompleted(continuation);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskObject.OnCompleted(continuation, continueOnCapturedContext: true);
            }
            else
            {
                Task.FromResult(_value._result).GetAwaiter().OnCompleted(continuation);
            }
        }

        /// <summary>Schedules the continuation action for this ValueTask.</summary>
        public void UnsafeOnCompleted(Action continuation)
        {
            if (_value.ObjectIsTask)
            {
                _value.UnsafeTask.GetAwaiter().UnsafeOnCompleted(continuation);
            }
            else if (_value._obj != null)
            {
                _value.UnsafeValueTaskObject.UnsafeOnCompleted(continuation, continueOnCapturedContext: true);
            }
            else
            {
                Task.FromResult(_value._result).GetAwaiter().UnsafeOnCompleted(continuation);
            }
        }

        /// <summary>Gets the task underlying <see cref="_value"/>.</summary>
        internal Task<TResult> AsTask() => _value.AsTask();

        /// <summary>Gets the task underlying the incomplete <see cref="_value"/>.</summary>
        /// <remarks>This method is used when awaiting and IsCompleted returned false; thus we expect the value task to be wrapping a non-null task.</remarks>
        Task IValueTaskAwaiter.GetTask() => _value.ObjectIsTask ? _value.UnsafeTask : null;
    }

    /// <summary>
    /// Internal interface used to enable extract the Task from arbitrary ValueTask awaiters.
    /// </summary>>
    internal interface IValueTaskAwaiter
    {
        Task GetTask();
    }
}
