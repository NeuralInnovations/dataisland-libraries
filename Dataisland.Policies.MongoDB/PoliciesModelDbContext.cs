// using Microsoft.EntityFrameworkCore;
//
// namespace Dataisland.Policies;
//
// public class PoliciesModelDbContext(PolicyDbContext db) : IPoliciesModel
// {
//     public async Task<IEnumerable<Policy>> FindAsync(OwnerType ownerType, OwnerKey ownerKey)
//     {
//         var collection = await db
//             .Policies
//             //.Where(p => p.OwnerKey == ownerKey.Value && p.OwnerType == ownerType.Value)
//             .ToListAsync();
//
//         // select does not support in MongoDB Entity Framework
//         // so we have to use LINQ to Objects
//         return collection.Select(p => new Policy(p.Name, p.Effect, p.Actions, p.Resources)
//         {
//             Id = p.Id,
//         });
//     }
//
//     public async Task<Policy?> FindAsync(PolicyId id)
//     {
//         var result = await db.Policies.FirstOrDefaultAsync(p => p.Id == id.Value) ?? null;
//         if (result != null)
//         {
//             return new Policy(result.Name, result.Effect, result.Actions, result.Resources)
//             {
//                 Id = result.Id,
//             };
//         }
//
//         return null;
//     }
//
//     public async Task<int> DeleteAsync(IEnumerable<PolicyId> policies)
//     {
//         var idValues = policies.Distinct().ToArray();
//         if (idValues.Length == 0)
//             return 0;
//
//         var results = await db.Policies
//             .Where(p => idValues.Contains(p.Id))
//             .ExecuteDeleteAsync();
//
//         return results;
//     }
//
//     public async Task AddAsync(OwnerType ownerType, OwnerKey ownerKey, IEnumerable<Policy> policy)
//     {
//         await db.Policies.AddRangeAsync(policy.Select(p => new PolicyEntity
//         {
//             OwnerType = ownerType.Value,
//             OwnerKey = ownerKey.Value,
//
//             Name = p.Name,
//             Effect = p.Effect,
//             Actions = p.Actions,
//             Resources = p.Resources
//         }));
//
//         await db.SaveChangesAsync();
//     }
//
//     public Task EnsureCreatedAsync() => Task.CompletedTask;
// }