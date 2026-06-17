namespace Dataisland.MQ
{
    [Serializable]
    public class RabbitMqOptions
    {
        public string? ConnectionString { get; set; }
        public string Host { get; set; } = null!;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public QueueType DefaultQueueType { get; set; } = QueueType.Classic;
        public int DeliveryLimit { get; set; } = 0;

        /// <summary>
        /// Enable MassTransit's <c>UseDelayedRedelivery</c> feature, which requires the
        /// <c>rabbitmq_delayed_message_exchange</c> plugin on the broker. When the plugin is
        /// missing the AMQP channel gets closed with PRECONDITION_FAILED on first use, which
        /// cascades into T-FAULT errors per message. Leave false when you can't install the
        /// plugin; the per-consumer <c>RetryCount</c> / <c>RetryIntervalInSeconds</c> provides
        /// in-memory retries regardless. Set via <c>RabbitMq__DelayedRedeliveryEnabled=true</c>.
        /// </summary>
        public bool DelayedRedeliveryEnabled { get; set; } = false;

        /// <summary>
        /// Optional upper bound applied to every consumer's PrefetchCount and ConcurrentMessageLimit
        /// (clamped down, never up). For small single-node customers whose CPU-bound embeddings
        /// sidecar can't sustain the default per-consumer concurrency (e.g. process-medical-case at
        /// 10), set this low (e.g. 3) to avoid thrashing the sidecar with 503s. 0 = no cap (default;
        /// large/multi-replica deployments keep the per-consumer attribute values).
        /// Set via <c>RabbitMq__ConsumerConcurrencyCap=3</c>.
        /// </summary>
        public int ConsumerConcurrencyCap { get; set; } = 0;

        public void ApplyConnectionString()
        {
            if (string.IsNullOrEmpty(ConnectionString)) return;

            var uri = new Uri(ConnectionString);
            Host = uri.Port > 0 && uri.Port != 5672
                ? $"{uri.Host}:{uri.Port}"
                : uri.Host;
            VirtualHost = uri.AbsolutePath == "/" || string.IsNullOrEmpty(uri.AbsolutePath)
                ? "/"
                : Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':');
                Username = Uri.UnescapeDataString(parts[0]);
                Password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            }
        }
    }
}
