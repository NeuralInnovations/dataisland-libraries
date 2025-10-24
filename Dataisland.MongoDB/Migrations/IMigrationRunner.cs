namespace Dataisland.MongoDB.Migrations;

public interface IMigrationRunner
{
    Task RunAsync(MigrationVersion toVersion, CancellationToken cancellationToken);
}