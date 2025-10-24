namespace Dataisland.Organizations.Models;

public interface IEnsureCreated
{
    /// <summary>
    /// Ensure all models are created.
    /// </summary>
    /// <returns></returns>
    Task EnsureCreatedAsync();
}