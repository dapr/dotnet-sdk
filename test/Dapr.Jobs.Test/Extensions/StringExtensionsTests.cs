using System.Collections.Generic;
using Dapr.Jobs.Extensions;
using Xunit;

namespace Dapr.Jobs.Test.Extensions;

public class StringExtensionsTests
{
    [Fact]
    public void EndsWithAny_ContainsMatch()
    {
        const string testValue = "@weekly";
        var result = testValue.EndsWithAny(new List<string>
        {
            "every",
            "monthly",
            "weekly",
            "daily",
            "midnight",
            "hourly"
        });
        Assert.True(result);
    }

    [Fact]
    public void EndsWithAny_DoesNotContainMatch()
    {
        const string testValue = "@weekly";
        var result = testValue.EndsWithAny(new List<string> { "every", "monthly", "daily", "midnight", "hourly" });
        Assert.False(result);
    }
}
