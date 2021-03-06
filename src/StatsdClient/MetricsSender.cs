﻿using System;
using StatsdClient.Bufferize;

namespace StatsdClient
{
    internal class MetricsSender
    {
        private readonly Telemetry _optionalTelemetry;
        private readonly StatsBufferize _statsBufferize;
        private readonly Serializers _serializers;
        private readonly bool _truncateIfTooLong;
        private readonly IStopWatchFactory _stopwatchFactory;
        private readonly IRandomGenerator _randomGenerator;

        internal MetricsSender(
                    StatsBufferize statsBufferize,
                    IRandomGenerator randomGenerator,
                    IStopWatchFactory stopwatchFactory,
                    Serializers serializers,
                    Telemetry optionalTelemetry,
                    bool truncateIfTooLong)
        {
            _stopwatchFactory = stopwatchFactory;
            _statsBufferize = statsBufferize;
            _randomGenerator = randomGenerator;
            _optionalTelemetry = optionalTelemetry;
            _serializers = serializers;
            _truncateIfTooLong = truncateIfTooLong;
        }

        public void SendEvent(string title, string text, string alertType = null, string aggregationKey = null, string sourceType = null, int? dateHappened = null, string priority = null, string hostname = null, string[] tags = null, bool truncateIfTooLong = false)
        {
            truncateIfTooLong = truncateIfTooLong || _truncateIfTooLong;
            var optionalSerializedMetric = _serializers.EventSerializer.Serialize(title, text, alertType, aggregationKey, sourceType, dateHappened, priority, hostname, tags, truncateIfTooLong);
            Send(optionalSerializedMetric, () => _optionalTelemetry?.OnEventSent());
        }

        public void SendServiceCheck(string name, int status, int? timestamp = null, string hostname = null, string[] tags = null, string serviceCheckMessage = null, bool truncateIfTooLong = false)
        {
            truncateIfTooLong = truncateIfTooLong || _truncateIfTooLong;
            var optionalSerializedMetric = _serializers.ServiceCheckSerializer.Serialize(name, status, timestamp, hostname, tags, serviceCheckMessage, truncateIfTooLong);
            Send(optionalSerializedMetric, () => _optionalTelemetry?.OnServiceCheckSent());
        }

        public void SendMetric<T>(MetricType metricType, string name, T value, double sampleRate = 1.0, string[] tags = null)
        {
            if (_randomGenerator.ShouldSend(sampleRate))
            {
                var optionalSerializedMetric = _serializers.MetricSerializer.Serialize(metricType, name, value, sampleRate, tags);
                Send(optionalSerializedMetric, () => _optionalTelemetry?.OnMetricSent());
            }
        }

        public void Send(Action actionToTime, string statName, double sampleRate = 1.0, string[] tags = null)
        {
            var stopwatch = _stopwatchFactory.Get();

            try
            {
                stopwatch.Start();
                actionToTime();
            }
            finally
            {
                stopwatch.Stop();
                SendMetric(MetricType.Timing, statName, stopwatch.ElapsedMilliseconds(), sampleRate, tags);
            }
        }

        private void Send(SerializedMetric optionalSerializedMetric, Action onSuccess)
        {
            if (optionalSerializedMetric != null && _statsBufferize.Send(optionalSerializedMetric))
            {
                onSuccess();
            }
            else
            {
                _optionalTelemetry?.OnPacketsDroppedQueue();
            }
        }
    }
}
