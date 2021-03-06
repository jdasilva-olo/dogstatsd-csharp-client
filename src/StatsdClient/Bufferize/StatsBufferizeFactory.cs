using System;
using System.Net;
using Mono.Unix;
using StatsdClient.Transport;

namespace StatsdClient.Bufferize
{
    internal class StatsBufferizeFactory : IStatsBufferizeFactory
    {
        public StatsBufferize CreateStatsBufferize(
            BufferBuilder bufferBuilder,
            int workerMaxItemCount,
            TimeSpan? blockingQueueTimeout,
            TimeSpan maxIdleWaitBeforeSending)
        {
            return new StatsBufferize(
                bufferBuilder,
                workerMaxItemCount,
                blockingQueueTimeout,
                maxIdleWaitBeforeSending);
        }

        public ITransport CreateUDPTransport(IPEndPoint endPoint)
        {
            return new UDPTransport(endPoint);
        }

        public ITransport CreateUnixDomainSocketTransport(
            UnixEndPoint endPoint,
            TimeSpan? udsBufferFullBlockDuration)
        {
            return new UnixDomainSocketTransport(endPoint, udsBufferFullBlockDuration);
        }

        public Telemetry CreateTelemetry(string assemblyVersion, TimeSpan flushInterval, ITransport transport, string[] globalTags)
        {
            return new Telemetry(assemblyVersion, flushInterval, transport, globalTags);
        }

        public ITransport CreateNamedPipeTransport(string pipeName)
        {
#if NAMED_PIPE_AVAILABLE
            return new NamedPipeTransport(pipeName);
#else
            throw new NotSupportedException("Named pipes are not supported on this .NET framework.");
#endif
        }
    }
}