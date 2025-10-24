namespace Dataisland.Organizations.ApiKeys;

public record ApiKey(ApiKeyId Id, ApiKeyAssignedTo AssignedTo, string Key, DateTime CreatedAt, DateTime ModifiedAt);