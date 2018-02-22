// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if !netstandard
using Internal.Runtime.CompilerServices;
#endif

namespace System.Threading.Tasks
{
    /// <summary>Provides a value type that can represent a task object or a synchronously completed success result.</summary>
    [AsyncMethodBuilder(typeof(AsyncValueTaskMethodBuilder))]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueTask : IEquatable<ValueTask>
    {
#if netstandard
        private static readonly Task s_completedTask = Task.FromResult(true);
#endif

        /// <summary>null if representing a successful synchronous completion, otherwise a <see cref="Task"/> or a <see cref="IValueTaskObject"/>.</summary>
        internal readonly object _task;
        /// <summary>
        /// true if <see cref="_task"/> is a <see cref="Task"/>;
        /// false if it's null or a <see cref="IValueTaskObject"/>.
        /// </summary>
        internal readonly bool _taskIsTask;

        /// <summary>
        /// Initialize the <see cref="ValueTask"/> with a <see cref="Task"/> that represents the operation.
        /// </summary>
        /// <param name="task">The task.</param>
        public ValueTask(Task task)
        {
            if (task == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task);
            }

            _task = task;
            _taskIsTask = true;
        }

        /// <summary>
        /// Initialize the <see cref="ValueTask"/> with a <see cref="IValueTaskObject"/> that represents the operation.
        /// </summary>
        /// <param name="task">The value task object.</param>
        public ValueTask(IValueTaskObject task)
        {
            if (task == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task);
            }

            _task = task;
            _taskIsTask = false;
        }

        internal Task UnsafeTask => Unsafe.As<Task>(_task);
        internal IValueTaskObject UnsafeTaskObject => Unsafe.As<IValueTaskObject>(_task);

        /// <summary>Returns the hash code for this instance.</summary>
        public override int GetHashCode() => _task?.GetHashCode() ?? 0;

        /// <summary>Returns a value indicating whether this value is equal to a specified <see cref="object"/>.</summary>
        public override bool Equals(object obj) =>
            obj is ValueTask &&
            Equals((ValueTask)obj);

        /// <summary>Returns a value indicating whether this value is equal to a specified <see cref="ValueTask"/> value.</summary>
        public bool Equals(ValueTask other) => _task == other._task;

        /// <summary>Returns a value indicating whether two <see cref="ValueTask"/> values are equal.</summary>
        public static bool operator==(ValueTask left, ValueTask right) =>
            left.Equals(right);

        /// <summary>Returns a value indicating whether two <see cref="ValueTask"/> values are not equal.</summary>
        public static bool operator!=(ValueTask left, ValueTask right) =>
            !left.Equals(right);

        /// <summary>
        /// Gets a <see cref="Task"/> object to represent this ValueTask.  It will
        /// either return the wrapped task object if one exists, or it'll manufacture a new
        /// task object to represent the result.
        /// </summary>
        public Task AsTask() =>
            // Return the task if we were constructed from one, otherwise manufacture one.  We don't
            // cache the generated task into _task as it would end up changing both equality comparison
            // and the hash code we generate in GetHashCode.
            _task == null ?
#if netstandard
                s_completedTask :
#else
                Task.CompletedTask :
#endif
            _taskIsTask ? UnsafeTask :
            GetTaskForValueTaskObject();

        private Task GetTaskForValueTaskObject()
        {
            IValueTaskObject t = UnsafeTaskObject;

            if (t.IsCompleted)
            {
                bool canceled = t.IsCanceled;
                try
                {
                    t.GetResult();
                    return
#if netstandard
                        s_completedTask;
#else
                        Task.CompletedTask;
#endif
                }
                catch (Exception exc)
                {
#if netstandard
                    var tcs = new TaskCompletionSource<bool>();
                    if (canceled)
                    {
                        tcs.TrySetCanceled(exc is OperationCanceledException oce ? oce.CancellationToken : new CancellationToken(true));
                    }
                    else
                    {
                        tcs.TrySetException(exc);
                    }
                    return tcs.Task;
#else
                    return canceled ?
                        System.Threading.Tasks.Task.FromCanceled(exc is OperationCanceledException oce ? oce.CancellationToken : new CancellationToken(true)) :
                        System.Threading.Tasks.Task.FromException(exc);
#endif
                }
            }

            return new ValueTaskObjectCompletionSource(t).Task;
        }

        private sealed class ValueTaskObjectCompletionSource : TaskCompletionSource<bool>
        {
            private readonly IValueTaskObject _task;

            public ValueTaskObjectCompletionSource(IValueTaskObject task)
            {
                _task = task;
                task.UnsafeOnCompleted(new Action(OnCompleted));
            }

            private void OnCompleted()
            {
                bool canceled = _task.IsCanceled;
                try
                {
                    _task.GetResult();
                    TrySetResult(true);
                }
                catch (Exception exc)
                {
                    if (canceled)
                    {
                        TrySetCanceled(exc is OperationCanceledException oce ? oce.CancellationToken : new CancellationToken(true));
                    }
                    else
                    {
                        TrySetException(exc);
                    }
                }
            }
        }

        internal Task AsTaskExpectNonNull() =>
            // Return the task if we were constructed from one, otherwise manufacture one.
            // Unlike AsTask(), this method is called only when we expect _task to be non-null,
            // and thus we don't want GetTaskForResult inlined.
            _task == null ?
#if netstandard
                s_completedTask :
#else
                Task.CompletedTask :
#endif
            _taskIsTask ? UnsafeTask :
            GetTaskForValueTaskObject();

        /// <summary>Gets whether the <see cref="ValueTask"/> represents a completed operation.</summary>
        public bool IsCompleted =>
            _task == null ||
            (_taskIsTask ? UnsafeTask.IsCompleted : UnsafeTaskObject.IsCompleted);

        /// <summary>Gets whether the <see cref="ValueTask"/> represents a successfully completed operation.</summary>
        public bool IsCompletedSuccessfully =>
            _task == null ||
            (_taskIsTask ?
#if netstandard
                UnsafeTask.Status == TaskStatus.RanToCompletion :
#else
                UnsafeTask.IsCompletedSuccessfully :
#endif
                UnsafeTaskObject.IsCompletedSuccessfully);

        /// <summary>Gets whether the <see cref="ValueTask"/> represents a failed operation.</summary>
        public bool IsFaulted =>
            _task != null &&
            (_taskIsTask ? UnsafeTask.IsFaulted : UnsafeTaskObject.IsFaulted);

        /// <summary>Gets whether the <see cref="ValueTask"/> represents a canceled operation.</summary>
        public bool IsCanceled =>
            _task != null &&
            (_taskIsTask ? UnsafeTask.IsCanceled : UnsafeTaskObject.IsCanceled);

        /// <summary>Gets an awaiter for this value.</summary>
        public ValueTaskAwaiter GetAwaiter() => new ValueTaskAwaiter(this);

        /// <summary>Configures an awaiter for this value.</summary>
        /// <param name="continueOnCapturedContext">
        /// true to attempt to marshal the continuation back to the captured context; otherwise, false.
        /// </param>
        public ConfiguredValueTaskAwaitable ConfigureAwait(bool continueOnCapturedContext) =>
            new ConfiguredValueTaskAwaitable(this, continueOnCapturedContext);
    }

    /// <summary>Provides a value type that can represent a synchronously available value or a task object.</summary>
    /// <typeparam name="TResult">Specifies the type of the result.</typeparam>
    [AsyncMethodBuilder(typeof(AsyncValueTaskMethodBuilder<>))]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ValueTask<TResult> : IEquatable<ValueTask<TResult>>
    {
        /// <summary>null if <see cref="_result"/> has the result, otherwise a <see cref="Task{TResult}"/> or a <see cref="IValueTaskObject{TResult}"/>.</summary>
        internal readonly object _task;
        /// <summary>The result to be used if the operation completed successfully synchronously.</summary>
        internal readonly TResult _result;
        /// <summary>
        /// true if <see cref="_task"/> is a <see cref="Task{TResult}"/>;
        /// false if it's null or a <see cref="IValueTaskObject{TResult}"/>.
        /// </summary>
        internal readonly bool _taskIsTask;

        /// <summary>Initialize the <see cref="ValueTask{TResult}"/> with the result of the successful operation.</summary>
        /// <param name="result">The result.</param>
        public ValueTask(TResult result)
        {
            _task = null;
            _result = result;
            _taskIsTask = false;
        }

        /// <summary>
        /// Initialize the <see cref="ValueTask{TResult}"/> with a <see cref="Task{TResult}"/> that represents the operation.
        /// </summary>
        /// <param name="task">The task.</param>
        public ValueTask(Task<TResult> task)
        {
            if (task == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task);
            }

            _task = task;
            _result = default;
            _taskIsTask = true;
        }

        /// <summary>
        /// Initialize the <see cref="ValueTask{TResult}"/> with a <see cref="IValueTaskObject{TResult}"/> that represents the operation.
        /// </summary>
        /// <param name="task">The value task object.</param>
        public ValueTask(IValueTaskObject<TResult> task)
        {
            if (task == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.task);
            }

            _task = task;
            _result = default;
            _taskIsTask = false;
        }

        internal Task<TResult> UnsafeTask => Unsafe.As<Task<TResult>>(_task);
        internal IValueTaskObject<TResult> UnsafeTaskObject => Unsafe.As<IValueTaskObject<TResult>>(_task);

        /// <summary>Returns the hash code for this instance.</summary>
        public override int GetHashCode() =>
            _task != null ? _task.GetHashCode() :
            _result != null ? _result.GetHashCode() :
            0;

        /// <summary>Returns a value indicating whether this value is equal to a specified <see cref="object"/>.</summary>
        public override bool Equals(object obj) =>
            obj is ValueTask<TResult> &&
            Equals((ValueTask<TResult>)obj);

        /// <summary>Returns a value indicating whether this value is equal to a specified <see cref="ValueTask{TResult}"/> value.</summary>
        public bool Equals(ValueTask<TResult> other) =>
            _task != null || other._task != null ?
                _task == other._task :
                EqualityComparer<TResult>.Default.Equals(_result, other._result);

        /// <summary>Returns a value indicating whether two <see cref="ValueTask{TResult}"/> values are equal.</summary>
        public static bool operator==(ValueTask<TResult> left, ValueTask<TResult> right) =>
            left.Equals(right);

        /// <summary>Returns a value indicating whether two <see cref="ValueTask{TResult}"/> values are not equal.</summary>
        public static bool operator!=(ValueTask<TResult> left, ValueTask<TResult> right) =>
            !left.Equals(right);

        /// <summary>
        /// Gets a <see cref="Task{TResult}"/> object to represent this ValueTask.  It will
        /// either return the wrapped task object if one exists, or it'll manufacture a new
        /// task object to represent the result.
        /// </summary>
        public Task<TResult> AsTask() =>
            // Return the task if we were constructed from one, otherwise manufacture one.  We don't
            // cache the generated task into _task as it would end up changing both equality comparison
            // and the hash code we generate in GetHashCode.
            _task == null ?
#if netstandard
                Task.FromResult(_result) :
#else
                AsyncTaskMethodBuilder<TResult>.GetTaskForResult(_result) :
#endif
            _taskIsTask? UnsafeTask :
            GetTaskForValueTaskObject();

        private Task<TResult> GetTaskForValueTaskObject()
        {
            IValueTaskObject<TResult> t = UnsafeTaskObject;

            if (t.IsCompleted)
            {
                bool canceled = t.IsCanceled;
                try
                {
                    return Task.FromResult(t.GetResult());
                }
                catch (Exception exc)
                {
#if netstandard
                    var tcs = new TaskCompletionSource<TResult>();
                    if (canceled)
                    {
                        tcs.TrySetCanceled(exc is OperationCanceledException oce ? oce.CancellationToken : new CancellationToken(true));
                    }
                    else
                    {
                        tcs.TrySetException(exc);
                    }
                    return tcs.Task;
#else
                    return canceled ?
                        System.Threading.Tasks.Task.FromCanceled<TResult>(exc is OperationCanceledException oce ? oce.CancellationToken : new CancellationToken(true)) :
                        System.Threading.Tasks.Task.FromException<TResult>(exc);
#endif
                }
            }

            return new ValueTaskObjectCompletionSource(t).Task;
        }

        private sealed class ValueTaskObjectCompletionSource : TaskCompletionSource<TResult>
        {
            private readonly IValueTaskObject<TResult> _task;

            public ValueTaskObjectCompletionSource(IValueTaskObject<TResult> task)
            {
                _task = task;
                task.UnsafeOnCompleted(new Action(OnCompleted));
            }

            private void OnCompleted()
            {
                bool canceled = _task.IsCanceled;
                try
                {
                    TrySetResult(_task.GetResult());
                }
                catch (Exception exc)
                {
                    if (canceled)
                    {
                        TrySetCanceled(exc is OperationCanceledException oce ? oce.CancellationToken : new CancellationToken(true));
                    }
                    else
                    {
                        TrySetException(exc);
                    }
                }
            }
        }

        internal Task<TResult> AsTaskExpectNonNull() =>
            // Return the task if we were constructed from one, otherwise manufacture one.
            // Unlike AsTask(), this method is called only when we expect _task to be non-null,
            // and thus we don't want GetTaskForResult inlined.
            _task == null ? GetTaskForResultNoInlining() :
            _taskIsTask ? UnsafeTask :
            GetTaskForValueTaskObject();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private Task<TResult> GetTaskForResultNoInlining() =>
#if netstandard
            Task.FromResult(_result);
#else
            AsyncTaskMethodBuilder<TResult>.GetTaskForResult(_result);
#endif

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> represents a completed operation.</summary>
        public bool IsCompleted =>
            _task == null ||
            (_taskIsTask ? UnsafeTask.IsCompleted : UnsafeTaskObject.IsCompleted);

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> represents a successfully completed operation.</summary>
        public bool IsCompletedSuccessfully =>
            _task == null ||
            (_taskIsTask ?
#if netstandard
                UnsafeTask.Status == TaskStatus.RanToCompletion :
#else
                UnsafeTask.IsCompletedSuccessfully :
#endif
                UnsafeTaskObject.IsCompletedSuccessfully);

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> represents a failed operation.</summary>
        public bool IsFaulted =>
            _task != null &&
            (_taskIsTask ? UnsafeTask.IsFaulted : UnsafeTaskObject.IsFaulted);

        /// <summary>Gets whether the <see cref="ValueTask{TResult}"/> represents a canceled operation.</summary>
        public bool IsCanceled =>
            _task != null &&
            (_taskIsTask ? UnsafeTask.IsCanceled : UnsafeTaskObject.IsCanceled);

        /// <summary>Gets the result.</summary>
        public TResult Result =>
            _task == null ? _result :
            _taskIsTask ? UnsafeTask.GetAwaiter().GetResult() :
            UnsafeTaskObject.GetResult();

        /// <summary>Gets an awaiter for this value.</summary>
        public ValueTaskAwaiter<TResult> GetAwaiter() => new ValueTaskAwaiter<TResult>(this);

        /// <summary>Configures an awaiter for this value.</summary>
        /// <param name="continueOnCapturedContext">
        /// true to attempt to marshal the continuation back to the captured context; otherwise, false.
        /// </param>
        public ConfiguredValueTaskAwaitable<TResult> ConfigureAwait(bool continueOnCapturedContext) =>
            new ConfiguredValueTaskAwaitable<TResult>(this, continueOnCapturedContext);

        /// <summary>Gets a string-representation of this <see cref="ValueTask{TResult}"/>.</summary>
        public override string ToString()
        {
            if (IsCompletedSuccessfully)
            {
                TResult result = Result;
                if (result != null)
                {
                    return result.ToString();
                }
            }

            return string.Empty;
        }
    }
}
