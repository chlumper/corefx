// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Threading.Tasks
{
    /// <summary>Represents a <see cref="Task"/>-like object that can be wrapped by a <see cref="ValueTask"/>.</summary>
    public interface IValueTaskObject
    {
        /// <summary>Gets whether the <see cref="IValueTaskObject"/> represents a completed operation.</summary>
        bool IsCompleted { get; }

        /// <summary>Gets the result of the <see cref="IValueTaskObject"/>.</summary>
        void GetResult();
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject"/>.</summary>
        void OnCompleted(Action continuation);
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject"/>.</summary>
        void UnsafeOnCompleted(Action continuation);

        /// <summary>Gets whether the <see cref="IValueTaskObject"/> represents a successfully completed operation.</summary>
        bool IsCompletedSuccessfully { get; }
        /// <summary>Gets whether the <see cref="IValueTaskObject"/> represents a failed operation.</summary>
        bool IsFaulted { get; }
        /// <summary>Gets whether the <see cref="IValueTaskObject"/> represents a canceled operation.</summary>
        bool IsCanceled { get; }

        /// <summary>Gets a <see cref="IValueTaskObject"/> with the specified setting for whether to invoke continuations on the current context.</summary>
        /// <param name="continueOnCapturedContext">
        /// true to attempt to marshal the continuation back to the captured context; otherwise, false.
        /// </param>
        /// <returns>The configured <see cref="IValueTaskObject"/>.
        /// This may be the same instance mutated appropriately, or a new instance configured appropriately.
        /// </returns>
        IValueTaskObject ConfigureAwait(bool continueOnCapturedContext);
    }

    /// <summary>Represents a <see cref="Task{TResult}"/>-like object that can be wrapped by a <see cref="ValueTask{TResult}"/>.</summary>
    /// <typeparam name="TResult">Specifies the type of data returned from the object.</typeparam>
    public interface IValueTaskObject<out TResult>
    {
        /// <summary>Gets whether the <see cref="IValueTaskObject{TResult}"/> represents a completed operation.</summary>
        bool IsCompleted { get; }

        /// <summary>Gets the result of the <see cref="IValueTaskObject{TResult}"/>.</summary>
        TResult GetResult();
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject{TResult}"/>.</summary>
        void OnCompleted(Action continuation);
        /// <summary>Schedules the continuation action for this <see cref="IValueTaskObject{TResult}"/>.</summary>
        void UnsafeOnCompleted(Action continuation);

        /// <summary>Gets whether the <see cref="IValueTaskObject{TResult}"/> represents a successfully completed operation.</summary>
        bool IsCompletedSuccessfully { get; }
        /// <summary>Gets whether the <see cref="IValueTaskObject{TResult}"/> represents a failed operation.</summary>
        bool IsFaulted { get; }
        /// <summary>Gets whether the <see cref="IValueTaskObject{TResult}"/> represents a canceled operation.</summary>
        bool IsCanceled { get; }

        /// <summary>Gets a <see cref="IValueTaskObject{TResult}"/> with the specified setting for whether to invoke continuations on the current context.</summary>
        /// <param name="continueOnCapturedContext">
        /// true to attempt to marshal the continuation back to the captured context; otherwise, false.
        /// </param>
        /// <returns>The configured <see cref="IValueTaskObject{TResult}"/>.
        /// This may be the same instance mutated appropriately, or a new instance configured appropriately.
        /// </returns>
        IValueTaskObject<TResult> ConfigureAwait(bool continueOnCapturedContext);
    }
}
