using Durandal.Common.Audio;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Prototype
{
    public class RemotingStutterRepro
    {
        public static async Task Run()
        {
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
            // Create two anonymous pipes for full-duplex communication
            AnonymousPipeServerStream readPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable, 1024 * 1024);
            AnonymousPipeServerStream writePipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable, 1024 * 1024);

            // Create a remote domain and then an echo server proxy inside that domain
            AppDomainSetup setup = new AppDomainSetup();
            AppDomain domain = AppDomain.CreateDomain("EchoTest", null, setup);
            ObjectHandle remoteHandle = domain.CreateInstanceFrom(typeof(EchoServer).Assembly.Location, typeof(EchoServer).FullName);
            EchoServer remoteServerProxy = (EchoServer)remoteHandle.Unwrap();
            remoteServerProxy.Run(readPipeId: long.Parse(writePipe.GetClientHandleAsString()), writePipeId: long.Parse(readPipe.GetClientHandleAsString()));
            AppDomainContainerSponsor containerSponsor = new AppDomainContainerSponsor(remoteServerProxy);
            Stopwatch timer = Stopwatch.StartNew();
            byte[] buf = new byte[4096];
            Random rand = new Random();

            DateTimeOffset lastUpdateTime = DateTimeOffset.UtcNow;
            RateLimiter limiter = new RateLimiter(100, 100);
            RateCounter counter = new RateCounter(TimeSpan.FromSeconds(5));
            MovingPercentile percentile = new MovingPercentile(100000, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.9999);
            rand.NextBytes(buf); // Make a random message
            GarbageGenerator garbage = new GarbageGenerator();

            while (true)
            {
                limiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                await Task.Run(async () =>
                {
                    garbage.GenerateGarbage(10000);
                    await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                });

                using (IDisposable finalizable = new BufferedChannel<int>())
                {
                    timer.Restart();
                    await writePipe.WriteAsync(buf, 0, buf.Length); // Send it across the pipe
                    await writePipe.FlushAsync();
                    int totalRead = 0; // Then reliably read the echoed response
                    do { totalRead += await readPipe.ReadAsync(buf, totalRead, buf.Length); }
                    while (totalRead < buf.Length);
                    timer.Stop();

                    counter.Increment();
                    percentile.Add(timer.ElapsedMillisecondsPrecise());
                    if (DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(5)) > lastUpdateTime)
                    {
                        StringBuilder s = new StringBuilder();
                        s.Append("RATE:");
                        s.Append(counter.Rate.ToString("F2"));
                        s.Append(" PERCENTILES: ");
                        foreach (var perc in percentile.GetPercentiles())
                        {
                            s.Append(perc.Item2.ToString("F3"));
                            s.Append("  ");
                        }

                        Console.WriteLine(s.ToString());
                        lastUpdateTime = DateTimeOffset.UtcNow;
                    }
                }
            }
        }

        private class EchoServer : MarshalByRefObject
        {
            private GarbageGenerator _garbage = new GarbageGenerator();

            public void Run(long readPipeId, long writePipeId)
            {
                AnonymousPipeClientStream readPipe = new AnonymousPipeClientStream(PipeDirection.In, new SafePipeHandle(new IntPtr(readPipeId), false));
                AnonymousPipeClientStream writePipe = new AnonymousPipeClientStream(PipeDirection.Out, new SafePipeHandle(new IntPtr(writePipeId), false));
                Task.Run(async () =>
                {
                    Random rand = new Random();
                    byte[] buf = new byte[4096];
                    while (true)
                    {
                        await Task.Run(() =>
                        {
                            _garbage.GenerateGarbage(10000);
                        });
                        using (IDisposable finalizable = new BufferedChannel<int>())
                        {
                            int totalRead = 0; // Read the whole message (4kb)
                            do { totalRead += await readPipe.ReadAsync(buf, totalRead, buf.Length); }
                            while (totalRead < buf.Length);
                            await writePipe.WriteAsync(buf, 0, totalRead); // Then echo it back
                            await writePipe.FlushAsync();
                        }
                    }
                });
            }
        }

        /// <summary>
        /// ISponsor object used to maintain memory references across app domains to prevent garbage collection from interfering
        /// </summary>
        private class AppDomainContainerSponsor : MarshalByRefObject, ISponsor, IDisposable
        {
            private ILease _lease;

            public AppDomainContainerSponsor(MarshalByRefObject mbro)
            {
                _lease = (ILease)RemotingServices.GetLifetimeService(mbro);
                _lease.Register(this);
            }

            public TimeSpan Renewal(ILease lease)
            {
                return _lease != null ? lease.InitialLeaseTime : TimeSpan.Zero;
            }

            public void Dispose()
            {
                if (_lease != null)
                {
                    _lease.Unregister(this);
                    _lease = null;
                }
            }

            public override object InitializeLifetimeService()
            {
                return null;
            }
        }
    }
}
