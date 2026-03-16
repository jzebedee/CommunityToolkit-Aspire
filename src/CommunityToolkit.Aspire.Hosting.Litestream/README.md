# CommunityToolkit.Aspire.Hosting.Litestream library

Provides extension methods and resource definitions for the Aspire AppHost to support configuring Litestream sidecars for SQLite replication.

The MVP package focuses on typed Litestream configuration for:

- a single SQLite database file
- a grouped or directory-based set of SQLite databases
- one replica target per Litestream resource

Current provider support includes:

- generic S3-compatible object storage
- MinIO
- Azure Blob Storage

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Litestream
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Litestream resource:

```csharp
var litestream = builder.AddLitestream("litestream")
    .WithDataBindMount("./data/sqlite")
    .WithDatabase("app.db")
    .WithMinioReplica(minio, "sqlite-replicas", "apps/app");
```

To replicate a grouped directory of databases instead, configure a shared storage root and a directory scope:

```csharp
var litestream = builder.AddLitestream("litestream")
    .WithDataVolume()
    .WithDatabaseDirectory("tenants", pattern: "*.db", recursive: true, watch: true)
    .WithS3Replica(
        bucket: "sqlite-replicas",
        path: "apps/tenants",
        region: "us-east-1");
```

Use `WithDataVolume(...)` or `WithDataBindMount(...)` to place the SQLite files in a location that both your application and the Litestream sidecar can share.

## Additional Information

https://github.com/CommunityToolkit/Aspire

## MVP scope and future expansion

This package currently models Litestream as a container-first sidecar with generated Litestream configuration.

The current MVP intentionally keeps the scope narrow:

- one configured provider per Litestream resource
- typed support for single-file and grouped-directory replication
- provider helpers for S3-compatible storage, MinIO, and Azure Blob Storage

Future work can expand this with additional Litestream providers, more advanced configuration options, and potentially provider-specific extension packages if that offers a cleaner API surface.

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
