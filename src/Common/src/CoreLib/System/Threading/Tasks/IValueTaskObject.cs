// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Threading.Tasks
{
    /// <summary>Represents a <see cref="Task"/>-like object that can be wrapped by a <see cref="ValueTask"/>.</summary>
    public interface IValueTaskObject
    {
        /// <summary>Gets whether the <see cref="IValueTaskObject"/> represents a completed operation.</summary>
        bool IsCompleted { get; }
        /// <summary>Gets whether the <see cref="IValueTaskObject"/> represents a successfully completed operation.</summary>
        bool IsCompletedSuccessfully { get; }

        /// <summary>Gets the result of the <see cref="IValueTaskObject"/>.</summary>
        void GetResult();
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject"/>.</summary>
        void OnCompleted(Action continuation, bool continueOnCapturedContext);
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject"/>.</summary>
        void UnsafeOnCompleted(Action continuation, bool continueOnCapturedContext);
    }

    /// <summary>Represents a <see cref="Task{TResult}"/>-like object that can be wrapped by a <see cref="ValueTask{TResult}"/>.</summary>
    /// <typeparam name="TResult">Specifies the type of data returned from the object.</typeparam>
    public interface IValueTaskObject<out TResult>
    {
        /// <summary>Gets whether the <see cref="IValueTaskObject{TResult}"/> represents a completed operation.</summary>
        bool IsCompleted { get; }
        /// <summary>Gets whether the <see cref="IValueTaskObject{TResult}"/> represents a successfully completed operation.</summary>
        bool IsCompletedSuccessfully { get; }

        /// <summary>Gets the result of the <see cref="IValueTaskObject{TResult}"/>.</summary>
        TResult GetResult();
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject{TResult}"/>.</summary>
        void OnCompleted(Action continuation, bool continueOnCapturedContext);
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject{TResult}"/>.</summary>
        void UnsafeOnCompleted(Action continuation, bool continueOnCapturedContext);
    }
}
