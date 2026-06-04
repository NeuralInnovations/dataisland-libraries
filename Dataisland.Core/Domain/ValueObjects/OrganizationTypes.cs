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

    // Department-scoped membership: when non-empty, the user only sees cases/doctors/analytics
    // tied to doctors in ANY of these departments (union, not intersection). One person can
    // head multiple departments — common in smaller clinics where one director oversees
    // several specialties. Populated by accepting a department-scoped invite (InvitesController).
    [BsonElement("departmentIds")]
    public List<string> DepartmentIds { get; set; } = [];

    // Legacy single-department field. Kept for backward read-compat with records written
    // before DepartmentIds existed; combined into the effective set via Departments.
    [BsonElement("departmentId")]
    public string? DepartmentId { get; set; }

    // Direction-scoped membership: parallel grouping axis to departments. When non-empty the
    // user is a "head of direction" — sees only doctors/cases/analytics tied to doctors in ANY
    // of these directions (union, combined with any DepartmentIds scope). New field, no legacy
    // singular counterpart.
    [BsonElement("directionIds")]
    public List<string> DirectionIds { get; set; } = [];

    /// <summary>
    /// Effective list of department ids that scope this membership — union of the new array
    /// field and the legacy singular field (back-compat with rows written before the array
    /// existed). Empty when the member is org-wide (no department restriction).
    /// </summary>
    [BsonIgnore]
    public IReadOnlyList<string> Departments =>
        string.IsNullOrWhiteSpace(DepartmentId)
            ? DepartmentIds
            : DepartmentIds.Contains(DepartmentId!) ? DepartmentIds : [.. DepartmentIds, DepartmentId!];
}
