using Prometheus;

namespace Dataisland.MQ;

public static class ConsumerMetrics
{
    private static readonly string[] Labels = ["consumer", "message_type"];

    public static readonly Histogram ProcessingDurationSeconds = Metrics.CreateHistogram(
        "mq_consumer_duration_seconds",
        "Duration of MQ consumer message processing in seconds",
        new HistogramConfiguration
        {
            LabelNames = Labels,
            Buckets = [0.1, 0.5, 1, 2, 5, 10, 30, 60, 120, 300]
        });

    public static readonly Counter MessagesTotal = Metrics.CreateCounter(
        "mq_consumer_messages_total",
        "Total MQ messages processed by outcome",
        ["consumer", "message_type", "status"]);
}
