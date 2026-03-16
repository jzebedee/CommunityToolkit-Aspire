using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var accessKey = builder.AddParameter("minio-user", "minioadmin");
var secretKey = builder.AddParameter("minio-password", "minioadmin", secret: true);

var minio = builder.AddMinioContainer("minio", accessKey, secretKey);

const string replicaBucketName = "litestream-harness";
const string writerSingleDatabasePath = "/workspace/writer/single/app.db";
const string verifierSingleDatabasePath = "/workspace/verifier/single/app.db";
const string writerGroupDatabaseDirectory = "/workspace/writer/groups";
const string verifierGroupDatabaseDirectory = "/workspace/verifier/groups";

// These projects intentionally use separate local database roots.
// The failing tests become green once Litestream wiring is added by add-litestream-integration.
builder.AddProject<CommunityToolkit_Aspire_Litestream_Testing_Writer>("writer")
    .WithReference(minio)
    .WaitFor(minio)
    .WithHttpEndpoint(env: "HTTP_PORT")
    .WithEnvironment("Harness__ScenarioRole", "writer")
    .WithEnvironment("Harness__ReplicaBucketName", replicaBucketName)
    .WithEnvironment("Harness__SingleDatabasePath", writerSingleDatabasePath)
    .WithEnvironment("Harness__GroupDatabaseDirectory", writerGroupDatabaseDirectory)
    .WithHttpHealthCheck("/health");

builder.AddProject<CommunityToolkit_Aspire_Litestream_Testing_Verifier>("verifier")
    .WithReference(minio)
    .WaitFor(minio)
    .WithHttpEndpoint(env: "HTTP_PORT")
    .WithEnvironment("Harness__ScenarioRole", "verifier")
    .WithEnvironment("Harness__ReplicaBucketName", replicaBucketName)
    .WithEnvironment("Harness__SingleDatabasePath", verifierSingleDatabasePath)
    .WithEnvironment("Harness__GroupDatabaseDirectory", verifierGroupDatabaseDirectory)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
