namespace Dataisland.Policies;

public interface IPoliciesModel
{
    /// <summary>
    /// Find policies by owner id and owner type.
    /// </summary>
    /// <param name="principal"></param>
    /// <param name="assignedTo"></param>
    /// <returns></returns>
    Task<IEnumerable<Policy>> FindAsync(Principal principal, AssignedTo assignedTo);

    /// <summary>
    /// Find policy by id.
    /// </summary>
    /// <param name="id">Unique id of policy</param>
    /// <returns></returns>
    Task<Policy?> FindAsync(PolicyId id);

    /// <summary>
    /// Delete policies by ids.
    /// </summary>
    /// <param name="policies"></param>
    /// <returns>Count of deleted elements</returns>
    public Task<int> DeleteAsync(IEnumerable<PolicyId> policies);

    /// <summary>
    /// Add a new policy.
    /// </summary>
    /// <param name="principal"></param>
    /// <param name="assignedTo"></param>
    /// <param name="policy"></param>
    /// <returns></returns>
    public Task AddAsync(Principal principal, AssignedTo assignedTo, IEnumerable<Policy> policy);

    /// <summary>
    /// Wait until the model is created.
    /// </summary>
    /// <returns></returns>
    Task EnsureCreatedAsync();
}