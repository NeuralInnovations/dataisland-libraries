using MassTransit;
using Microsoft.Extensions.Logging;

namespace Dataisland.MQ;

public class ErrorQueueObserver(ILogger<ErrorQueueObserver> logger) : IReceiveObserver
{
    public Task PreReceive(ReceiveContext context) => Task.CompletedTask;

    public Task PostReceive(ReceiveContext context) => Task.CompletedTask;

    public Task PostConsume<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType)
        where T : class
    {
        var consumer = ShortName(consumerType);
        var messageType = typeof(T).Name;

        ConsumerMetrics.ProcessingDurationSeconds
            .WithLabels(consumer, messageType)
            .Observe(duration.TotalSeconds);
        ConsumerMetrics.MessagesTotal
            .WithLabels(consumer, messageType, "success")
            .Inc();

        logger.LogInformation(
            "Consumer completed: {ConsumerType} processed {MessageType} in {Duration:F1}s (MessageId={MessageId}, CorrelationId={CorrelationId})",
            consumer, messageType, duration.TotalSeconds, context.MessageId, context.CorrelationId);

        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType, Exception exception)
        where T : class
    {
        var consumer = ShortName(consumerType);
        var messageType = typeof(T).Name;

        ConsumerMetrics.ProcessingDurationSeconds
            .WithLabels(consumer, messageType)
            .Observe(duration.TotalSeconds);
        ConsumerMetrics.MessagesTotal
            .WithLabels(consumer, messageType, "fault")
            .Inc();

        logger.LogWarning(exception,
            "Consumer fault: {ConsumerType} failed processing {MessageType} in {Duration:F1}s (MessageId={MessageId}, CorrelationId={CorrelationId})",
            consumer, messageType, duration.TotalSeconds, context.MessageId, context.CorrelationId);

        return Task.CompletedTask;
    }

    public Task ReceiveFault(ReceiveContext context, Exception exception)
    {
        logger.LogError(exception,
            "Message moved to error queue: InputAddress={InputAddress}. Manual intervention required.",
            context.InputAddress);
        return Task.CompletedTask;
    }

    private static string ShortName(string fullTypeName)
    {
        var idx = fullTypeName.LastIndexOf('.');
        return idx >= 0 ? fullTypeName[(idx + 1)..] : fullTypeName;
    }
}
