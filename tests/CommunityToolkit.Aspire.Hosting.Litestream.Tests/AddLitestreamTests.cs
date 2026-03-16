using Aspire.Hosting;
using Aspire.Hosting.Azure;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Minio;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Litestream.Tests;

public class AddLitestreamTests
{
    [Fact]
    public void DistributedApplicationBuilderCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => LitestreamResourceBuilderExtensions.AddLitestream(null!, "litestream"));
    }

    [Fact]
    public void ResourceNameCannotBeOmitted()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string whitespaceName = " ";

        Assert.Throws<ArgumentException>(() => builder.AddLitestream(string.Empty));
        Assert.Throws<ArgumentException>(() => builder.AddLitestream(whitespaceName));
        Assert.Throws<ArgumentNullException>(() => builder.AddLitestream(null!));
    }

    [Fact]
    public void EachResourceHasUniqueConfigurationFile()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        IResourceBuilder<LitestreamResource> litestream1 = builder.AddLitestream("litestream-1");
        IResourceBuilder<LitestreamResource> litestream2 = builder.AddLitestream("litestream-2");

        Assert.NotEqual(litestream1.Resource.ConfigurationFileHostPath, litestream2.Resource.ConfigurationFileHostPath);
    }

    [Fact]
    public void GenericS3ConfigurationDoesNotRenderEndpointWhenNoneWasConfigured()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        LitestreamResource resource = builder
            .AddLitestream("litestream")
            .WithDatabase("databases/app.db")
            .WithS3Replica("replica-bucket", "backups/app", region: "us-east-1")
            .Resource;

        Assert.Equal("/var/lib/litestream/databases/app.db", resource.DatabasePath);
        Assert.False(resource.HasCustomS3Endpoint);
        Assert.DoesNotContain("endpoint:", resource.RenderConfiguration(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CustomS3ConfigurationRendersExpectedConfigurationAndEnvironment()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ParameterResource> accessKey = builder.AddParameter("litestream-access-key", "access-value");
        IResourceBuilder<ParameterResource> secretKey = builder.AddParameter("litestream-secret-key", "secret-value", secret: true);

        IResourceBuilder<LitestreamResource> litestream = builder
            .AddLitestream("litestream")
            .WithDatabase("databases/app.db")
            .WithS3Replica(
                "replica-bucket",
                "backups/app",
                region: "us-east-1",
                endpoint: "https://s3.example.test",
                accessKey: accessKey,
                secretKey: secretKey,
                skipVerify: true);

        using DistributedApplication app = builder.Build();

        LitestreamResource resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources
            .OfType<LitestreamResource>()
            .Single();

        Dictionary<string, string> environment = await resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Equal(
            """
            dbs:
              -
                path: '/var/lib/litestream/databases/app.db'
                replica:
                  type: s3
                  bucket: 'replica-bucket'
                  path: 'backups/app'
                  region: 'us-east-1'
                  force-path-style: true
                  skip-verify: true
                  endpoint: ${LITESTREAM_S3_ENDPOINT}
            """,
            resource.RenderConfiguration().TrimEnd());

        Assert.Equal("https://s3.example.test", environment["LITESTREAM_S3_ENDPOINT"]);
        Assert.Equal("access-value", environment["LITESTREAM_ACCESS_KEY_ID"]);
        Assert.Equal("secret-value", environment["LITESTREAM_SECRET_ACCESS_KEY"]);

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out IEnumerable<ContainerMountAnnotation>? mounts));
        Assert.Contains(
            mounts,
            mount => mount.Source == resource.ConfigurationFileHostPath &&
                mount.Target == LitestreamResource.ConfigurationFileTargetPath &&
                mount.IsReadOnly);
    }

    [Fact]
    public async Task MinioReplicaRendersDirectoryConfigurationAndResolvedEnvironment()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ParameterResource> rootUser = builder.AddParameter("minio-root-user", "minio-user");
        IResourceBuilder<ParameterResource> rootPassword = builder.AddParameter("minio-root-password", "minio-password", secret: true);
        IResourceBuilder<MinioContainerResource> minio = builder.AddMinioContainer("minio", rootUser, rootPassword);

        IResourceBuilder<LitestreamResource> litestream = builder
            .AddLitestream("litestream")
            .WithDataBindMount(@"C:\litestream-data", "/replica-data")
            .WithDatabaseDirectory("tenants", pattern: "*.sqlite", recursive: true, watch: true)
            .WithMinioReplica(minio, "replica-bucket", "backups/tenants");

        using DistributedApplication app = builder.Build();

        EndpointAnnotation endpoint = minio.Resource.GetEndpoint("http").EndpointAnnotation;
        AllocatedEndpoint allocatedEndpoint = new(endpoint, "minio.test.internal", 9000, EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.DefaultAspireContainerNetwork);
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(KnownNetworkIdentifiers.DefaultAspireContainerNetwork, allocatedEndpoint);

        LitestreamResource resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources
            .OfType<LitestreamResource>()
            .Single(resource => resource.Name == "litestream");

        Dictionary<string, string> environment = await resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Equal("/replica-data/tenants", resource.DatabaseDirectoryPath);
        Assert.Equal(
            """
            dbs:
              -
                dir: '/replica-data/tenants'
                pattern: '*.sqlite'
                recursive: true
                watch: true
                replica:
                  type: s3
                  bucket: 'replica-bucket'
                  path: 'backups/tenants'
                  force-path-style: true
                  endpoint: ${LITESTREAM_S3_ENDPOINT}
            """,
            resource.RenderConfiguration().TrimEnd());

        Assert.Equal("minio-user", environment["LITESTREAM_ACCESS_KEY_ID"]);
        Assert.Equal("minio-password", environment["LITESTREAM_SECRET_ACCESS_KEY"]);
        Assert.StartsWith("http://", environment["LITESTREAM_S3_ENDPOINT"], StringComparison.Ordinal);

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out IEnumerable<ContainerMountAnnotation>? mounts));
        Assert.Contains(mounts, mount => mount.Source == @"C:\litestream-data" && mount.Target == "/replica-data");
    }

    [Fact]
    public async Task AzureBlobReplicaRendersExpectedConfigurationAndResolvedEnvironment()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<ParameterResource> accountKey = builder.AddParameter("storage-account-key", "storage-key-value", secret: true);
        IResourceBuilder<AzureStorageResource> storage = builder.AddAzureStorage("storage")
            .RunAsEmulator();
        IResourceBuilder<AzureBlobStorageResource> blobs = storage.AddBlobs("blobs");

        IResourceBuilder<LitestreamResource> litestream = builder
            .AddLitestream("litestream")
            .WithDatabase("/data/app.db")
            .WithAzureBlobReplica(blobs, "replica-container", "backups/app", accountKey);

        using DistributedApplication app = builder.Build();

        LitestreamResource resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources
            .OfType<LitestreamResource>()
            .Single(resource => resource.Name == "litestream");

        Dictionary<string, string> environment = await resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Equal(
            """
            dbs:
              -
                path: '/data/app.db'
                replica:
                  type: abs
                  account-name: ${LITESTREAM_AZURE_ACCOUNT_NAME}
                  bucket: 'replica-container'
                  path: 'backups/app'
            """,
            resource.RenderConfiguration().TrimEnd());

        Assert.Equal("devstoreaccount1", environment["LITESTREAM_AZURE_ACCOUNT_NAME"]);
        Assert.Equal("storage-key-value", environment["LITESTREAM_AZURE_ACCOUNT_KEY"]);
    }
}
