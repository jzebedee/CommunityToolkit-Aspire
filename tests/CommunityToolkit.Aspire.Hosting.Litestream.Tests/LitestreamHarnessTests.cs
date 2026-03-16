using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;
using System.Net;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Litestream.Tests;

[RequiresDocker]
public class LitestreamHarnessTests(
    AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Litestream_Testing_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Litestream_Testing_AppHost>>
{
    [Fact]
    public async Task SingleDatabaseReplication_RestoresFromRemoteReplica()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        await WaitForHarnessAsync(cts.Token);

        var writer = fixture.CreateHttpClient("writer");
        var verifier = fixture.CreateHttpClient("verifier");

        var writerConfig = await writer.GetFromJsonAsync<HarnessConfiguration>("/config", cts.Token);
        var verifierConfig = await verifier.GetFromJsonAsync<HarnessConfiguration>("/config", cts.Token);

        Assert.NotNull(writerConfig);
        Assert.NotNull(verifierConfig);
        Assert.NotEqual(writerConfig.SingleDatabasePath, verifierConfig.SingleDatabasePath);
        Assert.Equal(writerConfig.ReplicaBucketName, verifierConfig.ReplicaBucketName);
        Assert.NotEqual(writerConfig.StorageRoot, verifierConfig.StorageRoot);

        var value = $"single-{Guid.NewGuid():N}";
        var writeResponse = await writer.PostAsync($"/single/{value}", content: null, cts.Token);
        Assert.Equal(HttpStatusCode.Created, writeResponse.StatusCode);

        await WaitForRestoreAsync(verifier, "/restore/single", cts.Token);

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
    public async Task GroupedDatabaseReplication_RestoresFromRemoteReplica()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        await WaitForHarnessAsync(cts.Token);

        var writer = fixture.CreateHttpClient("writer");
        var verifier = fixture.CreateHttpClient("verifier");

        var writerConfig = await writer.GetFromJsonAsync<HarnessConfiguration>("/config", cts.Token);
        var verifierConfig = await verifier.GetFromJsonAsync<HarnessConfiguration>("/config", cts.Token);

        Assert.NotNull(writerConfig);
        Assert.NotNull(verifierConfig);
        Assert.NotEqual(writerConfig.GroupDatabaseDirectory, verifierConfig.GroupDatabaseDirectory);
        Assert.Equal(writerConfig.ReplicaBucketName, verifierConfig.ReplicaBucketName);
        Assert.Equal(writerConfig.SeededGroupDatabaseNames, verifierConfig.SeededGroupDatabaseNames);
        Assert.NotEqual(writerConfig.StorageRoot, verifierConfig.StorageRoot);
        Assert.True(
            writerConfig.SeededGroupDatabaseNames.Length >= 2,
            "Expected the harness to provide at least two grouped database names for directory replication validation.");

        string firstDatabaseName = writerConfig.SeededGroupDatabaseNames[0];
        string secondDatabaseName = writerConfig.SeededGroupDatabaseNames[1];
        string firstValue = $"group-{Guid.NewGuid():N}";
        string secondValue = $"group-{Guid.NewGuid():N}";

        using HttpResponseMessage firstWriteResponse = await writer.PostAsync($"/groups/{firstDatabaseName}/{firstValue}", content: null, cts.Token);
        Assert.Equal(HttpStatusCode.Created, firstWriteResponse.StatusCode);

        using HttpResponseMessage secondWriteResponse = await writer.PostAsync($"/groups/{secondDatabaseName}/{secondValue}", content: null, cts.Token);
        Assert.Equal(HttpStatusCode.Created, secondWriteResponse.StatusCode);

        await WaitForRestoreAsync(verifier, $"/restore/groups/{firstDatabaseName}", cts.Token);
        await WaitForRestoreAsync(verifier, $"/restore/groups/{secondDatabaseName}", cts.Token);

        await AssertGroupValueAsync(verifier, firstDatabaseName, firstValue, cts.Token);
        await AssertGroupValueAsync(verifier, secondDatabaseName, secondValue, cts.Token);
    }

    private static async Task AssertGroupValueAsync(HttpClient verifier, string databaseName, string expectedValue, CancellationToken cancellationToken)
    {
        using HttpResponseMessage verifyResponse = await verifier.GetAsync($"/groups/{databaseName}", cancellationToken);
        string? verifiedValue = null;

        if (verifyResponse.IsSuccessStatusCode)
        {
            GroupValuePayload? payload = await verifyResponse.Content.ReadFromJsonAsync<GroupValuePayload>(cancellationToken: cancellationToken);
            verifiedValue = payload?.Value;
        }

        Assert.True(
            verifyResponse.StatusCode == HttpStatusCode.OK && verifiedValue == expectedValue,
            $"Expected verifier to observe replicated grouped-database value '{expectedValue}' for '{databaseName}', but got status {(int)verifyResponse.StatusCode} and value '{verifiedValue ?? "<null>"}'.");
    }

    private async Task WaitForHarnessAsync(CancellationToken cancellationToken)
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("minio", cancellationToken).WaitAsync(cancellationToken);
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("writer", cancellationToken).WaitAsync(cancellationToken);
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("verifier", cancellationToken).WaitAsync(cancellationToken);
        await fixture.ResourceNotificationService.WaitForResourceAsync("writer-single-litestream", KnownResourceStates.Running, cancellationToken).WaitAsync(cancellationToken);
        await fixture.ResourceNotificationService.WaitForResourceAsync("writer-group-litestream", KnownResourceStates.Running, cancellationToken).WaitAsync(cancellationToken);
    }

    private static async Task WaitForRestoreAsync(HttpClient verifier, string path, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        HttpStatusCode? lastStatusCode = null;
        string? lastResponseBody = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await verifier.PostAsync(path, content: null, cancellationToken);
            lastStatusCode = response.StatusCode;
            lastResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException(
            $"Timed out waiting for restore endpoint '{path}' to succeed. Last response: {(int?)lastStatusCode} {lastStatusCode}; body: {lastResponseBody ?? "<empty>"}");
    }

    private sealed class HarnessConfiguration
    {
        public required string Role { get; init; }

        public required string StorageRoot { get; init; }

        public required string SingleDatabasePath { get; init; }

        public required string GroupDatabaseDirectory { get; init; }

        public required string ReplicaBucketName { get; init; }

        public required string[] SeededGroupDatabaseNames { get; init; }
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
