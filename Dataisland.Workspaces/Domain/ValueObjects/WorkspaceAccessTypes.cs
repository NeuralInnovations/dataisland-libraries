using Dataisland.Contracts.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Workspaces.Domain.ValueObjects;

public class AccessGroupPermits
{
    [BsonElement("isAdmin")]
    public bool IsAdmin { get; set; }

    [BsonElement("permits")]
    public List<AccessPermit> Permits { get; set; } = [];
}

public class AccessPermit
{
    [BsonElement("resource")]
    public AccessResource Resource { get; set; }

    [BsonElement("actions")]
    public List<AccessAction> Actions { get; set; } = [];
}

public class AccessGroupRegulation
{
    [BsonElement("isRegulateOrganization")]
    public bool IsRegulateOrganization { get; set; }

    [BsonElement("regulateWorkspaceIds")]
    public List<ObjectId> RegulateWorkspaceIds { get; set; } = [];
}
