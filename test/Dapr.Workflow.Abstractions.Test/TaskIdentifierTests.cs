namespace Dapr.Workflow.Abstractions.Test;

public class TaskIdentifierTests
{
    [Fact]
    public void Implicit_String_To_TaskIdentifier_Works()
    {
        TaskIdentifier id = "my-task";
        Assert.Equal("my-task", id.Name);
    }

    [Fact]
    public void Implicit_TaskIdentifier_To_String_Works()
    {
        var id = new TaskIdentifier("activity-1");
        string s = id;
        Assert.Equal("activity-1", s);
    }

    [Fact]
    public void ToString_Returns_Name()
    {
        var id = new TaskIdentifier("xyz");
        Assert.Equal("xyz", id.ToString());
    }

    [Fact]
    public void Equality_By_Name()
    {
        var a = new TaskIdentifier("same");
        var b = new TaskIdentifier("same");
        var c = new TaskIdentifier("other");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
    }

    [Fact]
    public void Default_TaskIdentifier_Has_Null_Name()
    {
        TaskIdentifier d = default;
        Assert.Null(d.Name);
        Assert.Null(((string)d));
    }
}
