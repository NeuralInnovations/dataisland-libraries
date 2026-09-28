using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace DataIsland.Middleware;

public interface IAuditLogger
{
    void LogAction(string userId, string action, string entityType, string entityId, object? details = null);
    void LogEvent(AuditLogEvent auditEvent);
}

public sealed record AuditLogEvent(
    string ActorId,
    string? TenantId,
    string ResourceType,
    string ResourceId,
    string Action,
    object? Before,
    object? After,
    string RequestId,
    string Result = "Succeeded",
    DateTimeOffset? TimestampUtc = null);

public class AuditLogger(ILogger<AuditLogger> logger) : IAuditLogger
{
    public void LogAction(string userId, string action, string entityType, string entityId, object? details = null)
    {
        logger.LogInformation(
            "AUDIT: User={UserId} Action={Action} Entity={EntityType} EntityId={EntityId} Details={@Details}",
            userId, action, entityType, entityId, Sanitize(details));
    }

    public void LogEvent(AuditLogEvent auditEvent)
    {
        logger.LogInformation(
            "AUDIT: Actor={ActorId} Tenant={TenantId} ResourceType={ResourceType} ResourceId={ResourceId} " +
            "Action={Action} Before={@Before} After={@After} TimestampUtc={TimestampUtc} " +
            "RequestId={RequestId} Result={Result}",
            auditEvent.ActorId,
            auditEvent.TenantId,
            auditEvent.ResourceType,
            auditEvent.ResourceId,
            auditEvent.Action,
            Sanitize(auditEvent.Before),
            Sanitize(auditEvent.After),
            auditEvent.TimestampUtc ?? DateTimeOffset.UtcNow,
            auditEvent.RequestId,
            auditEvent.Result);
    }

    private static object? Sanitize(object? value)
    {
        if (value is null) return null;

        try
        {
            var node = JsonSerializer.SerializeToNode(value);
            Redact(node);
            return node;
        }
        catch (NotSupportedException)
        {
            return "[unserializable]";
        }
        catch (JsonException)
        {
            return "[unserializable]";
        }
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (IsSensitive(property.Key))
                    obj[property.Key] = "[REDACTED]";
                else
                    Redact(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                Redact(item);
        }
    }

    private static bool IsSensitive(string name) =>
        name.Contains("key", StringComparison.OrdinalIgnoreCase)
        || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("token", StringComparison.OrdinalIgnoreCase)
        || name.Contains("password", StringComparison.OrdinalIgnoreCase)
        || name.Contains("credential", StringComparison.OrdinalIgnoreCase)
        || name.Contains("authorization", StringComparison.OrdinalIgnoreCase);
}
