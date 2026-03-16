using CommunityToolkit.Aspire.Testing;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Litestream.Tests;

[RequiresDocker]
[Trait("category", "failing")]
public class LitestreamHarnessTests(
    AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Litestream_Testing_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Litestream_Testing_AppHost>>
{
    [Fact]
    public async Task SingleDatabaseReplication_RemainsRedUntilLitestreamIntegrationExists()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        await WaitForHarnessAsync(cts.Token);

        var minioConnectionString = await fixture.GetConnectionString("minio");
        Assert.NotNull(minioConnectionString);

        var writer = fixture.CreateHttpClient("writer");
        var verifier = fixture.CreateHttpClient("verifier");

        var writerConfig = await writer.GetFromJsonAsync<HarnessConfiguration>("/config", cts.Token);
        var verifierConfig = await verifier.GetFromJsonAsync<HarnessConfiguration>("/config", cts.Token);

        Assert.NotNull(writerConfig);
        Assert.NotNull(verifierConfig);
        Assert.NotEqual(writerConfig.SingleDatabasePath, verifierConfig.SingleDatabasePath);
        Assert.Equal(writerConfig.ReplicaBucketName, verifierConfig.ReplicaBucketName);

        var value = $"single-{Guid.NewGuid():N}";
        var writeResponse = await writer.PostAsync($"/single/{value}", content: null, cts.Token);
        Assert.Equal(HttpStatusCode.Created, writeResponse.StatusCode);

        var verifyResponse = await verifier.GetAsync("/single", cts.Token);
        string? verifiedValue = null;

        if (verifyResponse.IsSuccessStatusCode)
        {
            var payload = await verifyResponse.Content.ReadFromJsonAsync<ValuePayload>(cancellationToken: cts.Token);
            verifiedValue = payload?.Value;
        }

        Assert.True(
            verifyResponse.StatusCode == HttpStatusCode.OK && verifiedValue == value,
            $"Expected verifier to observe replicated single-database value '{value}', but got status {(int)verifyResponse.StatusCode} and value '{verifiedValue ?? "<null>"}'.");
    }

    [Fact]
    public async Task GroupedDatabaseReplication_RemainsRedUntilLitestreamIntegrationExists()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        await WaitForHarnessAsync(cts.Token);

        var writer = fixture.CreateHttpClient("writer");
        var verifier = fixture.CreateHttpClient("verifier");

        var writerConfig = await writer.GetFromJsonAsync<HarnessConfiguration>("/config", cts.Token);
        var verifierConfig = await verifier.GetFromJsonAsync<HarnessConfiguration>("/config", cts.Token);

        Assert.NotNull(writerConfig);
        Assert.NotNull(verifierConfig);
        Assert.NotEqual(writerConfig.GroupDatabaseDirectory, verifierConfig.GroupDatabaseDirectory);
        Assert.Equal(writerConfig.ReplicaBucketName, verifierConfig.ReplicaBucketName);

        var tenantName = $"tenant-{Guid.NewGuid():N}";
        var value = $"group-{Guid.NewGuid():N}";

        var writeResponse = await writer.PostAsync($"/groups/{tenantName}/{value}", content: null, cts.Token);
        Assert.Equal(HttpStatusCode.Created, writeResponse.StatusCode);

        var verifyResponse = await verifier.GetAsync($"/groups/{tenantName}", cts.Token);
        string? verifiedValue = null;

        if (verifyResponse.IsSuccessStatusCode)
        {
            var payload = await verifyResponse.Content.ReadFromJsonAsync<GroupValuePayload>(cancellationToken: cts.Token);
            verifiedValue = payload?.Value;
        }

        Assert.True(
            verifyResponse.StatusCode == HttpStatusCode.OK && verifiedValue == value,
            $"Expected verifier to observe replicated grouped-database value '{value}' for '{tenantName}', but got status {(int)verifyResponse.StatusCode} and value '{verifiedValue ?? "<null>"}'.");
    }

    private async Task WaitForHarnessAsync(CancellationToken cancellationToken)
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("minio", cancellationToken).WaitAsync(cancellationToken);
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("writer", cancellationToken).WaitAsync(cancellationToken);
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("verifier", cancellationToken).WaitAsync(cancellationToken);
    }

    private sealed class HarnessConfiguration
    {
        public required string Role { get; init; }

        public required string SingleDatabasePath { get; init; }

        public required string GroupDatabaseDirectory { get; init; }

        public required string ReplicaBucketName { get; init; }
    }

    private sealed class ValuePayload
    {
        public required string Value { get; init; }
    }

    private sealed class GroupValuePayload
    {
        public required string DatabaseName { get; init; }

        public required string Value { get; init; }
    }
}
