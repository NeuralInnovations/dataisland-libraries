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

                        // Clamp per-consumer concurrency to the global cap (when set). Lets a
                        // small single-node customer throttle CPU-bound consumers without
                        // touching the per-consumer attribute (which large deployments keep).
                        var cap = options.ConsumerConcurrencyCap;
                        var prefetch = cap > 0 ? Math.Min(attribute.PrefetchCount, cap) : attribute.PrefetchCount;
                        var concurrency = attribute.ConcurrentMessageLimit > 0
                            ? (cap > 0 ? Math.Min(attribute.ConcurrentMessageLimit, cap) : attribute.ConcurrentMessageLimit)
                            : (cap > 0 ? cap : 0);

                        e.PrefetchCount = prefetch;
                        if (concurrency > 0)
                            e.ConcurrentMessageLimit = concurrency;
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

                        if (attribute.DeliveryAcknowledgementTimeoutInMilliseconds > 0)
                            e.SetQueueArgument(
                                "x-consumer-timeout",
                                attribute.DeliveryAcknowledgementTimeoutInMilliseconds);

                        if (attribute.RetryCount > 0)
                            e.UseMessageRetry(a =>
                            {
                                a.Interval(attribute.RetryCount, TimeSpan.FromSeconds(attribute.RetryIntervalInSeconds));
                                // Don't retry when the consumer's cancellation token has already
                                // tripped (pod drain, KEDA scale-down, bus stop). The retry's own
                                // Task.Delay uses the same ct and throws immediately, so retries
                                // burn in microseconds and the message lands in _error anyway —
                                // just noisier. Skipping retry on OCE keeps the log clean; the
                                // raised StopTimeout on the bus gives in-flight consumers a
                                // chance to finish cleanly before the hard cancel.
                                a.Ignore<OperationCanceledException>();
                            });

                        // Only wire UseDelayedRedelivery when the broker has the
                        // rabbitmq_delayed_message_exchange plugin — without it, the first
                        // use closes the channel with PRECONDITION_FAILED and every subsequent
                        // message faults. The per-consumer attribute expresses intent; the
                        // broker-level flag gates actual activation.
                        if (options.DelayedRedeliveryEnabled
                            && attribute.UseDelayedRedelivery
                            && attribute.RedeliveryIntervalsSeconds.Length > 0)
                        {
                            e.UseDelayedRedelivery(r => r.Intervals(
                                attribute.RedeliveryIntervalsSeconds.Select(s => TimeSpan.FromSeconds(s)).ToArray()
                            ));
                        }

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
            // Default MassTransitHostOptions.StopTimeout is 30s. Medical case processing can
            // run 1-5 min; at default, a pod drain hard-cancels in-flight consumers, their
            // OCE bubbles to _error queues. Give the bus up to 5 min to drain. Pod's
            // terminationGracePeriodSeconds must be >= this for k8s to honour it.
            services.Configure<MassTransitHostOptions>(o =>
            {
                o.WaitUntilStarted = true;
                o.StopTimeout = TimeSpan.FromMinutes(5);
            });

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

                    impl.Configure(ctx, cfg, options);

                    cfg.ConfigureEndpoints(ctx, new KebabCaseEndpointNameFormatter(false));
                });
            });
            return services;
        }
    }
}
