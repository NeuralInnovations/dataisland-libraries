using System.Reflection;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Dataisland.MQ
{
    public interface IConfigureConsumers
    {
        IConfigureConsumers AddConsumer<T>() where T : class, IConsumer;
    }

    public class ConfigureConsumersImpl(
        IBusRegistrationConfigurator configurator
    ) : IConfigureConsumers
    {
        private readonly Dictionary<Type, MqAttribute> _maps = new();

        public void Configure(
            IBusRegistrationContext ctx,
            IRabbitMqBusFactoryConfigurator cfg,
            RabbitMqOptions options)
        {
            foreach (var pair in _maps)
            {
                var attribute = pair.Value;
                if (!string.IsNullOrWhiteSpace(attribute.Queue))
                {
                    cfg.ReceiveEndpoint(attribute.Queue, e =>
                    {
                        e.ConfigureConsumeTopology = true;
                        e.PrefetchCount = attribute.PrefetchCount;
                        if (attribute.ConcurrentMessageLimit > 0)
                            e.ConcurrentMessageLimit = attribute.ConcurrentMessageLimit;
                        e.Durable = attribute.Durable;
                        e.AutoDelete = attribute.AutoDelete;

                        var queueType = attribute.QueueType != QueueType.Inherited
                            ? attribute.QueueType
                            : options.DefaultQueueType;

                        if (queueType == QueueType.Quorum)
                        {
                            e.SetQuorumQueue();

                            var deliveryLimit = attribute.DeliveryLimit >= 0
                                ? attribute.DeliveryLimit
                                : options.DeliveryLimit;

                            if (deliveryLimit > 0)
                                e.SetQueueArgument("x-delivery-limit", deliveryLimit);
                        }

                        if (attribute.RetryCount > 0)
                            e.UseMessageRetry(a =>
                                a.Interval(attribute.RetryCount, TimeSpan.FromSeconds(attribute.RetryIntervalInSeconds))
                            );
                        if (attribute.ConsumerTimeoutInSeconds > 0)
                            e.UseTimeout(t => t.Timeout = TimeSpan.FromSeconds(attribute.ConsumerTimeoutInSeconds));
                        e.ConfigureConsumer(ctx, pair.Key);
                    });
                }
            }
        }

        public IConfigureConsumers AddConsumer<T>() where T : class, IConsumer
        {
            if (typeof(T).GetCustomAttribute(typeof(MqAttribute)) is MqAttribute attribute)
            {
                // Retry is configured on the receive endpoint (Configure method), not here,
                // to avoid compounding retries (endpoint × consumer = RetryCount²).
                var consumerDef = configurator.AddConsumer<T>();
                consumerDef.ExcludeFromConfigureEndpoints();
                _maps.Add(typeof(T), attribute);
            }
            else
            {
                configurator.AddConsumer<T>();
            }

            return this;
        }
    }

    public static class MqExtensions
    {
        public static IServiceCollection AddMqService(
            this IServiceCollection services,
            RabbitMqOptions options,
            Action<IConfigureConsumers> configure
        )
        {
            services.AddMassTransit(mt =>
            {
                var impl = new ConfigureConsumersImpl(mt);
                configure(impl);
                mt.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                    {
                        h.Username(options.Username);
                        h.Password(options.Password);
                    });

                    impl.Configure(ctx, cfg, options);

                    cfg.ConfigureEndpoints(ctx);
                });
            });
            return services;
        }
    }
}