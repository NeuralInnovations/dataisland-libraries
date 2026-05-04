namespace Dataisland.MQ
{
    [Serializable]
    public class RabbitMqOptions
    {
        public string Host { get; set; } = null!;
        public ushort Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public QueueType DefaultQueueType { get; set; } = QueueType.Classic;
        public int DeliveryLimit { get; set; } = 0;
    }
}