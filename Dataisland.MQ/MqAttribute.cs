namespace Dataisland.MQ
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class MqAttribute : Attribute
    {
        public MqAttribute(string queue)
        {
            if (string.IsNullOrWhiteSpace(queue))
                throw new ArgumentNullException(nameof(queue));

            Queue = queue;
        }

        public string Queue { get; }
        public int PrefetchCount { get; set; } = 1;
        public bool Durable { get; set; } = true;
        public bool AutoDelete { get; set; } = false;
        public int RetryCount { get; set; } = 3;
        public float RetryIntervalInSeconds { get; set; } = 3;
    }
}