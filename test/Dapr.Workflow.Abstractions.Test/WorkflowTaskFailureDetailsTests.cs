using Dapr.Workflow;

namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowTaskFailureDetailsTests
{
    [Fact]
    public void Constructor_Sets_Properties()
    {
        var details = new WorkflowTaskFailureDetails(typeof(InvalidOperationException).FullName!, "boom", "stack");
        Assert.Equal(typeof(InvalidOperationException).FullName, details.ErrorType);
        Assert.Equal("boom", details.ErrorMessage);
        Assert.Equal("stack", details.StackTrace);
        Assert.Equal($"{typeof(InvalidOperationException).FullName}: boom", details.ToString());
    }

    [Fact]
    public void ErrorType_Null_Throws_ArgumentNullException()
    {
        var details = new WorkflowTaskFailureDetails(null!, "msg");
        var ex = Assert.Throws<ArgumentNullException>(() => _ = details.ErrorType);
        // Just validate the exception type; implementation details of message/param are not part of API contract.
        Assert.IsType<ArgumentNullException>(ex);
    }

    [Fact]
    public void ErrorMessage_Null_Throws_ArgumentNullException()
    {
        var details = new WorkflowTaskFailureDetails(typeof(Exception).FullName!, null!);
        Assert.Throws<ArgumentNullException>(() => _ = details.ErrorMessage);
    }

    [Fact]
    public void IsCausedBy_Returns_True_For_Exact_And_Base_Type()
    {
        var details = new WorkflowTaskFailureDetails(typeof(InvalidOperationException).FullName!, "boom");
        Assert.True(details.IsCausedBy<InvalidOperationException>());
        Assert.True(details.IsCausedBy<Exception>());
    }

    [Fact]
    public void IsCausedBy_Returns_False_For_Unrelated_Type()
    {
        var details = new WorkflowTaskFailureDetails(typeof(InvalidOperationException).FullName!, "boom");
        Assert.False(details.IsCausedBy<ArgumentException>());
    }

    [Fact]
    public void IsCausedBy_Returns_False_When_Type_Not_Resolvable()
    {
        var details = new WorkflowTaskFailureDetails("Not.A.Real.TypeName, NotARealAssembly", "boom");
        Assert.False(details.IsCausedBy<Exception>());
    }

    [Fact]
    public void IsCausedBy_Catches_When_ErrorType_Getter_Throws()
    {
        var details = new WorkflowTaskFailureDetails(null!, "boom");
        // Access through IsCausedBy should catch and return false
        Assert.False(details.IsCausedBy<Exception>());
    }

    [Fact]
    public void FromException_Produces_Details()
    {
        InvalidOperationException original;
        try
        {
            throw new InvalidOperationException("oops");
        }
        catch (InvalidOperationException ex)
        {
            original = ex;
        }
        var details = typeof(WorkflowTaskFailureDetails)
            .GetMethod("FromException", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { original }) as WorkflowTaskFailureDetails;

        Assert.NotNull(details);
        Assert.Equal(typeof(InvalidOperationException).FullName, details!.ErrorType);
        Assert.Equal("oops", details.ErrorMessage);
        Assert.False(string.IsNullOrEmpty(details.StackTrace));
        Assert.Contains(nameof(FromException_Produces_Details), details.StackTrace!);
    }
}
