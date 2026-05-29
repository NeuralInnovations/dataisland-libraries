using Dataisland.Contracts.Shared;
using Dataisland.Core.Domain.ValueObjects;
using Dataisland.MongoDB.Entity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.Entities;

[BsonIgnoreExtraElements(Inherited = true)]
public class InviteLink : EntityBase
{
    [BsonElement("organizationId")]
    public ObjectId OrganizationId { get; set; }

    [BsonElement("createdBy")]
    public ObjectId CreatedBy { get; set; }

    [BsonElement("accessGroupIds")]
    public List<ObjectId> AccessGroupIds { get; set; } = [];

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("expireAt")]
    public DateTime ExpireAt { get; set; }

    [BsonElement("isSingleUsageMode")]
    public bool IsSingleUsageMode { get; set; }

    [BsonElement("validationParameters")]
    public InviteValidationParameters ValidationParameters { get; set; } = new();

    [BsonElement("role")]
    public OrganizationRole Role { get; set; } = OrganizationRole.Admin;

    [BsonElement("doctorId")]
    public string? DoctorId { get; set; }

    // When set, accepting the invite adds the joining user's DoctorProfile to this
    // department's DepartmentIds. Only meaningful with Role=Doctor — admin-role invites
    // are not department-scoped at the membership layer. Validated at create time to
    // ensure the department belongs to OrganizationId.
    [BsonElement("departmentId")]
    public string? DepartmentId { get; set; }
}

[BsonIgnoreExtraElements(Inherited = true)]
public class InviteLinkStats : EntityBase
{
    [BsonElement("inviteLinkId")]
    public ObjectId InviteLinkId { get; set; }

    [BsonElement("organizationId")]
    public ObjectId OrganizationId { get; set; }

    [BsonElement("createdBy")]
    public ObjectId CreatedBy { get; set; }

    [BsonElement("usages")]
    public int Usages { get; set; }

    [BsonElement("deactivatedAt")]
    public DateTime? DeactivatedAt { get; set; }
}

[BsonIgnoreExtraElements(Inherited = true)]
public class InviteWhiteList : EntityBase
{
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("expireAt")]
    public DateTime ExpireAt { get; set; }
}
