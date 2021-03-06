using System;
using System.Threading.Tasks;
using StatsdClient.Worker;

namespace StatsdClient.Bufferize
{
    /// <summary>
    /// StatsBufferize bufferizes metrics before sending them.
    /// </summary>
    internal class StatsBufferize : IDisposable
    {
        private readonly AsynchronousWorker<SerializedMetric> _worker;

        public StatsBufferize(
            BufferBuilder bufferBuilder,
            int workerMaxItemCount,
            TimeSpan? blockingQueueTimeout,
            TimeSpan maxIdleWaitBeforeSending)
        {
            var handler = new WorkerHandler(bufferBuilder, maxIdleWaitBeforeSending);

            // `handler` (and also `bufferBuilder`) do not need to be thread safe as long as workerMaxItemCount is 1.
            this._worker = new AsynchronousWorker<SerializedMetric>(
                handler,
                new Waiter(),
                1,
                workerMaxItemCount,
                blockingQueueTimeout);
        }

        public bool Send(SerializedMetric serializedMetric)
        {
            if (!this._worker.TryEnqueue(serializedMetric))
            {
                serializedMetric.Dispose();
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            this._worker.Dispose();
        }

        private class WorkerHandler : IAsynchronousWorkerHandler<SerializedMetric>
        {
            private readonly BufferBuilder _bufferBuilder;
            private readonly TimeSpan _maxIdleWaitBeforeSending;
            private System.Diagnostics.Stopwatch _stopwatch;

            public WorkerHandler(BufferBuilder bufferBuilder, TimeSpan maxIdleWaitBeforeSending)
            {
                _bufferBuilder = bufferBuilder;
                _maxIdleWaitBeforeSending = maxIdleWaitBeforeSending;
            }

            public void OnNewValue(SerializedMetric serializedMetric)
            {
                using (serializedMetric)
                {
                    if (!_bufferBuilder.Add(serializedMetric))
                    {
                        throw new InvalidOperationException($"The metric size exceeds the buffer capacity: {serializedMetric.ToString()}");
                    }

                    _stopwatch = null;
                }
            }

            public bool OnIdle()
            {
                if (_stopwatch == null)
                {
                    _stopwatch = System.Diagnostics.Stopwatch.StartNew();
                }

                if (_stopwatch.ElapsedMilliseconds > _maxIdleWaitBeforeSending.TotalMilliseconds)
                {
                    this._bufferBuilder.HandleBufferAndReset();

                    return true;
                }

                return true;
            }

            public void OnShutdown()
            {
                this._bufferBuilder.HandleBufferAndReset();
            }
        }
    }
}
