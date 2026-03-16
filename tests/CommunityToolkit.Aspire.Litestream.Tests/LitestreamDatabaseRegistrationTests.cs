using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Litestream.Tests;

public class LitestreamDatabaseRegistrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsSingleDatabasePathCorrectly(bool useKeyed)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(
        [
            new("Aspire:Litestream:Client:Database:primary:DatabasePath", "/data/primary.db")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedLitestreamDatabase("primary");
        }
        else
        {
            builder.AddLitestreamDatabase("primary");
        }

        using IHost host = builder.Build();

        LitestreamDatabaseRegistration registration = useKeyed
            ? host.Services.GetRequiredKeyedService<LitestreamDatabaseRegistration>("primary")
            : host.Services.GetRequiredService<LitestreamDatabaseRegistration>();

        Assert.Equal("primary", registration.Name);
        Assert.Equal("/data/primary.db", registration.DatabasePath);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanOverrideSingleDatabasePathInCode(bool useKeyed)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(
        [
            new("Aspire:Litestream:Client:Database:primary:DatabasePath", "/data/not-used.db")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedLitestreamDatabase("primary", settings => settings.DatabasePath = "/data/override.db");
        }
        else
        {
            builder.AddLitestreamDatabase("primary", settings => settings.DatabasePath = "/data/override.db");
        }

        using IHost host = builder.Build();

        LitestreamDatabaseRegistration registration = useKeyed
            ? host.Services.GetRequiredKeyedService<LitestreamDatabaseRegistration>("primary")
            : host.Services.GetRequiredService<LitestreamDatabaseRegistration>();

        Assert.Equal("/data/override.db", registration.DatabasePath);
    }

    [Fact]
    public void CanSetMultipleKeyedSingleDatabaseRegistrations()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(
        [
            new("Aspire:Litestream:Client:Database:primary:DatabasePath", "/data/primary.db"),
            new("Aspire:Litestream:Client:Database:secondary:DatabasePath", "/data/secondary.db")
        ]);

        builder.AddKeyedLitestreamDatabase("primary");
        builder.AddKeyedLitestreamDatabase("secondary");

        using IHost host = builder.Build();

        LitestreamDatabaseRegistration primary = host.Services.GetRequiredKeyedService<LitestreamDatabaseRegistration>("primary");
        LitestreamDatabaseRegistration secondary = host.Services.GetRequiredKeyedService<LitestreamDatabaseRegistration>("secondary");

        Assert.Equal("/data/primary.db", primary.DatabasePath);
        Assert.Equal("/data/secondary.db", secondary.DatabasePath);
    }

    [Fact]
    public void MissingSingleDatabasePathThrows()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => builder.AddLitestreamDatabase("primary"));

        Assert.Contains("DatabasePath", exception.Message, StringComparison.Ordinal);
        Assert.Contains("primary", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsDatabaseGroupConfigurationCorrectly(bool useKeyed)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(
        [
            new("Aspire:Litestream:Client:Group:tenants:DirectoryPath", "/data/tenants"),
            new("Aspire:Litestream:Client:Group:tenants:Pattern", "*.sqlite"),
            new("Aspire:Litestream:Client:Group:tenants:Recursive", "true"),
            new("Aspire:Litestream:Client:Group:tenants:Watch", "true")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedLitestreamDatabaseGroup("tenants");
        }
        else
        {
            builder.AddLitestreamDatabaseGroup("tenants");
        }

        using IHost host = builder.Build();

        LitestreamDatabaseGroupRegistration registration = useKeyed
            ? host.Services.GetRequiredKeyedService<LitestreamDatabaseGroupRegistration>("tenants")
            : host.Services.GetRequiredService<LitestreamDatabaseGroupRegistration>();

        Assert.Equal("tenants", registration.Name);
        Assert.Equal("/data/tenants", registration.DirectoryPath);
        Assert.Equal("*.sqlite", registration.Pattern);
        Assert.True(registration.Recursive);
        Assert.True(registration.Watch);
        Assert.Equal(Path.Combine("/data/tenants", "tenant-a.db"), registration.ResolveDatabasePath("tenant-a.db"));
    }

    [Fact]
    public void CanOverrideDatabaseGroupConfigurationInCode()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(
        [
            new("Aspire:Litestream:Client:Group:tenants:DirectoryPath", "/data/not-used"),
            new("Aspire:Litestream:Client:Group:tenants:Pattern", "*.db")
        ]);

        builder.AddLitestreamDatabaseGroup(
            "tenants",
            settings =>
            {
                settings.DirectoryPath = "/data/overridden";
                settings.Pattern = "*.sqlite";
                settings.Recursive = true;
                settings.Watch = true;
            });

        using IHost host = builder.Build();

        LitestreamDatabaseGroupRegistration registration = host.Services.GetRequiredService<LitestreamDatabaseGroupRegistration>();

        Assert.Equal("/data/overridden", registration.DirectoryPath);
        Assert.Equal("*.sqlite", registration.Pattern);
        Assert.True(registration.Recursive);
        Assert.True(registration.Watch);
    }

    [Fact]
    public void MissingDatabaseGroupDirectoryThrows()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(
        [
            new("Aspire:Litestream:Client:Group:tenants:Pattern", "*.db")
        ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => builder.AddLitestreamDatabaseGroup("tenants"));

        Assert.Contains("DirectoryPath", exception.Message, StringComparison.Ordinal);
        Assert.Contains("tenants", exception.Message, StringComparison.Ordinal);
    }
}
