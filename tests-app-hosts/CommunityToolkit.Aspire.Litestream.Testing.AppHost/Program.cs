using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var accessKey = builder.AddParameter("minio-user", "minioadmin");
var secretKey = builder.AddParameter("minio-password", "minioadmin", secret: true);

var minio = builder.AddMinioContainer("minio", accessKey, secretKey);

string storageRoot = Path.Combine(
    Path.GetTempPath(),
    "CommunityToolkit.Aspire",
    "LitestreamHarness",
    Guid.NewGuid().ToString("N"));

string writerStorageRoot = Path.Combine(storageRoot, "writer");
string verifierStorageRoot = Path.Combine(storageRoot, "verifier");
string[] seededGroupDatabaseNames =
[
    $"tenant-{Guid.NewGuid():N}",
    $"tenant-{Guid.NewGuid():N}",
];
string seededGroupDatabaseNamesValue = string.Join(';', seededGroupDatabaseNames);

Directory.CreateDirectory(writerStorageRoot);
Directory.CreateDirectory(verifierStorageRoot);

string replicaBucketName = $"litestream-harness-{Guid.NewGuid():N}";

string writerSingleDatabasePath = Path.Combine(writerStorageRoot, "single", "app.db");
string verifierSingleDatabasePath = Path.Combine(verifierStorageRoot, "single", "app.db");
string writerGroupDatabaseDirectory = Path.Combine(writerStorageRoot, "groups");
string verifierGroupDatabaseDirectory = Path.Combine(verifierStorageRoot, "groups");

Directory.CreateDirectory(Path.GetDirectoryName(writerSingleDatabasePath) ?? throw new InvalidOperationException("Writer single database path is invalid."));
Directory.CreateDirectory(Path.GetDirectoryName(verifierSingleDatabasePath) ?? throw new InvalidOperationException("Verifier single database path is invalid."));
Directory.CreateDirectory(writerGroupDatabaseDirectory);
Directory.CreateDirectory(verifierGroupDatabaseDirectory);

const string writerLitestreamContainerRoot = "/workspace/writer";
const string writerSingleReplicaPath = "replicas/single/app.db";
const string writerGroupReplicaPath = "replicas/groups";
const string litestreamRestoreImage = "litestream/litestream:latest";

// These projects intentionally use separate local database roots.
// The verifier only reads after an explicit restore step, so success requires remote replication.
var writer = builder.AddProject<CommunityToolkit_Aspire_Litestream_Testing_Writer>("writer")
    .WithReference(minio)
    .WaitFor(minio)
    .WithHttpEndpoint(env: "HTTP_PORT")
    .WithEnvironment("Harness__ScenarioRole", "writer")
    .WithEnvironment("Harness__StorageRoot", writerStorageRoot)
    .WithEnvironment("Harness__ReplicaBucketName", replicaBucketName)
    .WithEnvironment("Harness__SingleDatabasePath", writerSingleDatabasePath)
    .WithEnvironment("Harness__GroupDatabaseDirectory", writerGroupDatabaseDirectory)
    .WithEnvironment("Harness__SeededGroupDatabaseNames", seededGroupDatabaseNamesValue)
    .WithHttpHealthCheck("/health");

builder.AddLitestream("writer-single-litestream")
    .WaitFor(writer)
    .WithDataBindMount(writerStorageRoot, writerLitestreamContainerRoot)
    .WithDatabase("single/app.db")
    .WithMinioReplica(minio, replicaBucketName, writerSingleReplicaPath);

builder.AddLitestream("writer-group-litestream")
    .WaitFor(writer)
    .WithDataBindMount(writerStorageRoot, writerLitestreamContainerRoot)
    .WithDatabaseDirectory("groups", pattern: "*.db", watch: true)
    .WithMinioReplica(minio, replicaBucketName, writerGroupReplicaPath);

builder.AddProject<CommunityToolkit_Aspire_Litestream_Testing_Verifier>("verifier")
    .WithReference(minio)
    .WaitFor(minio)
    .WithHttpEndpoint(env: "HTTP_PORT")
    .WithEnvironment("Harness__ScenarioRole", "verifier")
    .WithEnvironment("Harness__StorageRoot", verifierStorageRoot)
    .WithEnvironment("Harness__ReplicaBucketName", replicaBucketName)
    .WithEnvironment("Harness__SingleDatabasePath", verifierSingleDatabasePath)
    .WithEnvironment("Harness__GroupDatabaseDirectory", verifierGroupDatabaseDirectory)
    .WithEnvironment("Harness__SeededGroupDatabaseNames", seededGroupDatabaseNamesValue)
    .WithEnvironment("Harness__SingleReplicaPath", writerSingleReplicaPath)
    .WithEnvironment("Harness__GroupReplicaPath", writerGroupReplicaPath)
    .WithEnvironment("Harness__LitestreamRestoreImage", litestreamRestoreImage)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
