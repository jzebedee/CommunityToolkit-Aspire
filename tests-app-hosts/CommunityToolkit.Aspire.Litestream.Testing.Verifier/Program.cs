using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

ConfigureHttpEndpoint(builder);

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var singleDatabasePath = GetRequiredSetting(builder.Configuration, "Harness:SingleDatabasePath");
var groupDatabaseDirectory = GetRequiredSetting(builder.Configuration, "Harness:GroupDatabaseDirectory");
var replicaBucketName = GetRequiredSetting(builder.Configuration, "Harness:ReplicaBucketName");
var scenarioRole = GetRequiredSetting(builder.Configuration, "Harness:ScenarioRole");

var app = builder.Build();

app.UseExceptionHandler();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new { role = scenarioRole }));
app.MapGet("/config", () => Results.Ok(new
{
    Role = scenarioRole,
    SingleDatabasePath = singleDatabasePath,
    GroupDatabaseDirectory = groupDatabaseDirectory,
    ReplicaBucketName = replicaBucketName,
}));

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
