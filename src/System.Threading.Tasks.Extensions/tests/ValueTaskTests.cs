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
            TaskObject
        }

        [Fact]
        public void DefaultValueTask_ValueType_DefaultValue()
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
        [InlineData(CtorMode.TaskObject)]
        public void CreateFromSuccessfullyCompleted_IsCompletedSuccessfully(CtorMode mode)
        {
            ValueTask<int> t =
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(CreateCompletedTaskObject(42, null));
            Assert.True(t.IsCompleted);
            Assert.True(t.IsCompletedSuccessfully);
            Assert.False(t.IsFaulted);
            Assert.False(t.IsCanceled);
            Assert.Equal(42, t.Result);
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.TaskObject)]
        public void CreateFromNotCompleted_ThenCompleteSuccessfully(CtorMode mode)
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

                case CtorMode.TaskObject:
                    var mre = new ManualResetValueTaskObject<int>();
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

                case CtorMode.TaskObject:
                    ((ManualResetValueTaskObject<int>)completer).SetResult(42);
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
        [InlineData(CtorMode.TaskObject)]
        public void CreateFromNotCompleted_ThenFault(CtorMode mode)
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

                case CtorMode.TaskObject:
                    var mre = new ManualResetValueTaskObject<int>();
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

                case CtorMode.TaskObject:
                    ((ManualResetValueTaskObject<int>)completer).SetException(e);
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
        [InlineData(CtorMode.TaskObject)]
        public void CreateFromFaulted_IsFaulted(CtorMode mode)
        {
            InvalidOperationException e = new InvalidOperationException();
            ValueTask<int> t = mode == CtorMode.Task ? new ValueTask<int>(Task.FromException<int>(e)) : new ValueTask<int>(CreateCompletedTaskObject<int>(0, e));

            Assert.True(t.IsCompleted);
            Assert.False(t.IsCompletedSuccessfully);
            Assert.True(t.IsFaulted);
            Assert.False(t.IsCanceled);

            Assert.Same(e, Assert.Throws<InvalidOperationException>(() => t.Result));
            Assert.Same(e, Assert.Throws<InvalidOperationException>(() => t.GetAwaiter().GetResult()));
        }

        [Fact]
        public void CreateFromNullTask_Throws()
        {
            AssertExtensions.Throws<ArgumentNullException>("task", () => new ValueTask<int>((Task<int>)null));
            AssertExtensions.Throws<ArgumentNullException>("task", () => new ValueTask<string>((Task<string>)null));

            AssertExtensions.Throws<ArgumentNullException>("task", () => new ValueTask<int>((IValueTaskObject<int>)null));
            AssertExtensions.Throws<ArgumentNullException>("task", () => new ValueTask<string>((IValueTaskObject<string>)null));
        }

        [Fact]
        public void CreateFromTask_AsTaskIdempotent()
        {
            Task<int> source = Task.FromResult(42);
            ValueTask<int> t = new ValueTask<int>(source);
            Assert.Same(source, t.AsTask());
            Assert.Same(t.AsTask(), t.AsTask());
        }

        [Fact]
        public void CreateFromValue_AsTaskNotIdempotent()
        {
            ValueTask<int> t = new ValueTask<int>(42);
            Assert.NotSame(Task.FromResult(42), t.AsTask());
            Assert.NotSame(t.AsTask(), t.AsTask());
        }

        [Fact]
        public void CreateFromTaskObject_AsTaskNotIdempotent()
        {
            ValueTask<int> t = new ValueTask<int>(CreateCompletedTaskObject<int>(42, null));
            Assert.NotSame(Task.FromResult(42), t.AsTask());
            Assert.NotSame(t.AsTask(), t.AsTask());
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.TaskObject)]
        public async Task CreateFromCompleted_Await(CtorMode mode)
        {
            ValueTask<int> t =
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(CreateCompletedTaskObject(42, null));
            Assert.Equal(42, await t);
            Assert.Equal(42, await t.ConfigureAwait(false));
            Assert.Equal(42, await t.ConfigureAwait(true));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task CreateFromTask_Await_Normal(int mode)
        {
            Task<int> source = Task.Delay(1).ContinueWith(_ => 42);
            var t = new ValueTask<int>(source);
            int actual =
                mode == 0 ? await t :
                mode == 1 ? await t.ConfigureAwait(false) :
                await t.ConfigureAwait(true);
            Assert.Equal(42, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task CreateFromTaskObject_Await_Normal(int mode)
        {
            var mre = new ManualResetValueTaskObject<int>();
            ValueTask<int> t = new ValueTask<int>(mre);
            var ignored = Task.Delay(1).ContinueWith(_ => mre.SetResult(42));
            int actual =
                mode == 0 ? await t :
                mode == 1 ? await t.ConfigureAwait(false) :
                await t.ConfigureAwait(true);
            Assert.Equal(42, actual);
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.TaskObject)]
        public async Task Awaiter_OnCompleted(CtorMode mode)
        {
            // Since ValueTask implements both OnCompleted and UnsafeOnCompleted,
            // OnCompleted typically won't be used by await, so we add an explicit test
            // for it here.

            ValueTask<int> t =
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(CreateCompletedTaskObject(42, null));

            var tcs = new TaskCompletionSource<bool>();
            t.GetAwaiter().OnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result, true)]
        [InlineData(CtorMode.Task, true)]
        [InlineData(CtorMode.TaskObject, true)]
        [InlineData(CtorMode.Result, false)]
        [InlineData(CtorMode.Task, false)]
        [InlineData(CtorMode.TaskObject, false)]
        public async Task ConfiguredAwaiter_OnCompleted(CtorMode mode, bool continueOnCapturedContext)
        {
            // Since ValueTask implements both OnCompleted and UnsafeOnCompleted,
            // OnCompleted typically won't be used by await, so we add an explicit test
            // for it here.

            ValueTask<int> t =
                mode == CtorMode.Result ? new ValueTask<int>(42) :
                mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                new ValueTask<int>(CreateCompletedTaskObject(42, null));

            var tcs = new TaskCompletionSource<bool>();
            t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().OnCompleted(() => tcs.SetResult(true));
            await tcs.Task;
        }

        [Theory]
        [InlineData(CtorMode.Result)]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.TaskObject)]
        public async Task Awaiter_ContinuesOnCapturedContext(CtorMode mode)
        {
            await Task.Run(() =>
            {
                var tsc = new TrackingSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(tsc);
                try
                {
                    ValueTask<int> t =
                        mode == CtorMode.Result ? new ValueTask<int>(42) :
                        mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                        new ValueTask<int>(CreateCompletedTaskObject(42, null));

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
        [InlineData(CtorMode.Result, true)]
        [InlineData(CtorMode.Task, true)]
        [InlineData(CtorMode.TaskObject, true)]
        [InlineData(CtorMode.Result, false)]
        [InlineData(CtorMode.Task, false)]
        [InlineData(CtorMode.TaskObject, false)]
        public async Task ConfiguredAwaiter_ContinuesOnCapturedContext(CtorMode mode, bool continueOnCapturedContext)
        {
            await Task.Run(() =>
            {
                var tsc = new TrackingSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(tsc);
                try
                {
                    ValueTask<int> t =
                        mode == CtorMode.Result ? new ValueTask<int>(42) :
                        mode == CtorMode.Task ? new ValueTask<int>(Task.FromResult(42)) :
                        new ValueTask<int>(CreateCompletedTaskObject(42, null));

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
        public void GetHashCode_FromResult_ContainsResult()
        {
            ValueTask<int> t = new ValueTask<int>(42);
            Assert.Equal(t.Result.GetHashCode(), t.GetHashCode());
        }

        [Fact]
        public void GetHashCode_FromResult_ContainsNull()
        {
            ValueTask<string> t = new ValueTask<string>((string)null);
            Assert.Equal(0, t.GetHashCode());
        }

        [Theory]
        [InlineData(CtorMode.Task)]
        [InlineData(CtorMode.TaskObject)]
        public void GetHashCode_FromObject_MatchesObjectHashCode(CtorMode mode)
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
                ManualResetValueTaskObject<int> t = CreateCompletedTaskObject(42, null);
                vt = new ValueTask<int>(t);
                obj = t;
            }

            Assert.Equal(obj.GetHashCode(), vt.GetHashCode());
        }

        [Fact]
        public void OperatorEquals()
        {
            Assert.True(new ValueTask<int>(42) == new ValueTask<int>(42));
            Assert.False(new ValueTask<int>(42) == new ValueTask<int>(43));

            Assert.True(new ValueTask<string>("42") == new ValueTask<string>("42"));
            Assert.True(new ValueTask<string>((string)null) == new ValueTask<string>((string)null));

            Assert.False(new ValueTask<string>("42") == new ValueTask<string>((string)null));
            Assert.False(new ValueTask<string>((string)null) == new ValueTask<string>("42"));

            Assert.False(new ValueTask<int>(42) == new ValueTask<int>(Task.FromResult(42)));
            Assert.False(new ValueTask<int>(Task.FromResult(42)) == new ValueTask<int>(42));
            Assert.False(new ValueTask<int>(CreateCompletedTaskObject(42, null)) == new ValueTask<int>(42));
        }

        [Fact]
        public void OperatorNotEquals()
        {
            Assert.False(new ValueTask<int>(42) != new ValueTask<int>(42));
            Assert.True(new ValueTask<int>(42) != new ValueTask<int>(43));

            Assert.False(new ValueTask<string>("42") != new ValueTask<string>("42"));
            Assert.False(new ValueTask<string>((string)null) != new ValueTask<string>((string)null));

            Assert.True(new ValueTask<string>("42") != new ValueTask<string>((string)null));
            Assert.True(new ValueTask<string>((string)null) != new ValueTask<string>("42"));

            Assert.True(new ValueTask<int>(42) != new ValueTask<int>(Task.FromResult(42)));
            Assert.True(new ValueTask<int>(Task.FromResult(42)) != new ValueTask<int>(42));
            Assert.True(new ValueTask<int>(CreateCompletedTaskObject(42, null)) != new ValueTask<int>(42));
        }

        [Fact]
        public void Equals_ValueTask()
        {
            Assert.True(new ValueTask<int>(42).Equals(new ValueTask<int>(42)));
            Assert.False(new ValueTask<int>(42).Equals(new ValueTask<int>(43)));

            Assert.True(new ValueTask<string>("42").Equals(new ValueTask<string>("42")));
            Assert.True(new ValueTask<string>((string)null).Equals(new ValueTask<string>((string)null)));

            Assert.False(new ValueTask<string>("42").Equals(new ValueTask<string>((string)null)));
            Assert.False(new ValueTask<string>((string)null).Equals(new ValueTask<string>("42")));

            Assert.False(new ValueTask<int>(42).Equals(new ValueTask<int>(Task.FromResult(42))));
            Assert.False(new ValueTask<int>(Task.FromResult(42)).Equals(new ValueTask<int>(42)));
            Assert.False(new ValueTask<int>(CreateCompletedTaskObject(42, null)).Equals(new ValueTask<int>(42)));
        }

        [Fact]
        public void Equals_Object()
        {
            Assert.True(new ValueTask<int>(42).Equals((object)new ValueTask<int>(42)));
            Assert.False(new ValueTask<int>(42).Equals((object)new ValueTask<int>(43)));

            Assert.True(new ValueTask<string>("42").Equals((object)new ValueTask<string>("42")));
            Assert.True(new ValueTask<string>((string)null).Equals((object)new ValueTask<string>((string)null)));

            Assert.False(new ValueTask<string>("42").Equals((object)new ValueTask<string>((string)null)));
            Assert.False(new ValueTask<string>((string)null).Equals((object)new ValueTask<string>("42")));

            Assert.False(new ValueTask<int>(42).Equals((object)new ValueTask<int>(Task.FromResult(42))));
            Assert.False(new ValueTask<int>(Task.FromResult(42)).Equals((object)new ValueTask<int>(42)));
            Assert.False(new ValueTask<int>(CreateCompletedTaskObject(42, null)).Equals((object)new ValueTask<int>(42)));

            Assert.False(new ValueTask<int>(42).Equals((object)null));
            Assert.False(new ValueTask<int>(42).Equals(new object()));
            Assert.False(new ValueTask<int>(42).Equals((object)42));
        }

        [Fact]
        public void ToString_Success()
        {
            Assert.Equal("Hello", new ValueTask<string>("Hello").ToString());
            Assert.Equal("Hello", new ValueTask<string>(Task.FromResult("Hello")).ToString());
            Assert.Equal("Hello", new ValueTask<string>(CreateCompletedTaskObject("Hello", null)).ToString());

            Assert.Equal("42", new ValueTask<int>(42).ToString());
            Assert.Equal("42", new ValueTask<int>(Task.FromResult(42)).ToString());
            Assert.Equal("42", new ValueTask<int>(CreateCompletedTaskObject(42, null)).ToString());

            Assert.Same(string.Empty, new ValueTask<string>(string.Empty).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(Task.FromResult(string.Empty)).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(CreateCompletedTaskObject(string.Empty, null)).ToString());

            Assert.Same(string.Empty, new ValueTask<string>(Task.FromException<string>(new InvalidOperationException())).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(Task.FromException<string>(new OperationCanceledException())).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(CreateCompletedTaskObject<string>(null, new InvalidOperationException())).ToString());

            Assert.Same(string.Empty, new ValueTask<string>(Task.FromCanceled<string>(new CancellationToken(true))).ToString());

            Assert.Equal("0", default(ValueTask<int>).ToString());
            Assert.Same(string.Empty, default(ValueTask<string>).ToString());
            Assert.Same(string.Empty, new ValueTask<string>((string)null).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(Task.FromResult<string>(null)).ToString());
            Assert.Same(string.Empty, new ValueTask<string>(CreateCompletedTaskObject<string>(null, null)).ToString());

            Assert.Same(string.Empty, new ValueTask<DateTime>(new TaskCompletionSource<DateTime>().Task).ToString());
        }

        [Theory]
        [InlineData(typeof(ValueTask<>))]
        [InlineData(typeof(ValueTask<int>))]
        [InlineData(typeof(ValueTask<string>))]
        public void AsyncMethodBuilderAttribute_ValueTaskAttributed(Type valueTaskType)
        {
            CustomAttributeData cad = valueTaskType.GetTypeInfo().CustomAttributes.Single(attr => attr.AttributeType == typeof(AsyncMethodBuilderAttribute));
            Type builderTypeCtorArg = (Type)cad.ConstructorArguments[0].Value;
            Assert.Equal(typeof(AsyncValueTaskMethodBuilder<>), builderTypeCtorArg);

            AsyncMethodBuilderAttribute amba = valueTaskType.GetTypeInfo().GetCustomAttribute<AsyncMethodBuilderAttribute>();
            Assert.Equal(builderTypeCtorArg, amba.BuilderType);
        }

        private static ManualResetValueTaskObject<T> CreateCompletedTaskObject<T>(T result, Exception error)
        {
            var mre = new ManualResetValueTaskObject<T>();
            if (error != null)
            {
                mre.SetException(error);
            }
            else
            {
                mre.SetResult(result);
            }
            return mre;
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
