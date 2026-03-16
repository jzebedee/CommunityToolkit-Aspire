using Microsoft.Data.Sqlite;
using Minio;
using Minio.DataModel.Args;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

ConfigureHttpEndpoint(builder);

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var singleDatabasePath = GetRequiredSetting(builder.Configuration, "Harness:SingleDatabasePath");
var groupDatabaseDirectory = GetRequiredSetting(builder.Configuration, "Harness:GroupDatabaseDirectory");
var replicaBucketName = GetRequiredSetting(builder.Configuration, "Harness:ReplicaBucketName");
var scenarioRole = GetRequiredSetting(builder.Configuration, "Harness:ScenarioRole");
var storageRoot = GetRequiredSetting(builder.Configuration, "Harness:StorageRoot");
string[] seededGroupDatabaseNames = GetSeededGroupDatabaseNames(builder.Configuration);

await EnsureReplicaBucketAsync(builder.Configuration, replicaBucketName);
await EnsureSeededGroupDatabasesAsync(groupDatabaseDirectory, seededGroupDatabaseNames);

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
}));

app.MapPost("/single/{value}", async (string value) =>
{
    await EnsureSchemaAsync(singleDatabasePath);
    await InsertValueAsync(singleDatabasePath, value);

    return Results.Created("/single", new { Value = value });
});

app.MapGet("/single", async () =>
{
    var value = await ReadLatestValueAsync(singleDatabasePath);
    return value is null ? Results.NotFound() : Results.Ok(new { Value = value });
});

app.MapPost("/groups/{databaseName}/{value}", async (string databaseName, string value) =>
{
    var databasePath = GetGroupedDatabasePath(groupDatabaseDirectory, databaseName);

    await EnsureSchemaAsync(databasePath);
    await InsertValueAsync(databasePath, value);

    return Results.Created($"/groups/{databaseName}", new { DatabaseName = databaseName, Value = value });
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
    Directory.CreateDirectory(directoryPath);
    return Path.Combine(directoryPath, $"{databaseName}.db");
}

static string[] GetSeededGroupDatabaseNames(IConfiguration configuration)
{
    return (configuration["Harness:SeededGroupDatabaseNames"] ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static async Task EnsureSchemaAsync(string databasePath)
{
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? throw new InvalidOperationException($"Invalid database path '{databasePath}'."));

    await using var connection = new SqliteConnection($"Data Source={databasePath}");
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = "CREATE TABLE IF NOT EXISTS entries (id INTEGER PRIMARY KEY AUTOINCREMENT, value TEXT NOT NULL);";

    await command.ExecuteNonQueryAsync();
}

static async Task InsertValueAsync(string databasePath, string value)
{
    await using var connection = new SqliteConnection($"Data Source={databasePath}");
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = "INSERT INTO entries (value) VALUES ($value);";
    command.Parameters.AddWithValue("$value", value);

    await command.ExecuteNonQueryAsync();
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

static async Task EnsureReplicaBucketAsync(IConfiguration configuration, string bucketName)
{
    string connectionString = configuration.GetConnectionString("minio")
        ?? throw new InvalidOperationException("Missing MinIO connection string.");

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

    Uri endpointUri = new(endpoint, UriKind.Absolute);
    IMinioClient client = new MinioClient()
        .WithEndpoint(endpointUri.Host, endpointUri.Port)
        .WithCredentials(accessKey, secretKey)
        .WithSSL(endpointUri.Scheme == Uri.UriSchemeHttps)
        .Build();

    BucketExistsArgs existsArgs = new BucketExistsArgs()
        .WithBucket(bucketName);

    if (!await client.BucketExistsAsync(existsArgs))
    {
        await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
    }
}

static async Task EnsureSeededGroupDatabasesAsync(string groupDatabaseDirectory, IEnumerable<string> databaseNames)
{
    foreach (string databaseName in databaseNames)
    {
        await EnsureSchemaAsync(GetGroupedDatabasePath(groupDatabaseDirectory, databaseName));
    }
}
