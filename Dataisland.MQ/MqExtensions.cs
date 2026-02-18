using System.Reflection;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            IRabbitMqBusFactoryConfigurator cfg)
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

                        if (attribute.UseDelayedRedelivery && attribute.RedeliveryIntervalsSeconds.Length > 0)
                        {
                            e.UseDelayedRedelivery(r => r.Intervals(
                                attribute.RedeliveryIntervalsSeconds.Select(s => TimeSpan.FromSeconds(s)).ToArray()
                            ));
                        }

                        e.UseMessageRetry(a =>
                            a.Interval(attribute.RetryCount, TimeSpan.FromSeconds(attribute.RetryIntervalInSeconds))
                        );
                        if (attribute.ConsumerTimeoutInSeconds > 0)
                            e.UseTimeout(t => t.Timeout = TimeSpan.FromSeconds(attribute.ConsumerTimeoutInSeconds));

                        e.UseInMemoryOutbox(ctx);

                        e.ConfigureConsumer(ctx, pair.Key);
                    });
                }
            }
        }

        public IConfigureConsumers AddConsumer<T>() where T : class, IConsumer
        {
            if (typeof(T).GetCustomAttribute(typeof(MqAttribute)) is MqAttribute attribute)
            {
                configurator.AddConsumer<T>(c =>
                {
                    c.UseMessageRetry(a =>
                        a.Interval(attribute.RetryCount,
                            TimeSpan.FromSeconds(attribute.RetryIntervalInSeconds)
                        )
                    );
                }).ExcludeFromConfigureEndpoints();
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
                    cfg.Host(options.Host, options.VirtualHost, h =>
                    {
                        h.Username(options.Username);
                        h.Password(options.Password);
                    });

                    var loggerFactory = ctx.GetRequiredService<ILoggerFactory>();
                    cfg.ConnectReceiveObserver(
                        new ErrorQueueObserver(loggerFactory.CreateLogger<ErrorQueueObserver>()));

                    impl.Configure(ctx, cfg);

                    cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(false));
                });
            });
            return services;
        }
    }
}
