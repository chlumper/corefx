// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace System.Threading.Tasks.Tests
{
    public class ValueTaskTests
    {
        public enum CtorMode
        {
            Result,
            Task,
            ValueTaskSource
        }

        [Fact]
        public void NonGeneric_DefaultValueTask_DefaultValue()
        {
            Assert.True(default(ValueTask).IsCompleted);
            Assert.True(default(ValueTask).IsCompletedSuccessfully);
            Assert.False(default(ValueTask).IsFaulted);
            Assert.False(default(ValueTask).IsCanceled);
        }

        [Fact]
        public void Generic_DefaultValueTask_DefaultValue()
        {
            Assert.True(default(ValueTask<int>).IsCompleted);
            Assert.True(default(ValueTask<int>).IsCompletedSuccessfully);
            Assert.False(default(ValueTask<int>).IsFaulted);
            Assert.False(default(ValueTask<int>).IsCanceled);
            Assert.Equal(0, default(ValueTask<int>).Result);

            Assert.True(default(ValueTask<string>).IsCompleted);
            Assert.True(default(ValueTask<string>).IsCompletedSuccessfully);
            Assert.False(default(ValueTask<string>).IsFaulted);
            Assert.False(default(ValueTask<string>).IsCanceled);
            Assert.Equal(null, default(ValueTask<string>).Result);
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void NonGeneric_CreateFromSuccessfullyCompleted_IsCompletedSuccessfully(CtorMode mode)
        {
            ValueTask t =
                mode == CtorMode.Result ? default :
                mode == CtorMode.Task ? new ValueTask(Task.CompletedTask) :
                new ValueTask(ManualResetValueTaskSource.Completed(0, null));
            Assert.True(t.IsCompleted);
            Assert.True(t.IsCompletedSuccessfully);
            Assert.False(t.IsFaulted);
            Assert.False(t.IsCanceled);
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void Generic_CreateFromSuccessfullyCompleted_IsCompletedSuccessfully(CtorMode mode)
        {
            ValueTask<int> t =
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null));
            Assert.True(t.IsCompleted);
            Assert.True(t.IsCompletedSuccessfully);
            Assert.False(t.IsFaulted);
            Assert.False(t.IsCanceled);
            Assert.Equal(42, t.Result);
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void NonGeneric_CreateFromNotCompleted_ThenCompleteSuccessfully(CtorMode mode)
        {
            object completer = null;
            ValueTask t = default;
            switch (mode)
            {
                case CtorMode.Task:
                    var tcs = new TaskCompletionSource<int>();
                    t = new ValueTask(tcs.Task);
                    completer = tcs;
                    break;

                case CtorMode.ValueTaskSource:
                    var mre = new ManualResetValueTaskSource<int>();
                    t = new ValueTask(mre);
                    completer = mre;
                    break;
            }

            Assert.False(t.IsCompleted);
            Assert.False(t.IsCompletedSuccessfully);
            Assert.False(t.IsFaulted);
            Assert.False(t.IsCanceled);

            switch (mode)
            {
                case CtorMode.Task:
                    ((TaskCompletionSource<int>)completer).SetResult(42);
                    break;

                case CtorMode.ValueTaskSource:
                    ((ManualResetValueTaskSource<int>)completer).SetResult(42);
                    break;
            }

            Assert.True(t.IsCompleted);
            Assert.True(t.IsCompletedSuccessfully);
            Assert.False(t.IsFaulted);
            Assert.False(t.IsCanceled);
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void Generic_CreateFromNotCompleted_ThenCompleteSuccessfully(CtorMode mode)
        {
            object completer = null;
            ValueTask<int> t = default;
            switch (mode)
            {
                case CtorMode.Task:
                    var tcs = new TaskCompletionSource<int>();
                    t = new ValueTask<int>(tcs.Task);
                    completer = tcs;
                    break;

                case CtorMode.ValueTaskSource:
                    var mre = new ManualResetValueTaskSource<int>();
                    t = new ValueTask<int>(mre);
                    completer = mre;
                    break;
            }

            Assert.False(t.IsCompleted);
            Assert.False(t.IsCompletedSuccessfully);
            Assert.False(t.IsFaulted);
            Assert.False(t.IsCanceled);

            switch (mode)
            {
                case CtorMode.Task:
                    ((TaskCompletionSource<int>)completer).SetResult(42);
                    break;

                case CtorMode.ValueTaskSource:
                    ((ManualResetValueTaskSource<int>)completer).SetResult(42);
                    break;
            }

            Assert.Equal(42, t.Result);
            Assert.True(t.IsCompleted);
            Assert.True(t.IsCompletedSuccessfully);
            Assert.False(t.IsFaulted);
            Assert.False(t.IsCanceled);
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void NonGeneric_CreateFromNotCompleted_ThenFault(CtorMode mode)
        {
            object completer = null;
            ValueTask t = default;
            switch (mode)
            {
                case CtorMode.Task:
                    var tcs = new TaskCompletionSource<int>();
                    t = new ValueTask(tcs.Task);
                    completer = tcs;
                    break;

                case CtorMode.ValueTaskSource:
                    var mre = new ManualResetValueTaskSource<int>();
                    t = new ValueTask(mre);
                    completer = mre;
                    break;
            }

            Assert.False(t.IsCompleted);
            Assert.False(t.IsCompletedSuccessfully);
            Assert.False(t.IsFaulted);
            Assert.False(t.IsCanceled);

            Exception e = new InvalidOperationException();

            switch (mode)
            {
                case CtorMode.Task:
                    ((TaskCompletionSource<int>)completer).SetException(e);
                    break;

                case CtorMode.ValueTaskSource:
                    ((ManualResetValueTaskSource<int>)completer).SetException(e);
                    break;
            }

            Assert.True(t.IsCompleted);
            Assert.False(t.IsCompletedSuccessfully);
            Assert.True(t.IsFaulted);
            Assert.False(t.IsCanceled);

            Assert.Same(e, Assert.Throws<InvalidOperationException>(() => t.GetAwaiter().GetResult()));
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void Generic_CreateFromNotCompleted_ThenFault(CtorMode mode)
        {
            object completer = null;
            ValueTask<int> t = default;
            switch (mode)
            {
                case CtorMode.Task:
                    var tcs = new TaskCompletionSource<int>();
                    t = new ValueTask<int>(tcs.Task);
                    completer = tcs;
                    break;

                case CtorMode.ValueTaskSource:
                    var mre = new ManualResetValueTaskSource<int>();
                    t = new ValueTask<int>(mre);
                    completer = mre;
                    break;
            }

            Assert.False(t.IsCompleted);
            Assert.False(t.IsCompletedSuccessfully);
            Assert.False(t.IsFaulted);
            Assert.False(t.IsCanceled);

            Exception e = new InvalidOperationException();

            switch (mode)
            {
                case CtorMode.Task:
                    ((TaskCompletionSource<int>)completer).SetException(e);
                    break;

                case CtorMode.ValueTaskSource:
                    ((ManualResetValueTaskSource<int>)completer).SetException(e);
                    break;
            }

            Assert.True(t.IsCompleted);
            Assert.False(t.IsCompletedSuccessfully);
            Assert.True(t.IsFaulted);
            Assert.False(t.IsCanceled);

            Assert.Same(e, Assert.Throws<InvalidOperationException>(() => t.Result));
            Assert.Same(e, Assert.Throws<InvalidOperationException>(() => t.GetAwaiter().GetResult()));
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void NonGeneric_CreateFromFaulted_IsFaulted(CtorMode mode)
        {
            InvalidOperationException e = new InvalidOperationException();
            ValueTask t = mode == CtorMode.Task ? new ValueTask(Task.FromException(e)) : new ValueTask(ManualResetValueTaskSource.Completed<int>(0, e));

            Assert.True(t.IsCompleted);
            Assert.False(t.IsCompletedSuccessfully);
            Assert.True(t.IsFaulted);
            Assert.False(t.IsCanceled);

            Assert.Same(e, Assert.Throws<InvalidOperationException>(() => t.GetAwaiter().GetResult()));
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void Generic_CreateFromFaulted_IsFaulted(CtorMode mode)
        {
            InvalidOperationException e = new InvalidOperationException();
            ValueTask<int> t = mode == CtorMode.Task ? new ValueTask<int>(Task.FromException<int>(e)) : new ValueTask<int>(ManualResetValueTaskSource.Completed<int>(0, e));

            Assert.True(t.IsCompleted);
            Assert.False(t.IsCompletedSuccessfully);
            Assert.True(t.IsFaulted);
            Assert.False(t.IsCanceled);

            Assert.Same(e, Assert.Throws<InvalidOperationException>(() => t.Result));
            Assert.Same(e, Assert.Throws<InvalidOperationException>(() => t.GetAwaiter().GetResult()));
        }

        [Fact]
        public void NonGeneric_CreateFromNullTask_Throws()
        {
            AssertExtensions.Throws<ArgumentNullException>("task", () => new ValueTask((Task)null));
            AssertExtensions.Throws<ArgumentNullException>("source", () => new ValueTask((IValueTaskSource)null));
        }

        [Fact]
        public void Generic_CreateFromNullTask_Throws()
        {
            AssertExtensions.Throws<ArgumentNullException>("task", () => new ValueTask<int>((Task<int>)null));
            AssertExtensions.Throws<ArgumentNullException>("task", () => new ValueTask<string>((Task<string>)null));

            AssertExtensions.Throws<ArgumentNullException>("source", () => new ValueTask<int>((IValueTaskSource<int>)null));
            AssertExtensions.Throws<ArgumentNullException>("source", () => new ValueTask<string>((IValueTaskSource<string>)null));
        }

        [Fact]
        public void NonGeneric_CreateFromTask_AsTaskIdempotent()
        {
            Task source = Task.FromResult(42);
            ValueTask t = new ValueTask(source);
            Assert.Same(source, t.AsTask());
            Assert.Same(t.AsTask(), t.AsTask());
        }

        [Fact]
        public void Generic_CreateFromTask_AsTaskIdempotent()
        {
            Task<int> source = Task.FromResult(42);
            ValueTask<int> t = new ValueTask<int>(source);
            Assert.Same(source, t.AsTask());
            Assert.Same(t.AsTask(), t.AsTask());
        }

        [Fact]
        public void NonGeneric_CreateFromDefault_AsTaskIdempotent()
        {
            ValueTask t = new ValueTask();
            Assert.Same(t.AsTask(), t.AsTask());
        }

        [Fact]
        public void Generic_CreateFromValue_AsTaskNotIdempotent()
        {
            ValueTask<int> t = new ValueTask<int>(42);
            Assert.NotSame(Task.FromResult(42), t.AsTask());
            Assert.NotSame(t.AsTask(), t.AsTask());
        }

        [Fact]
        public void NonGeneric_CreateFromValueTaskSource_AsTaskIdempotent() // validates unsupported behavior specific to the backing IValueTaskSource
        {
            ValueTask vt = new ValueTask(ManualResetValueTaskSource.Completed<int>(42, null));
            Task t = vt.AsTask();
            Assert.NotNull(t);
            Assert.Same(t, vt.AsTask());
            Assert.Same(Task.CompletedTask, vt.AsTask());
        }

        [Fact]
        public void Generic_CreateFromValueTaskSource_AsTaskNotIdempotent() // validates unsupported behavior specific to the backing IValueTaskSource
        {
            ValueTask<int> t = new ValueTask<int>(ManualResetValueTaskSource.Completed<int>(42, null));
            Assert.NotSame(Task.FromResult(42), t.AsTask());
            Assert.NotSame(t.AsTask(), t.AsTask());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NonGeneric_CreateFromValueTaskSource_Success(bool sync)
        {
            ValueTask vt = new ValueTask(sync ? ManualResetValueTaskSource.Completed(0) : ManualResetValueTaskSource.Delay(1, 0));
            Task t = vt.AsTask();
            if (sync)
            {
                Assert.True(t.Status == TaskStatus.RanToCompletion);
            }
            await t;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Generic_CreateFromValueTaskSource_Success(bool sync)
        {
            ValueTask<int> vt = new ValueTask<int>(sync ? ManualResetValueTaskSource.Completed(42) : ManualResetValueTaskSource.Delay(1, 42));
            Task<int> t = vt.AsTask();
            if (sync)
            {
                Assert.True(t.Status == TaskStatus.RanToCompletion);
            }
            Assert.Equal(42, await t);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NonGeneric_CreateFromValueTaskSource_Faulted(bool sync)
        {
            ValueTask vt = new ValueTask(sync ? ManualResetValueTaskSource.Completed(0, new FormatException()) : ManualResetValueTaskSource.Delay(1, 0, new FormatException()));
            Task t = vt.AsTask();
            if (sync)
            {
                Assert.True(t.IsFaulted);
                Assert.IsType<FormatException>(t.Exception.InnerException);
            }
            else
            {
                await Assert.ThrowsAsync<FormatException>(() => t);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Generic_CreateFromValueTaskSource_Faulted(bool sync)
        {
            ValueTask<int> vt = new ValueTask<int>(sync ? ManualResetValueTaskSource.Completed(0, new FormatException()) : ManualResetValueTaskSource.Delay(1, 0, new FormatException()));
            Task<int> t = vt.AsTask();
            if (sync)
            {
                Assert.True(t.IsFaulted);
                Assert.IsType<FormatException>(t.Exception.InnerException);
            }
            else
            {
                await Assert.ThrowsAsync<FormatException>(() => t);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NonGeneric_CreateFromValueTaskSource_Canceled(bool sync)
        {
            ValueTask vt = new ValueTask(sync ? ManualResetValueTaskSource.Completed(0, new OperationCanceledException()) : ManualResetValueTaskSource.Delay(1, 0, new OperationCanceledException()));
            Task t = vt.AsTask();
            if (sync)
            {
                Assert.True(t.IsCanceled);
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t);
                Assert.True(t.IsCanceled);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Generic_CreateFromValueTaskSource_Canceled(bool sync)
        {
            ValueTask<int> vt = new ValueTask<int>(sync ? ManualResetValueTaskSource.Completed(0, new OperationCanceledException()) : ManualResetValueTaskSource.Delay(1, 0, new OperationCanceledException()));
            Task<int> t = vt.AsTask();
            if (sync)
            {
                Assert.True(t.IsCanceled);
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t);
                Assert.True(t.IsCanceled);
            }
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public async Task NonGeneric_CreateFromCompleted_Await(CtorMode mode)
        {
            ValueTask Create() =>
                mode == CtorMode.Result ? new ValueTask() :
                mode == CtorMode.Task ? new ValueTask(Task.FromResult(42)) :
                new ValueTask(ManualResetValueTaskSource.Completed(0, null));

            int thread = Environment.CurrentManagedThreadId;

            await Create();
            Assert.Equal(thread, Environment.CurrentManagedThreadId);

            await Create().ConfigureAwait(false);
            Assert.Equal(thread, Environment.CurrentManagedThreadId);

            await Create().ConfigureAwait(true);
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public async Task Generic_CreateFromCompleted_Await(CtorMode mode)
        {
            ValueTask<int> Create() =>
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null));

            int thread = Environment.CurrentManagedThreadId;

            Assert.Equal(42, await Create());
            Assert.Equal(thread, Environment.CurrentManagedThreadId);

            Assert.Equal(42, await Create().ConfigureAwait(false));
            Assert.Equal(thread, Environment.CurrentManagedThreadId);

            Assert.Equal(42, await Create().ConfigureAwait(true));
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NonGeneric_CreateFromTask_Await_Normal(bool? continueOnCapturedContext)
        {
            var t = new ValueTask(Task.Delay(1));
            switch (continueOnCapturedContext)
            {
                case null: await t; break;
                default: await t.ConfigureAwait(continueOnCapturedContext.Value); break;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Generic_CreateFromTask_Await_Normal(bool? continueOnCapturedContext)
        {
            var t = new ValueTask<int>(Task.Delay(1).ContinueWith(_ => 42));
            switch (continueOnCapturedContext)
            {
                case null: Assert.Equal(42, await t); break;
                default: Assert.Equal(42, await t.ConfigureAwait(continueOnCapturedContext.Value)); break;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreateFromValueTaskSource_Await_Normal(bool? continueOnCapturedContext)
        {
            var mre = new ManualResetValueTaskSource<int>();
            ValueTask t = new ValueTask(mre);
            var ignored = Task.Delay(1).ContinueWith(_ => mre.SetResult(42));
            switch (continueOnCapturedContext)
            {
                case null: await t; break;
                default: await t.ConfigureAwait(continueOnCapturedContext.Value); break;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Generic_CreateFromValueTaskSource_Await_Normal(bool? continueOnCapturedContext)
        {
            var mre = new ManualResetValueTaskSource<int>();
            ValueTask<int> t = new ValueTask<int>(mre);
            var ignored = Task.Delay(1).ContinueWith(_ => mre.SetResult(42));
            switch (continueOnCapturedContext)
            {
                case null: Assert.Equal(42, await t); break;
                default: Assert.Equal(42, await t.ConfigureAwait(continueOnCapturedContext.Value)); break;
            }
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public async Task NonGeneric_Awaiter_OnCompleted(CtorMode mode)
        {
            ValueTask t =
                mode == CtorMode.Result ? new ValueTask() :
                mode == CtorMode.Task ? new ValueTask(Task.CompletedTask) :
                new ValueTask(ManualResetValueTaskSource.Completed(0, null));

            var tcs = new TaskCompletionSource<bool>();
            t.GetAwaiter().OnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public async Task NonGeneric_Awaiter_UnsafeOnCompleted(CtorMode mode)
        {
            ValueTask t =
                mode == CtorMode.Result ? new ValueTask() :
                mode == CtorMode.Task ? new ValueTask(Task.CompletedTask) :
                new ValueTask(ManualResetValueTaskSource.Completed(0, null));

            var tcs = new TaskCompletionSource<bool>();
            t.GetAwaiter().UnsafeOnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public async Task Generic_Awaiter_OnCompleted(CtorMode mode)
        {
            ValueTask<int> t =
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null));

            var tcs = new TaskCompletionSource<bool>();
            t.GetAwaiter().OnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public async Task Generic_Awaiter_UnsafeOnCompleted(CtorMode mode)
        {
            ValueTask<int> t =
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null));

            var tcs = new TaskCompletionSource<bool>();
            t.GetAwaiter().UnsafeOnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result, true)]
        [InlineData(CtorMode.Task, true)]
        [InlineData(CtorMode.ValueTaskSource, true)]
        [InlineData(CtorMode.Result, false)]
        [InlineData(CtorMode.Task, false)]
        [InlineData(CtorMode.ValueTaskSource, false)]
        public async Task NonGeneric_ConfiguredAwaiter_OnCompleted(CtorMode mode, bool continueOnCapturedContext)
        {
            ValueTask t =
                mode == CtorMode.Result ? new ValueTask() :
                mode == CtorMode.Task ? new ValueTask(Task.CompletedTask) :
                new ValueTask(ManualResetValueTaskSource.Completed(0, null));

            var tcs = new TaskCompletionSource<bool>();
            t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().OnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result, true)]
        [InlineData(CtorMode.Task, true)]
        [InlineData(CtorMode.ValueTaskSource, true)]
        [InlineData(CtorMode.Result, false)]
        [InlineData(CtorMode.Task, false)]
        [InlineData(CtorMode.ValueTaskSource, false)]
        public async Task NonGeneric_ConfiguredAwaiter_UnsafeOnCompleted(CtorMode mode, bool continueOnCapturedContext)
        {
            ValueTask t =
                mode == CtorMode.Result ? new ValueTask() :
                mode == CtorMode.Task ? new ValueTask(Task.CompletedTask) :
                new ValueTask(ManualResetValueTaskSource.Completed(0, null));

            var tcs = new TaskCompletionSource<bool>();
            t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result, true)]
        [InlineData(CtorMode.Task, true)]
        [InlineData(CtorMode.ValueTaskSource, true)]
        [InlineData(CtorMode.Result, false)]
        [InlineData(CtorMode.Task, false)]
        [InlineData(CtorMode.ValueTaskSource, false)]
        public async Task Generic_ConfiguredAwaiter_OnCompleted(CtorMode mode, bool continueOnCapturedContext)
        {
            ValueTask<int> t =
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null));

            var tcs = new TaskCompletionSource<bool>();
            t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().OnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result, true)]
        [InlineData(CtorMode.Task, true)]
        [InlineData(CtorMode.ValueTaskSource, true)]
        [InlineData(CtorMode.Result, false)]
        [InlineData(CtorMode.Task, false)]
        [InlineData(CtorMode.ValueTaskSource, false)]
        public async Task Generic_ConfiguredAwaiter_UnsafeOnCompleted(CtorMode mode, bool continueOnCapturedContext)
        {
            ValueTask<int> t =
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null));

            var tcs = new TaskCompletionSource<bool>();
            t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public async Task NonGeneric_Awaiter_ContinuesOnCapturedContext(CtorMode mode)
        {
            await Task.Run(() =>
            {
                var tsc = new TrackingSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(tsc);
                try
                {
                    ValueTask t =
                        mode == CtorMode.Result ? new ValueTask() :
                        mode == CtorMode.Task ? new ValueTask(Task.CompletedTask) :
                        new ValueTask(ManualResetValueTaskSource.Completed(0, null));

                    var mres = new ManualResetEventSlim();
                    t.GetAwaiter().OnCompleted(() => mres.Set());
                    Assert.True(mres.Wait(10000));
                    Assert.Equal(1, tsc.Posts);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });
        }

        [Theory]
        [InlineData(CtorMode.Task, false)]
        [InlineData(CtorMode.ValueTaskSource, false)]
        [InlineData(CtorMode.Result, true)]
        [InlineData(CtorMode.Task, true)]
        [InlineData(CtorMode.ValueTaskSource, true)]
        public async Task Generic_Awaiter_ContinuesOnCapturedContext(CtorMode mode, bool sync)
        {
            await Task.Run(() =>
            {
                var tsc = new TrackingSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(tsc);
                try
                {
                    ValueTask<int> t =
                        mode == CtorMode.Result ? new ValueTask<int>(42) :
                        mode == CtorMode.Task ? new ValueTask<int>(sync ? Task.FromResult(42) : Task.Delay(1).ContinueWith(_ => 42)) :
                        new ValueTask<int>(sync ? ManualResetValueTaskSource.Completed(42, null) : ManualResetValueTaskSource.Delay(1, 42, null));

                    var mres = new ManualResetEventSlim();
                    t.GetAwaiter().OnCompleted(() => mres.Set());
                    Assert.True(mres.Wait(10000));
                    Assert.Equal(1, tsc.Posts);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });
        }

        [Theory]
        [InlineData(CtorMode.Task, true, false)]
        [InlineData(CtorMode.ValueTaskSource, true, false)]
        [InlineData(CtorMode.Task, false, false)]
        [InlineData(CtorMode.ValueTaskSource, false, false)]
        [InlineData(CtorMode.Result, true, true)]
        [InlineData(CtorMode.Task, true, true)]
        [InlineData(CtorMode.ValueTaskSource, true, true)]
        [InlineData(CtorMode.Result, false, true)]
        [InlineData(CtorMode.Task, false, true)]
        [InlineData(CtorMode.ValueTaskSource, false, true)]
        public async Task NonGeneric_ConfiguredAwaiter_ContinuesOnCapturedContext(CtorMode mode, bool continueOnCapturedContext, bool sync)
        {
            await Task.Run(() =>
            {
                var tsc = new TrackingSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(tsc);
                try
                {
                    ValueTask t =
                        mode == CtorMode.Result ? new ValueTask() :
                        mode == CtorMode.Task ? new ValueTask(sync ? Task.CompletedTask : Task.Delay(1)) :
                        new ValueTask(sync ? ManualResetValueTaskSource.Completed(0, null) : ManualResetValueTaskSource.Delay(42, 0, null));

                    var mres = new ManualResetEventSlim();
                    t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().OnCompleted(() => mres.Set());
                    Assert.True(mres.Wait(10000));
                    Assert.Equal(continueOnCapturedContext ? 1 : 0, tsc.Posts);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });
        }

        [Theory]
        [InlineData(CtorMode.Task, true, false)]
        [InlineData(CtorMode.ValueTaskSource, true, false)]
        [InlineData(CtorMode.Task, false, false)]
        [InlineData(CtorMode.ValueTaskSource, false, false)]
        [InlineData(CtorMode.Result, true, true)]
        [InlineData(CtorMode.Task, true, true)]
        [InlineData(CtorMode.ValueTaskSource, true, true)]
        [InlineData(CtorMode.Result, false, true)]
        [InlineData(CtorMode.Task, false, true)]
        [InlineData(CtorMode.ValueTaskSource, false, true)]
        public async Task Generic_ConfiguredAwaiter_ContinuesOnCapturedContext(CtorMode mode, bool continueOnCapturedContext, bool sync)
        {
            await Task.Run(() =>
            {
                var tsc = new TrackingSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(tsc);
                try
                {
                    ValueTask<int> t =
                        mode == CtorMode.Result ? new ValueTask<int>(42) :
                        mode == CtorMode.Task ? new ValueTask<int>(sync ? Task.FromResult(42) : Task.Delay(1).ContinueWith(_ => 42)) :
                        new ValueTask<int>(sync ? ManualResetValueTaskSource.Completed(42, null) : ManualResetValueTaskSource.Delay(1, 42, null));

                    var mres = new ManualResetEventSlim();
                    t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().OnCompleted(() => mres.Set());
                    Assert.True(mres.Wait(10000));
                    Assert.Equal(continueOnCapturedContext ? 1 : 0, tsc.Posts);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            });
        }

        [Fact]
        public void NonGeneric_GetHashCode_FromDefault_0()
        {
            Assert.Equal(0, new ValueTask().GetHashCode());
        }

        [Fact]
        public void Generic_GetHashCode_FromResult_ContainsResult()
        {
            var vt = new ValueTask<int>(42);
            Assert.Equal(vt.Result.GetHashCode(), vt.GetHashCode());

            var rt = new ValueTask<string>((string)null);
            Assert.Equal(0, rt.GetHashCode());
            rt = new ValueTask<string>("12345");
            Assert.Equal(rt.Result.GetHashCode(), rt.GetHashCode());
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void NonGeneric_GetHashCode_FromObject_MatchesObjectHashCode(CtorMode mode)
        {
            object obj;
            ValueTask vt;
            if (mode == CtorMode.Task)
            {
                Task t = Task.CompletedTask;
                vt = new ValueTask(t);
                obj = t;
            }
            else
            {
                var t = ManualResetValueTaskSource.Completed(42, null);
                vt = new ValueTask(t);
                obj = t;
            }

            Assert.Equal(obj.GetHashCode(), vt.GetHashCode());
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.ValueTaskSource)]
        public void Generic_GetHashCode_FromObject_MatchesObjectHashCode(CtorMode mode)
        {
            object obj;
            ValueTask<int> vt;
            if (mode == CtorMode.Task)
            {
                Task<int> t = Task.FromResult(42);
                vt = new ValueTask<int>(t);
                obj = t;
            }
            else
            {
                ManualResetValueTaskSource<int> t = ManualResetValueTaskSource.Completed(42, null);
                vt = new ValueTask<int>(t);
                obj = t;
            }

            Assert.Equal(obj.GetHashCode(), vt.GetHashCode());
        }

        [Fact]
        public void NonGeneric_OperatorEquals()
        {
            var completedTcs = new TaskCompletionSource<int>();
            completedTcs.SetResult(42);

            var completedVts = ManualResetValueTaskSource.Completed(42, null);

            Assert.True(new ValueTask() == new ValueTask());
            Assert.True(new ValueTask(Task.CompletedTask) == new ValueTask(Task.CompletedTask));
            Assert.True(new ValueTask(completedTcs.Task) == new ValueTask(completedTcs.Task));
            Assert.True(new ValueTask(completedVts) == new ValueTask(completedVts));

            Assert.False(new ValueTask(Task.CompletedTask) == new ValueTask(completedTcs.Task));
            Assert.False(new ValueTask(Task.CompletedTask) == new ValueTask(completedVts));
            Assert.False(new ValueTask(completedTcs.Task) == new ValueTask(completedVts));
        }

        [Fact]
        public void Generic_OperatorEquals()
        {
            var completedTask = Task.FromResult(42);
            var completedVts = ManualResetValueTaskSource.Completed(42, null);

            Assert.True(new ValueTask<int>(42) == new ValueTask<int>(42));
            Assert.True(new ValueTask<int>(completedTask) == new ValueTask<int>(completedTask));
            Assert.True(new ValueTask<int>(completedVts) == new ValueTask<int>(completedVts));

            Assert.True(new ValueTask<string>("42") == new ValueTask<string>("42"));
            Assert.True(new ValueTask<string>((string)null) == new ValueTask<string>((string)null));

            Assert.False(new ValueTask<int>(42) == new ValueTask<int>(43));
            Assert.False(new ValueTask<string>("42") == new ValueTask<string>((string)null));
            Assert.False(new ValueTask<string>((string)null) == new ValueTask<string>("42"));

            Assert.False(new ValueTask<int>(42) == new ValueTask<int>(Task.FromResult(42)));
            Assert.False(new ValueTask<int>(Task.FromResult(42)) == new ValueTask<int>(42));
            Assert.False(new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null)) == new ValueTask<int>(42));
            Assert.False(new ValueTask<int>(completedTask) == new ValueTask<int>(completedVts));
        }

        [Fact]
        public void NonGeneric_OperatorNotEquals()
        {
            var completedTcs = new TaskCompletionSource<int>();
            completedTcs.SetResult(42);

            var completedVts = ManualResetValueTaskSource.Completed(42, null);

            Assert.False(new ValueTask() != new ValueTask());
            Assert.False(new ValueTask(Task.CompletedTask) != new ValueTask(Task.CompletedTask));
            Assert.False(new ValueTask(completedTcs.Task) != new ValueTask(completedTcs.Task));
            Assert.False(new ValueTask(completedVts) != new ValueTask(completedVts));

            Assert.True(new ValueTask(Task.CompletedTask) != new ValueTask(completedTcs.Task));
            Assert.True(new ValueTask(Task.CompletedTask) != new ValueTask(completedVts));
            Assert.True(new ValueTask(completedTcs.Task) != new ValueTask(completedVts));
        }

        [Fact]
        public void Generic_OperatorNotEquals()
        {
            var completedTask = Task.FromResult(42);
            var completedVts = ManualResetValueTaskSource.Completed(42, null);

            Assert.False(new ValueTask<int>(42) != new ValueTask<int>(42));
            Assert.False(new ValueTask<int>(completedTask) != new ValueTask<int>(completedTask));
            Assert.False(new ValueTask<int>(completedVts) != new ValueTask<int>(completedVts));

            Assert.False(new ValueTask<string>("42") != new ValueTask<string>("42"));
            Assert.False(new ValueTask<string>((string)null) != new ValueTask<string>((string)null));

            Assert.True(new ValueTask<int>(42) != new ValueTask<int>(43));
            Assert.True(new ValueTask<string>("42") != new ValueTask<string>((string)null));
            Assert.True(new ValueTask<string>((string)null) != new ValueTask<string>("42"));

            Assert.True(new ValueTask<int>(42) != new ValueTask<int>(Task.FromResult(42)));
            Assert.True(new ValueTask<int>(Task.FromResult(42)) != new ValueTask<int>(42));
            Assert.True(new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null)) != new ValueTask<int>(42));
            Assert.True(new ValueTask<int>(completedTask) != new ValueTask<int>(completedVts));
        }

        [Fact]
        public void NonGeneric_Equals_ValueTask()
        {
            Assert.True(new ValueTask().Equals(new ValueTask()));

            Assert.False(new ValueTask().Equals(new ValueTask(Task.CompletedTask)));
            Assert.False(new ValueTask(Task.CompletedTask).Equals(new ValueTask()));
            Assert.False(new ValueTask(ManualResetValueTaskSource.Completed(42, null)).Equals(new ValueTask()));
            Assert.False(new ValueTask().Equals(new ValueTask(ManualResetValueTaskSource.Completed(42, null))));
            Assert.False(new ValueTask(Task.CompletedTask).Equals(new ValueTask(ManualResetValueTaskSource.Completed(42, null))));
            Assert.False(new ValueTask(ManualResetValueTaskSource.Completed(42, null)).Equals(new ValueTask(Task.CompletedTask)));
        }

        [Fact]
        public void Generic_Equals_ValueTask()
        {
            Assert.True(new ValueTask<int>(42).Equals(new ValueTask<int>(42)));
            Assert.False(new ValueTask<int>(42).Equals(new ValueTask<int>(43)));

            Assert.True(new ValueTask<string>("42").Equals(new ValueTask<string>("42")));
            Assert.True(new ValueTask<string>((string)null).Equals(new ValueTask<string>((string)null)));

            Assert.False(new ValueTask<string>("42").Equals(new ValueTask<string>((string)null)));
            Assert.False(new ValueTask<string>((string)null).Equals(new ValueTask<string>("42")));

            Assert.False(new ValueTask<int>(42).Equals(new ValueTask<int>(Task.FromResult(42))));
            Assert.False(new ValueTask<int>(Task.FromResult(42)).Equals(new ValueTask<int>(42)));
            Assert.False(new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null)).Equals(new ValueTask<int>(42)));
        }

        [Fact]
        public void NonGeneric_Equals_Object()
        {
            Assert.True(new ValueTask().Equals((object)new ValueTask()));

            Assert.False(new ValueTask().Equals((object)new ValueTask(Task.CompletedTask)));
            Assert.False(new ValueTask(Task.CompletedTask).Equals((object)new ValueTask()));
            Assert.False(new ValueTask(ManualResetValueTaskSource.Completed(42, null)).Equals((object)new ValueTask()));
            Assert.False(new ValueTask().Equals((object)new ValueTask(ManualResetValueTaskSource.Completed(42, null))));
            Assert.False(new ValueTask(Task.CompletedTask).Equals((object)new ValueTask(ManualResetValueTaskSource.Completed(42, null))));
            Assert.False(new ValueTask(ManualResetValueTaskSource.Completed(42, null)).Equals((object)new ValueTask(Task.CompletedTask)));

            Assert.False(new ValueTask().Equals(null));
            Assert.False(new ValueTask().Equals("12345"));
            Assert.False(new ValueTask(Task.CompletedTask).Equals("12345"));
            Assert.False(new ValueTask(ManualResetValueTaskSource.Completed(42, null)).Equals("12345"));
        }

        [Fact]
        public void Generic_Equals_Object()
        {
            Assert.True(new ValueTask<int>(42).Equals((object)new ValueTask<int>(42)));
            Assert.False(new ValueTask<int>(42).Equals((object)new ValueTask<int>(43)));

            Assert.True(new ValueTask<string>("42").Equals((object)new ValueTask<string>("42")));
            Assert.True(new ValueTask<string>((string)null).Equals((object)new ValueTask<string>((string)null)));

            Assert.False(new ValueTask<string>("42").Equals((object)new ValueTask<string>((string)null)));
            Assert.False(new ValueTask<string>((string)null).Equals((object)new ValueTask<string>("42")));

            Assert.False(new ValueTask<int>(42).Equals((object)new ValueTask<int>(Task.FromResult(42))));
            Assert.False(new ValueTask<int>(Task.FromResult(42)).Equals((object)new ValueTask<int>(42)));
            Assert.False(new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null)).Equals((object)new ValueTask<int>(42)));

            Assert.False(new ValueTask<int>(42).Equals((object)null));
            Assert.False(new ValueTask<int>(42).Equals(new object()));
            Assert.False(new ValueTask<int>(42).Equals((object)42));
        }

        [Fact]
        public void NonGeneric_ToString_Success()
        {
            Assert.Equal("System.Threading.Tasks.ValueTask", new ValueTask().ToString());
            Assert.Equal("System.Threading.Tasks.ValueTask", new ValueTask(Task.CompletedTask).ToString());
            Assert.Equal("System.Threading.Tasks.ValueTask", new ValueTask(ManualResetValueTaskSource.Completed(42, null)).ToString());
        }

        [Fact]
        public void Generic_ToString_Success()
        {
            Assert.Equal("Hello", new ValueTask<string>("Hello").ToString());
            Assert.Equal("Hello", new ValueTask<string>(Task.FromResult("Hello")).ToString());
            Assert.Equal("Hello", new ValueTask<string>(ManualResetValueTaskSource.Completed("Hello", null)).ToString());

            Assert.Equal("42", new ValueTask<int>(42).ToString());
            Assert.Equal("42", new ValueTask<int>(Task.FromResult(42)).ToString());
            Assert.Equal("42", new ValueTask<int>(ManualResetValueTaskSource.Completed(42, null)).ToString());

            Assert.Same(string.Empty, new ValueTask<string>(string.Empty).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(Task.FromResult(string.Empty)).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(ManualResetValueTaskSource.Completed(string.Empty, null)).ToString());

            Assert.Same(string.Empty, new ValueTask<string>(Task.FromException<string>(new InvalidOperationException())).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(Task.FromException<string>(new OperationCanceledException())).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(ManualResetValueTaskSource.Completed<string>(null, new InvalidOperationException())).ToString());

            Assert.Same(string.Empty, new ValueTask<string>(Task.FromCanceled<string>(new CancellationToken(true))).ToString());

            Assert.Equal("0", default(ValueTask<int>).ToString());
            Assert.Same(string.Empty, default(ValueTask<string>).ToString());
            Assert.Same(string.Empty, new ValueTask<string>((string)null).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(Task.FromResult<string>(null)).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(ManualResetValueTaskSource.Completed<string>(null, null)).ToString());

            Assert.Same(string.Empty, new ValueTask<DateTime>(new TaskCompletionSource<DateTime>().Task).ToString());
        }

        [Theory]
        [InlineData(typeof(ValueTask))]
        public void NonGeneric_AsyncMethodBuilderAttribute_ValueTaskAttributed(Type valueTaskType)
        {
            CustomAttributeData cad = valueTaskType.GetTypeInfo().CustomAttributes.Single(attr => attr.AttributeType == typeof(AsyncMethodBuilderAttribute));
            Type builderTypeCtorArg = (Type)cad.ConstructorArguments[0].Value;
            Assert.Equal(typeof(AsyncValueTaskMethodBuilder), builderTypeCtorArg);

            AsyncMethodBuilderAttribute amba = valueTaskType.GetTypeInfo().GetCustomAttribute<AsyncMethodBuilderAttribute>();
            Assert.Equal(builderTypeCtorArg, amba.BuilderType);
        }

        [Theory]
        [InlineData(typeof(ValueTask<>))]
        [InlineData(typeof(ValueTask<int>))]
        [InlineData(typeof(ValueTask<string>))]
        public void Generic_AsyncMethodBuilderAttribute_ValueTaskAttributed(Type valueTaskType)
        {
            CustomAttributeData cad = valueTaskType.GetTypeInfo().CustomAttributes.Single(attr => attr.AttributeType == typeof(AsyncMethodBuilderAttribute));
            Type builderTypeCtorArg = (Type)cad.ConstructorArguments[0].Value;
            Assert.Equal(typeof(AsyncValueTaskMethodBuilder<>), builderTypeCtorArg);

            AsyncMethodBuilderAttribute amba = valueTaskType.GetTypeInfo().GetCustomAttribute<AsyncMethodBuilderAttribute>();
            Assert.Equal(builderTypeCtorArg, amba.BuilderType);
        }

        private sealed class TrackingSynchronizationContext : SynchronizationContext
        {
            internal int Posts { get; set; }

            public override void Post(SendOrPostCallback d, object state)
            {
                Posts++;
                base.Post(d, state);
            }
        }
    }
}
