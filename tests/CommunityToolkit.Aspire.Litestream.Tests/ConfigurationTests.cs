using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Litestream.Tests;

public class ConfigurationTests
{
    [Fact]
    public void DatabasePathIsNullByDefault() =>
        Assert.Null(new LitestreamDatabaseSettings().DatabasePath);

    [Fact]
    public void GroupPatternDefaultsToDbGlob() =>
        Assert.Equal("*.db", new LitestreamDatabaseGroupSettings().Pattern);

    [Fact]
    public void GroupRecursiveIsDisabledByDefault() =>
        Assert.False(new LitestreamDatabaseGroupSettings().Recursive);

    [Fact]
    public void GroupWatchIsDisabledByDefault() =>
        Assert.False(new LitestreamDatabaseGroupSettings().Watch);
}
