// using System.ComponentModel.DataAnnotations;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.ChangeTracking;
// using MongoDB.Bson.Serialization;
//
// namespace Dataisland.Policies;
//
// public class PolicyDbContext(DbContextOptions options) : DbContext(options)
// {
//     public DbSet<PolicyEntity> Policies { get; init; } = null!;
//
//     protected override void OnModelCreating(ModelBuilder modelBuilder)
//     {
//         modelBuilder.Entity<PolicyEntity>(entity =>
//         {
//             // KEY
//             entity.HasKey(x => x.Id);
//             entity.Property(x => x.Id)
//                 .IsRequired()
//                 .HasConversion(
//                     id => (string)id,
//                     s => (PolicyId)s)
//                 .HasMaxLength(26)
//                 .IsUnicode(false)
//                 .ValueGeneratedNever()
//                 .Metadata
//                 .SetValueComparer(
//                     new ValueComparer<PolicyId>((a, b) => a.Equals(b), v => v.GetHashCode(), v => v)
//                 )
//                 ;
//
//             // OWNER TYPE
//             entity.HasIndex(x => x.OwnerType);
//             entity.Property(x => x.OwnerType)
//                 .HasConversion(
//                     id => (string)id,
//                     s => (OwnerType)s)
//                 .HasMaxLength(128)
//                 .IsUnicode(false)
//                 .Metadata
//                 .SetValueComparer(
//                     new ValueComparer<OwnerType>((a, b) => a.Equals(b), v => v.GetHashCode(), v => v)
//                 )
//                 ;
//
//             // OWNER Key
//             entity.HasIndex(x => x.OwnerKey);
//             entity.Property(x => x.OwnerKey)
//                 .HasConversion(
//                     id => (string)id,
//                     s => (OwnerKey)s)
//                 .HasMaxLength(128)
//                 .IsUnicode(false)
//                 .Metadata
//                 .SetValueComparer(
//                     new ValueComparer<OwnerKey>((a, b) => a.Equals(b), v => v.GetHashCode(), v => v)
//                 )
//                 ;
//
//             // ACTIONS
//             entity.Property(x => x.Actions)
//                 .HasConversion(
//                     a => a.Select(v => (string)v).ToList(),
//                     s => s.Select(v => (PolicyAction)v).ToArray()
//                 )
//                 .IsUnicode(false)
//                 .Metadata
//                 .SetValueComparer(
//                     new ArrayStructuralComparer<PolicyAction>()
//                 )
//                 ;
//
//             // RESOURCES
//             entity.Property(x => x.Resources)
//                 .HasConversion(
//                     r => r.Select(v => (string)v).ToList(),
//                     s => s.Select(v => (PolicyResource)v).ToArray()
//                 )
//                 .IsUnicode(false)
//                 .Metadata
//                 .SetValueComparer(
//                     new ArrayStructuralComparer<PolicyResource>()
//                 )
//                 ;
//         });
//     }
// }