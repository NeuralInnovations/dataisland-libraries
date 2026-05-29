using Dataisland.Contracts.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dataisland.Core.Domain.ValueObjects;

[BsonIgnoreExtraElements]
public class OrganizationMemberRole
{
    [BsonElement("userId")]
    public ObjectId UserId { get; set; }

    [BsonElement("role")]
    public OrganizationRole Role { get; set; } = OrganizationRole.Admin;

    [BsonElement("doctorId")]
    public string? DoctorId { get; set; }

    // Department-scoped membership: when set, the user only sees cases/doctors/analytics
    // tied to doctors in this department. Populated by accepting a department-scoped invite
    // (see InvitesController). Independent of Role — a Doctor + Department combo just means
    // the doctor has been registered under a department too.
    [BsonElement("departmentId")]
    public string? DepartmentId { get; set; }
}
