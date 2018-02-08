// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Test.Common;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace System.Net.Http.Functional.Tests
{
    public class HttpClientHandler_CancellationTest : HttpClientTestBase
    {
        public enum CancellationMode
        {
            Token,
            CancelAllPending
        }

        public enum CancellationTiming
        {
            DuringRequestContentSend,
            DuringResponseHeadersReceive,
            DuringResponseBodyReceive
        }

        public static IEnumerable<object[]> CancellationCombinations()
        {
            foreach (CancellationMode mode in Enum.GetValues(typeof(CancellationMode)))
            {
                foreach (CancellationTiming timing in Enum.GetValues(typeof(CancellationTiming)))
                {
                    yield return new object[] { mode, timing };
                }
            }
        }

        [OuterLoop] // includes seconds of delay
        [Theory]
        [MemberData(nameof(CancellationCombinations))]
        [ActiveIssue("dotnet/corefx #20010", TargetFrameworkMonikers.Uap)]
        [ActiveIssue("dotnet/corefx #19038", TargetFrameworkMonikers.NetFramework)]
        public async Task PostAsync_CancelDuringOperation_TaskCanceledQuickly(CancellationMode mode, CancellationTiming timing)
        {
            using (HttpClient client = CreateHttpClient())
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                var cts = new CancellationTokenSource();
                CancellationToken cancellationToken = mode == CancellationMode.Token ? cts.Token : CancellationToken.None;

                var triggerResponseWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var triggerRequestCancel = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                await LoopbackServer.CreateServerAsync(async (server, url) =>
                {
                    Task serverTask = LoopbackServer.AcceptSocketAsync(server, async (socket, stream, reader, writer) =>
                    {
                        while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) ;
                        await writer.WriteAsync(
                            "HTTP/1.1 200 OK\r\n" +
                            $"Date: {DateTimeOffset.UtcNow:R}\r\n" +
                            "Content-Length: 16000\r\n" +
                            "\r\n" +
                            (startResponseBody ? "less than 16000 bytes" : ""));

                        await Task.Delay(1000);
                        triggerRequestCancel.SetResult(true); // allow request to cancel
                        await triggerResponseWrite.Task; // pause until we're released

                        return null;
                    });

                    var stopwatch = Stopwatch.StartNew();
                    Task t = new Func<Task>(async () =>
                    {
                        Task<HttpResponseMessage> getResponse = client.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken);
                        await triggerRequestCancel.Task;
                        cts.Cancel();
                        await getResponse;
                    })();
                    if (PlatformDetection.IsFullFramework)
                    {

                        // .NET Framework throws WebException instead of OperationCanceledException.
                        await Assert.ThrowsAnyAsync<WebException>(() => t);
                    }
                    else
                    {
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t);
                    }
                    stopwatch.Stop();

                    triggerResponseWrite.SetResult(true);
                    Assert.True(stopwatch.Elapsed < new TimeSpan(0, 0, 30), $"Elapsed time {stopwatch.Elapsed} should be less than 30 seconds, was {stopwatch.Elapsed.TotalSeconds}");
                });
            }
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework, "dotnet/corefx #18864")] // Hangs on NETFX
        [ActiveIssue(9075, TestPlatforms.AnyUnix)] // recombine this test into the subsequent one when issue is fixed
        [OuterLoop] // includes seconds of delay
        [Fact]
        public Task ReadAsStreamAsync_ReadAsync_Cancel_BodyNeverStarted_TaskCanceledQuickly()
        {
            return ReadAsStreamAsync_ReadAsync_Cancel_TaskCanceledQuickly(false);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework, "dotnet/corefx #18864")] // Hangs on NETFX
        [OuterLoop] // includes seconds of delay
        [Theory]
        [InlineData(true)]
        public async Task ReadAsStreamAsync_ReadAsync_Cancel_TaskCanceledQuickly(bool startResponseBody)
        {
            if (UseManagedHandler)
            {
                return;
            }

            using (HttpClient client = CreateHttpClient())
            {
                await LoopbackServer.CreateServerAsync(async (server, url) =>
                {
                    var triggerResponseWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    Task serverTask = LoopbackServer.AcceptSocketAsync(server, async (socket, stream, reader, writer) =>
                    {
                        while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) ;
                        await writer.WriteAsync(
                            "HTTP/1.1 200 OK\r\n" +
                            $"Date: {DateTimeOffset.UtcNow:R}\r\n" +
                            "Content-Length: 16000\r\n" +
                            "\r\n" +
                            (startResponseBody ? "20 bytes of the body" : ""));

                        await triggerResponseWrite.Task; // pause until we're released
                        
                        return null;
                    });

                    using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                    {
                        // Read all expected content
                        byte[] buffer = new byte[20];
                        if (startResponseBody)
                        {
                            int totalRead = 0;
                            int bytesRead;
                            while (totalRead < 20 && (bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                totalRead += bytesRead;
                            }
                        }

                        // Now do a read that'll need to be canceled
                        var stopwatch = Stopwatch.StartNew();
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(
                            () => responseStream.ReadAsync(buffer, 0, buffer.Length, new CancellationTokenSource(1000).Token));
                        stopwatch.Stop();

                        triggerResponseWrite.SetResult(true);
                        Assert.True(stopwatch.Elapsed < new TimeSpan(0, 0, 30), $"Elapsed time {stopwatch.Elapsed} should be less than 30 seconds, was {stopwatch.Elapsed.TotalSeconds}");
                    }
                });
            }
        }
    }
}
