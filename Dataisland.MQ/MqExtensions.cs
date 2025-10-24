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
            IRabbitMqBusFactoryConfigurator cfg)
        {
            foreach (var pair in _maps)
            {
                var attribute = pair.Value;
                if (!string.IsNullOrWhiteSpace(attribute.Queue))
                {
                    cfg.ReceiveEndpoint(attribute.Queue, e =>
                    {
                        e.PrefetchCount = attribute.PrefetchCount;
                        e.Durable = attribute.Durable;
                        e.AutoDelete = attribute.AutoDelete;
                        e.UseMessageRetry(a =>
                            a.Interval(attribute.RetryCount, TimeSpan.FromSeconds(attribute.RetryIntervalInSeconds))
                        );
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
                });
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

                    impl.Configure(ctx, cfg);

                    cfg.ConfigureEndpoints(ctx);
                });
            });
            return services;
        }
    }
}