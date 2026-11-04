using Terrace.Server.Hosting;

namespace Terrace.Server.Tests;

public class AdminKeyTests
{
    [Fact]
    public void 同じ鍵だけを通す()
    {
        Assert.True(AdminKey.Matches("abc123", "abc123"));
        Assert.False(AdminKey.Matches("abc124", "abc123"));
        Assert.False(AdminKey.Matches("abc12", "abc123"));
        Assert.False(AdminKey.Matches("ABC123", "abc123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 鍵の設定が空なら何を渡されても通さない(string? expected)
    {
        Assert.False(AdminKey.Matches("", expected));
        Assert.False(AdminKey.Matches(null, expected));
        Assert.False(AdminKey.Matches("anything", expected));
    }

    [Fact]
    public void 鍵を渡されなければ通さない()
    {
        Assert.False(AdminKey.Matches(null, "abc123"));
        Assert.False(AdminKey.Matches("", "abc123"));
    }
}
