using System.Text;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Litestream sidecar resource that replicates one SQLite database file or a directory of SQLite databases.
/// </summary>
/// <param name="name">The name of the resource.</param>
public sealed class LitestreamResource(string name) : ContainerResource(name)
{
    internal const string ConfigurationFileTargetPath = "/etc/litestream.yml";
    internal const string DefaultDatabaseRootPath = "/var/lib/litestream";

    private const string AzureAccountNameEnvironmentVariable = "LITESTREAM_AZURE_ACCOUNT_NAME";
    private const string S3EndpointEnvironmentVariable = "LITESTREAM_S3_ENDPOINT";

    internal string ConfigurationFileHostPath { get; } = Path.Combine(Path.GetTempPath(), "CommunityToolkit.Aspire", "Litestream", name, "litestream.yml");

    internal string DatabaseRootPath { get; set; } = DefaultDatabaseRootPath;

    internal LitestreamDatabaseMode? DatabaseMode { get; private set; }

    internal string? DatabasePathOrDirectory { get; private set; }

    internal string? Pattern { get; private set; }

    internal bool Recursive { get; private set; }

    internal bool Watch { get; private set; }

    internal LitestreamReplicaProviderType? ReplicaProviderType { get; private set; }

    internal string? ReplicaBucket { get; private set; }

    internal string? ReplicaPath { get; private set; }

    internal string? ReplicaRegion { get; private set; }

    internal bool HasCustomS3Endpoint { get; private set; }

    internal bool ForcePathStyle { get; private set; }

    internal bool SkipVerify { get; private set; }

    /// <summary>
    /// Gets the configured single database path when the resource uses single-database replication.
    /// </summary>
    public string? DatabasePath => DatabaseMode == LitestreamDatabaseMode.Single
        ? ResolveDatabasePath(DatabasePathOrDirectory)
        : null;

    /// <summary>
    /// Gets the configured database directory path when the resource uses directory replication.
    /// </summary>
    public string? DatabaseDirectoryPath => DatabaseMode == LitestreamDatabaseMode.Directory
        ? ResolveDatabasePath(DatabasePathOrDirectory)
        : null;

    internal void ConfigureSingleDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        DatabaseMode = LitestreamDatabaseMode.Single;
        DatabasePathOrDirectory = databasePath;
        Pattern = null;
        Recursive = false;
        Watch = false;
    }

    internal void ConfigureDirectory(string directoryPath, string pattern, bool recursive, bool watch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        DatabaseMode = LitestreamDatabaseMode.Directory;
        DatabasePathOrDirectory = directoryPath;
        Pattern = pattern;
        Recursive = recursive;
        Watch = watch;
    }

    internal void ConfigureS3Replica(string bucket, string path, string? region, bool forcePathStyle, bool skipVerify, bool hasEndpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        ReplicaProviderType = LitestreamReplicaProviderType.S3;
        ReplicaBucket = bucket;
        ReplicaPath = path;
        ReplicaRegion = region;
        HasCustomS3Endpoint = hasEndpoint;
        ForcePathStyle = forcePathStyle || hasEndpoint;
        SkipVerify = skipVerify;
    }

    internal void ConfigureAzureBlobReplica(string containerName, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        ReplicaProviderType = LitestreamReplicaProviderType.AzureBlobStorage;
        ReplicaBucket = containerName;
        ReplicaPath = path;
        ReplicaRegion = null;
        HasCustomS3Endpoint = false;
        ForcePathStyle = false;
        SkipVerify = false;
    }

    internal string RenderConfiguration()
    {
        ValidateConfiguration();

        StringBuilder builder = new();
        builder.AppendLine("dbs:");
        builder.AppendLine("  -");

        if (DatabaseMode == LitestreamDatabaseMode.Single)
        {
            builder.AppendLine($"    path: {Quote(ResolveDatabasePath(DatabasePathOrDirectory))}");
        }
        else
        {
            builder.AppendLine($"    dir: {Quote(ResolveDatabasePath(DatabasePathOrDirectory))}");
            builder.AppendLine($"    pattern: {Quote(Pattern)}");
            builder.AppendLine($"    recursive: {YamlBool(Recursive)}");
            builder.AppendLine($"    watch: {YamlBool(Watch)}");
        }

        builder.AppendLine("    replica:");

        if (ReplicaProviderType == LitestreamReplicaProviderType.S3)
        {
            builder.AppendLine("      type: s3");
            builder.AppendLine($"      bucket: {Quote(ReplicaBucket)}");
            builder.AppendLine($"      path: {Quote(ReplicaPath)}");

            if (!string.IsNullOrWhiteSpace(ReplicaRegion))
            {
                builder.AppendLine($"      region: {Quote(ReplicaRegion)}");
            }

            if (ForcePathStyle)
            {
                builder.AppendLine("      force-path-style: true");
            }

            if (SkipVerify)
            {
                builder.AppendLine("      skip-verify: true");
            }

            if (HasS3Endpoint())
            {
                builder.AppendLine($"      endpoint: ${{{S3EndpointEnvironmentVariable}}}");
            }
        }
        else
        {
            builder.AppendLine("      type: abs");
            builder.AppendLine($"      account-name: ${{{AzureAccountNameEnvironmentVariable}}}");
            builder.AppendLine($"      bucket: {Quote(ReplicaBucket)}");
            builder.AppendLine($"      path: {Quote(ReplicaPath)}");
        }

        return builder.ToString();
    }

    private void ValidateConfiguration()
    {
        if (DatabaseMode is null)
        {
            throw new InvalidOperationException($"Litestream resource '{Name}' requires either a single database path or a database directory.");
        }

        if (ReplicaProviderType is null)
        {
            throw new InvalidOperationException($"Litestream resource '{Name}' requires a replica provider configuration.");
        }
    }

    private bool HasS3Endpoint() => HasCustomS3Endpoint;

    private string ResolveDatabasePath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException($"Litestream resource '{Name}' is missing its configured database path.");
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return NormalizePath(configuredPath);
        }

        return NormalizePath(Path.Join(DatabaseRootPath, configuredPath));
    }

    private static string NormalizePath(string value)
        => value.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);


    private static string YamlBool(bool value)
        => value ? "true" : "false";

    private static string Quote(string? value)
        => $"'{value?.Replace("'", "''", StringComparison.Ordinal) ?? string.Empty}'";
}

internal enum LitestreamDatabaseMode
{
    Single,
    Directory
}

internal enum LitestreamReplicaProviderType
{
    S3,
    AzureBlobStorage
}
