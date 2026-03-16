using Microsoft.Data.Sqlite;
using System.Data.Common;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

ConfigureHttpEndpoint(builder);

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var singleDatabasePath = GetRequiredSetting(builder.Configuration, "Harness:SingleDatabasePath");
var groupDatabaseDirectory = GetRequiredSetting(builder.Configuration, "Harness:GroupDatabaseDirectory");
var replicaBucketName = GetRequiredSetting(builder.Configuration, "Harness:ReplicaBucketName");
var scenarioRole = GetRequiredSetting(builder.Configuration, "Harness:ScenarioRole");
var storageRoot = GetRequiredSetting(builder.Configuration, "Harness:StorageRoot");
var singleReplicaPath = GetRequiredSetting(builder.Configuration, "Harness:SingleReplicaPath");
var groupReplicaPath = GetRequiredSetting(builder.Configuration, "Harness:GroupReplicaPath");
var litestreamRestoreImage = GetRequiredSetting(builder.Configuration, "Harness:LitestreamRestoreImage");
string[] seededGroupDatabaseNames = GetSeededGroupDatabaseNames(builder.Configuration);
MinioConnectionSettings minioSettings = ParseMinioConnectionString(
    builder.Configuration.GetConnectionString("minio")
    ?? throw new InvalidOperationException("Missing MinIO connection string."));

var app = builder.Build();

app.UseExceptionHandler();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { role = scenarioRole }));
app.MapGet("/config", () => Results.Ok(new
{
    Role = scenarioRole,
    StorageRoot = storageRoot,
    SingleDatabasePath = singleDatabasePath,
    GroupDatabaseDirectory = groupDatabaseDirectory,
    ReplicaBucketName = replicaBucketName,
    SeededGroupDatabaseNames = seededGroupDatabaseNames,
    SingleReplicaPath = singleReplicaPath,
    GroupReplicaPath = groupReplicaPath,
}));

app.MapPost("/restore/single", async (CancellationToken cancellationToken) =>
{
    RestoreResult result = await RestoreAsync(
        storageRoot,
        singleDatabasePath,
        CreateReplicaUrl(replicaBucketName, singleReplicaPath),
        litestreamRestoreImage,
        minioSettings,
        cancellationToken);

    return result.DatabaseExists
        ? Results.Ok(result)
        : Results.NotFound(result);
});

app.MapPost("/restore/groups/{databaseName}", async (string databaseName, CancellationToken cancellationToken) =>
{
    string databasePath = GetGroupedDatabasePath(groupDatabaseDirectory, databaseName);
    string replicaUrl = CreateReplicaUrl(replicaBucketName, $"{groupReplicaPath}/{databaseName}.db");

    RestoreResult result = await RestoreAsync(
        storageRoot,
        databasePath,
        replicaUrl,
        litestreamRestoreImage,
        minioSettings,
        cancellationToken);

    return result.DatabaseExists
        ? Results.Ok(result)
        : Results.NotFound(result);
});

app.MapGet("/single", async () =>
{
    var value = await ReadLatestValueAsync(singleDatabasePath);
    return value is null ? Results.NotFound() : Results.Ok(new { Value = value });
});

app.MapGet("/groups/{databaseName}", async (string databaseName) =>
{
    var databasePath = GetGroupedDatabasePath(groupDatabaseDirectory, databaseName);
    var value = await ReadLatestValueAsync(databasePath);

    return value is null ? Results.NotFound() : Results.Ok(new { DatabaseName = databaseName, Value = value });
});

app.Run();

static string GetRequiredSetting(IConfiguration configuration, string key)
{
    return configuration[key] ?? throw new InvalidOperationException($"Missing required configuration value '{key}'.");
}

static void ConfigureHttpEndpoint(WebApplicationBuilder builder)
{
    var httpPort = builder.Configuration["HTTP_PORT"];

    if (!string.IsNullOrWhiteSpace(httpPort))
    {
        builder.WebHost.UseUrls($"http://127.0.0.1:{httpPort}");
    }
}

static string GetGroupedDatabasePath(string directoryPath, string databaseName)
{
    return Path.Combine(directoryPath, $"{databaseName}.db");
}

static string[] GetSeededGroupDatabaseNames(IConfiguration configuration)
{
    return (configuration["Harness:SeededGroupDatabaseNames"] ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static async Task<string?> ReadLatestValueAsync(string databasePath)
{
    if (!File.Exists(databasePath))
    {
        return null;
    }

    await using var connection = new SqliteConnection($"Data Source={databasePath}");
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = "SELECT value FROM entries ORDER BY id DESC LIMIT 1;";

    var result = await command.ExecuteScalarAsync();
    return result?.ToString();
}

static string CreateReplicaUrl(string bucketName, string replicaPath)
{
    return $"s3://{bucketName}/{replicaPath.Replace('\\', '/')}";
}

static MinioConnectionSettings ParseMinioConnectionString(string connectionString)
{
    DbConnectionStringBuilder builder = new()
    {
        ConnectionString = connectionString,
    };

    string endpoint = builder["Endpoint"]?.ToString()
        ?? throw new InvalidOperationException("Missing MinIO endpoint.");
    string accessKey = builder["AccessKey"]?.ToString()
        ?? throw new InvalidOperationException("Missing MinIO access key.");
    string secretKey = builder["SecretKey"]?.ToString()
        ?? throw new InvalidOperationException("Missing MinIO secret key.");

    return new MinioConnectionSettings(new Uri(endpoint, UriKind.Absolute), accessKey, secretKey);
}

static async Task<RestoreResult> RestoreAsync(
    string storageRoot,
    string databasePath,
    string replicaUrl,
    string litestreamImage,
    MinioConnectionSettings minioSettings,
    CancellationToken cancellationToken)
{
    string fullStorageRoot = Path.GetFullPath(storageRoot);
    string fullDatabasePath = Path.GetFullPath(databasePath);
    string relativeDatabasePath = Path.GetRelativePath(fullStorageRoot, fullDatabasePath);

    if (relativeDatabasePath.StartsWith("..", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Database path '{databasePath}' is outside of the configured storage root '{storageRoot}'.");
    }

    Directory.CreateDirectory(Path.GetDirectoryName(fullDatabasePath) ?? throw new InvalidOperationException($"Invalid database path '{databasePath}'."));

    const string containerStorageRoot = "/workspace/restore";
    string containerDatabasePath = $"{containerStorageRoot}/{relativeDatabasePath.Replace('\\', '/')}";
    string restoreEndpoint = BuildRestoreEndpoint(minioSettings.Endpoint);

    ProcessStartInfo startInfo = new("docker")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    startInfo.ArgumentList.Add("run");
    startInfo.ArgumentList.Add("--rm");
    startInfo.ArgumentList.Add("--add-host");
    startInfo.ArgumentList.Add("host.docker.internal:host-gateway");
    startInfo.ArgumentList.Add("-v");
    startInfo.ArgumentList.Add($"{fullStorageRoot}:{containerStorageRoot}");
    startInfo.ArgumentList.Add("-e");
    startInfo.ArgumentList.Add($"LITESTREAM_ACCESS_KEY_ID={minioSettings.AccessKey}");
    startInfo.ArgumentList.Add("-e");
    startInfo.ArgumentList.Add($"LITESTREAM_SECRET_ACCESS_KEY={minioSettings.SecretKey}");
    startInfo.ArgumentList.Add(litestreamImage);
    startInfo.ArgumentList.Add("restore");
    startInfo.ArgumentList.Add("-if-db-not-exists");
    startInfo.ArgumentList.Add("-if-replica-exists");
    startInfo.ArgumentList.Add("-o");
    startInfo.ArgumentList.Add(containerDatabasePath);
    startInfo.ArgumentList.Add($"{replicaUrl}?endpoint={Uri.EscapeDataString(restoreEndpoint)}");

    using Process process = new()
    {
        StartInfo = startInfo,
    };

    process.Start();

    Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
    Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

    await process.WaitForExitAsync(cancellationToken);

    string standardOutput = await stdoutTask;
    string standardError = await stderrTask;

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"Litestream restore failed with exit code {process.ExitCode}.{Environment.NewLine}stdout: {standardOutput}{Environment.NewLine}stderr: {standardError}");
    }

    return new RestoreResult
    {
        DatabaseExists = File.Exists(fullDatabasePath),
        DatabasePath = fullDatabasePath,
        ReplicaUrl = replicaUrl,
        Output = standardOutput.Trim(),
        Error = standardError.Trim(),
    };
}

static string BuildRestoreEndpoint(Uri endpoint)
{
    string host = endpoint.Host is "localhost" or "127.0.0.1" ? "host.docker.internal" : endpoint.Host;
    return $"{endpoint.Scheme}://{host}:{endpoint.Port}";
}

internal sealed record MinioConnectionSettings(Uri Endpoint, string AccessKey, string SecretKey);

internal sealed class RestoreResult
{
    public required bool DatabaseExists { get; init; }

    public required string DatabasePath { get; init; }

    public required string ReplicaUrl { get; init; }

    public required string Output { get; init; }

    public required string Error { get; init; }
}
