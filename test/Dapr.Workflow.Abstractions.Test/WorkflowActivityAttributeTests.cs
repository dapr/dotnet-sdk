// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Reflection;
using Dapr.Workflow.Abstractions.Attributes;

namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowActivityAttributeTests
{
    [Fact]
    public void DefaultCtor_Sets_Name_Null()
    {
        var attr = new WorkflowActivityAttribute();
        Assert.Null(attr.Name);
    }

    [Fact]
    public void NamedCtor_Sets_Name()
    {
        var attr = new WorkflowActivityAttribute("MyActivity");
        Assert.Equal("MyActivity", attr.Name);
    }

    [Fact]
    public void AttributeUsage_Is_Class_Only_NotInherited_NotMultiple()
    {
        var usage = typeof(WorkflowActivityAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }
}
