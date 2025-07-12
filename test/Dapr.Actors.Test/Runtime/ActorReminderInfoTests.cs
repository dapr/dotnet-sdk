using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Dapr.Actors.Runtime;

public class ActorReminderInfoTests
{
    [Fact]
    public async Task TestActorReminderInfo_SerializeExcludesNullTtl()
    {
        var info = new ReminderInfo(new byte[] { }, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), 1);
        var serialized = await info.SerializeAsync();

        Assert.DoesNotContain("ttl", serialized);
        var info2 = await ReminderInfo.DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(serialized)));
        Assert.Null(info2.Ttl);
    }

    [Fact]
    public async Task TestActorReminderInfo_SerializeIncludesTtlWhenSet()
    {
        var info = new ReminderInfo(new byte[] { }, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), 1, TimeSpan.FromSeconds(1));
        var serialized = await info.SerializeAsync();

        Assert.Contains("ttl", serialized);
        var info2 = await ReminderInfo.DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(serialized)));
        Assert.NotNull(info2.Ttl);
        Assert.Equal(TimeSpan.FromSeconds(1), info2.Ttl);
    }
}