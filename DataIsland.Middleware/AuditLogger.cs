using Microsoft.Extensions.Logging;

namespace DataIsland.Middleware;

public interface IAuditLogger
{
    void LogAction(string userId, string action, string entityType, string entityId, object? details = null);
}

public class AuditLogger(ILogger<AuditLogger> logger) : IAuditLogger
{
    public void LogAction(string userId, string action, string entityType, string entityId, object? details = null)
    {
        logger.LogInformation(
            "AUDIT: User={UserId} Action={Action} Entity={EntityType} EntityId={EntityId} Details={@Details}",
            userId, action, entityType, entityId, details);
    }
}
