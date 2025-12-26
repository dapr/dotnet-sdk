using System.Reflection;
using Dapr.Workflow.Abstractions.Attributes;

namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowAttributeTests
{
    [Fact]
    public void DefaultCtor_Sets_Name_Null()
    {
        var attr = new WorkflowAttribute();
        Assert.Null(attr.Name);
    }

    [Fact]
    public void NamedCtor_Sets_Name()
    {
        var attr = new WorkflowAttribute("MyWorkflow");
        Assert.Equal("MyWorkflow", attr.Name);
    }

    [Fact]
    public void AttributeUsage_Is_Class_Only_NotInherited_NotMultiple()
    {
        var usage = typeof(WorkflowAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }
}
