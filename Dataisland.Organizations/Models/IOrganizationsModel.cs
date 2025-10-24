namespace Dataisland.Organizations.Models;

public interface IOrganizationsModel : IEnsureCreated
{
    Task<Organization?> FindAsync(OrganizationId id);
    Task<Organization> CreateAsync(string name);
    Task<bool> ExistsAsync(OrganizationId id);
    Task<bool> DeleteAsync(OrganizationId id);
}