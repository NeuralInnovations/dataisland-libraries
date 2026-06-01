namespace Dataisland.MQ
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class MqAttribute : Attribute
    {
        // RabbitMQ 3.12 supports per-queue consumer ack timeout values, not per-queue "undefined".
        public const long LongRunningConsumerTimeoutInMilliseconds = 365L * 24 * 60 * 60 * 1000;

        public MqAttribute(string queue)
        {
            if (string.IsNullOrWhiteSpace(queue))
                throw new ArgumentNullException(nameof(queue));

            Queue = queue;
        }

        public string Queue { get; }
        public int PrefetchCount { get; set; } = 1;
        public int ConcurrentMessageLimit { get; set; } = 0;
        public bool Durable { get; set; } = true;
        public bool AutoDelete { get; set; } = false;
        public int RetryCount { get; set; } = 3;
        public float RetryIntervalInSeconds { get; set; } = 3;
        public int ConsumerTimeoutInSeconds { get; set; } = 0;
        /// <summary>RabbitMQ x-consumer-timeout queue argument in milliseconds. Zero leaves broker defaults intact.</summary>
        public long DeliveryAcknowledgementTimeoutInMilliseconds { get; set; } = 0;
        public QueueType QueueType { get; set; } = QueueType.Inherited;
        public int DeliveryLimit { get; set; } = -1;

        /// <summary>
        /// Enable delayed redelivery for transient failures (GPU cold starts, rate limits, etc.)
        /// </summary>
        public bool UseDelayedRedelivery { get; set; } = false;

        /// <summary>
        /// Intervals in seconds for delayed redelivery attempts.
        /// </summary>
        public int[] RedeliveryIntervalsSeconds { get; set; } = [5, 30, 120];
    }
}
