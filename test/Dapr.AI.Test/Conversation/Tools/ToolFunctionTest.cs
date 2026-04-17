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

#nullable enable
using System.Collections.Generic;
using Dapr.AI.Conversation.Tools;

namespace Dapr.AI.Test.Conversation.Tools;

public class ToolFunctionTest
{
    [Fact]
    public void ToolFunction_ShouldSetName()
    {
        var tool = new ToolFunction("my_function");
        Assert.Equal("my_function", tool.Name);
    }

    [Fact]
    public void ToolFunction_DefaultDescription_ShouldBeNull()
    {
        var tool = new ToolFunction("my_function");
        Assert.Null(tool.Description);
    }

    [Fact]
    public void ToolFunction_DefaultParameters_ShouldBeEmpty()
    {
        var tool = new ToolFunction("my_function");
        Assert.Empty(tool.Parameters);
    }

    [Fact]
    public void ToolFunction_WithDescription_ShouldSetDescription()
    {
        var tool = new ToolFunction("my_function") { Description = "Does something useful" };
        Assert.Equal("Does something useful", tool.Description);
    }

    [Fact]
    public void ToolFunction_WithParameters_ShouldSetParameters()
    {
        var parameters = new Dictionary<string, object?> { { "city", "string" }, { "units", "string" } };
        var tool = new ToolFunction("get_weather") { Parameters = parameters };

        Assert.Equal(2, tool.Parameters.Count);
        Assert.Equal("string", tool.Parameters["city"]);
        Assert.Equal("string", tool.Parameters["units"]);
    }

    [Fact]
    public void ToolFunction_WithNullParameterValue_ShouldSetNull()
    {
        var parameters = new Dictionary<string, object?> { { "optional_param", null } };
        var tool = new ToolFunction("my_function") { Parameters = parameters };

        Assert.Single(tool.Parameters);
        Assert.Null(tool.Parameters["optional_param"]);
    }

    [Fact]
    public void ToolFunction_ImplementsITool()
    {
        var tool = new ToolFunction("my_function");
        Assert.IsAssignableFrom<ITool>(tool);
    }

    [Fact]
    public void ToolFunction_Equality_SameInstanceShouldBeEqual()
    {
        var tool = new ToolFunction("my_function");
        Assert.Equal(tool, tool);
    }

    [Fact]
    public void ToolFunction_Equality_DifferentNamesShouldNotBeEqual()
    {
        var sharedParams = new Dictionary<string, object?>();
        var tool1 = new ToolFunction("function_a") { Parameters = sharedParams };
        var tool2 = new ToolFunction("function_b") { Parameters = sharedParams };
        Assert.NotEqual(tool1, tool2);
    }
}
