// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
    /// <summary>Provides an awaitable type that enables configured awaits on a <see cref="ValueTask"/>.</summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ConfiguredValueTaskAwaitable
    {
        /// <summary>The wrapped <see cref="Task"/>.</summary>
        private readonly ValueTask _value;

        /// <summary>Initializes the awaitable.</summary>
        /// <param name="value">The wrapped <see cref="ValueTask"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ConfiguredValueTaskAwaitable(ValueTask value) => _value = value;

        /// <summary>Returns an awaiter for this <see cref="ConfiguredValueTaskAwaitable"/> instance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredValueTaskAwaiter GetAwaiter() => new ConfiguredValueTaskAwaiter(_value);

        /// <summary>Provides an awaiter for a <see cref="ConfiguredValueTaskAwaitable"/>.</summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct ConfiguredValueTaskAwaiter : ICriticalNotifyCompletion, IConfiguredValueTaskAwaiter
        {
            /// <summary>The value being awaited.</summary>
            private readonly ValueTask _value;

            /// <summary>Initializes the awaiter.</summary>
            /// <param name="value">The value to be awaited.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ConfiguredValueTaskAwaiter(ValueTask value) => _value = value;

            /// <summary>Gets whether the <see cref="ConfiguredValueTaskAwaitable"/> has completed.</summary>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _value.IsCompleted;
            }

            /// <summary>Gets the result of the ValueTask.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [StackTraceHidden]
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

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredValueTaskAwaitable"/>.</summary>
            public void OnCompleted(Action continuation)
            {
                if (_value.ObjectIsTask)
                {
                    _value.UnsafeTask.ConfigureAwait(_value.ContinueOnCapturedContext).GetAwaiter().OnCompleted(continuation);
                }
                else if (_value._obj != null)
                {
                    _value.UnsafeValueTaskObject.OnCompleted(continuation, continueOnCapturedContext: _value.ContinueOnCapturedContext);
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(_value.ContinueOnCapturedContext).GetAwaiter().OnCompleted(continuation);
                }
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredValueTaskAwaitable"/>.</summary>
            public void UnsafeOnCompleted(Action continuation)
            {
                if (_value.ObjectIsTask)
                {
                    _value.UnsafeTask.ConfigureAwait(_value.ContinueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(continuation);
                }
                else if (_value._obj != null)
                {
                    _value.UnsafeValueTaskObject.UnsafeOnCompleted(continuation, _value.ContinueOnCapturedContext);
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(_value.ContinueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(continuation);
                }
            }

            /// <summary>Gets the task underlying <see cref="_value"/>.</summary>
            internal Task AsTask() => _value.AsTask();

            /// <summary>Gets the task underlying the incomplete <see cref="_value"/>.</summary>
            /// <remarks>
            /// This method is used when awaiting and IsCompleted returned false; thus we expect the value task to be wrapping a non-null task.
            /// If the ValueTask doesn't already wrap a Task, we return null, and a different path will be taken; this must not allocate.
            /// </remarks>
            Task IConfiguredValueTaskAwaiter.GetTask(out bool continueOnCapturedContext)
            {
                continueOnCapturedContext = _value.ContinueOnCapturedContext;
                return _value.ObjectIsTask ? _value.UnsafeTask : null;
            }
        }
    }

    /// <summary>Provides an awaitable type that enables configured awaits on a <see cref="ValueTask{TResult}"/>.</summary>
    /// <typeparam name="TResult">The type of the result produced.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ConfiguredValueTaskAwaitable<TResult>
    {
        /// <summary>The wrapped <see cref="ValueTask{TResult}"/>.</summary>
        private readonly ValueTask<TResult> _value;

        /// <summary>Initializes the awaitable.</summary>
        /// <param name="value">The wrapped <see cref="ValueTask{TResult}"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ConfiguredValueTaskAwaitable(ValueTask<TResult> value) => _value = value;

        /// <summary>Returns an awaiter for this <see cref="ConfiguredValueTaskAwaitable{TResult}"/> instance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredValueTaskAwaiter GetAwaiter() => new ConfiguredValueTaskAwaiter(_value);

        /// <summary>Provides an awaiter for a <see cref="ConfiguredValueTaskAwaitable{TResult}"/>.</summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct ConfiguredValueTaskAwaiter : ICriticalNotifyCompletion, IConfiguredValueTaskAwaiter
        {
            /// <summary>The value being awaited.</summary>
            private readonly ValueTask<TResult> _value;

            /// <summary>Initializes the awaiter.</summary>
            /// <param name="value">The value to be awaited.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ConfiguredValueTaskAwaiter(ValueTask<TResult> value) => _value = value;

            /// <summary>Gets whether the <see cref="ConfiguredValueTaskAwaitable{TResult}"/> has completed.</summary>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _value.IsCompleted;
            }

            /// <summary>Gets the result of the ValueTask.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [StackTraceHidden]
            public TResult GetResult() =>
                _value._obj == null ? _value._result :
                _value.ObjectIsTask ? _value.UnsafeTask.GetAwaiter().GetResult() :
                _value.UnsafeValueTaskObject.GetResult();

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredValueTaskAwaitable{TResult}"/>.</summary>
            public void OnCompleted(Action continuation)
            {
                if (_value.ObjectIsTask)
                {
                    _value.UnsafeTask.ConfigureAwait(_value.ContinueOnCapturedContext).GetAwaiter().OnCompleted(continuation);
                }
                else if (_value._obj != null)
                {
                    _value.UnsafeValueTaskObject.OnCompleted(continuation, _value.ContinueOnCapturedContext);
                }
                else
                {
                    Task.FromResult(_value._result).ConfigureAwait(_value.ContinueOnCapturedContext).GetAwaiter().OnCompleted(continuation);
                }
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredValueTaskAwaitable{TResult}"/>.</summary>
            public void UnsafeOnCompleted(Action continuation)
            {
                if (_value.ObjectIsTask)
                {
                    _value.UnsafeTask.ConfigureAwait(_value.ContinueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(continuation);
                }
                else if (_value._obj != null)
                {
                    _value.UnsafeValueTaskObject.UnsafeOnCompleted(continuation, _value.ContinueOnCapturedContext);
                }
                else
                {
                    Task.FromResult(_value._result).ConfigureAwait(_value.ContinueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(continuation);
                }
            }

            /// <summary>Gets the task underlying <see cref="_value"/>.</summary>
            internal Task<TResult> AsTask() => _value.AsTask();

            /// <summary>Gets the task underlying the incomplete <see cref="_value"/>.</summary>
            /// <remarks>
            /// This method is used when awaiting and IsCompleted returned false; thus we expect the value task to be wrapping a non-null task.
            /// If the ValueTask doesn't already wrap a Task, we return null, and a different path will be taken; this must not allocate.
            /// </remarks>
            Task IConfiguredValueTaskAwaiter.GetTask(out bool continueOnCapturedContext)
            {
                continueOnCapturedContext = _value.ContinueOnCapturedContext;
                return _value.ObjectIsTask ? _value.UnsafeTask : null;
            }
        }
    }

    /// <summary>
    /// Internal interface used to enable extract the Task from arbitrary configured ValueTask awaiters.
    /// </summary>
    internal interface IConfiguredValueTaskAwaiter
    {
        Task GetTask(out bool continueOnCapturedContext);
    }
}
