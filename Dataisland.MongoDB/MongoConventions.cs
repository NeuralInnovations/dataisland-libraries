using MongoDB.Bson.Serialization.Conventions;

namespace Dataisland.MongoDB;

/// <summary>
/// Serialization conventions every service in this system shares.
/// </summary>
public static class MongoConventions
{
    private static readonly Lock Gate = new();
    private static bool _registered;

    /// <summary>
    /// Makes a service able to read a document that a NEWER service wrote.
    /// <para>
    /// By default the driver throws when a document carries an element the mapped type does not
    /// know about. <c>[BsonIgnoreExtraElements]</c> on an entity does NOT reach the value objects
    /// nested inside it, so an additive field on one of those — the ordinary way this system
    /// evolves a result — breaks the other service the moment the two are not deployed in the same
    /// breath. That has already cost us one incident, and the coupling it creates is worse than the
    /// mistake it catches: nobody wants a release order to be load-bearing.
    /// </para>
    /// <para>
    /// Call before the first document is mapped — <see cref="MongoDBRepositoryExtensions.AddMongoDB"/>
    /// does, at startup. Idempotent.
    /// </para>
    /// </summary>
    public static void EnsureRegistered()
    {
        lock (Gate)
        {
            if (_registered) return;
            ConventionRegistry.Register(
                "Dataisland.IgnoreExtraElements",
                new ConventionPack { new IgnoreExtraElementsConvention(true) },
                _ => true);
            _registered = true;
        }
    }
}
