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

using Dapr.AI.Conversation.Tools;

namespace Dapr.AI.Test.Conversation.Tools;

public class ToolChoiceTest
{
    [Fact]
    public void None_ShouldHaveValueOfNone()
    {
        Assert.Equal("none", ToolChoice.None.Value);
    }

    [Fact]
    public void None_IsNone_ShouldBeTrue()
    {
        Assert.True(ToolChoice.None.IsNone);
    }

    [Fact]
    public void None_IsAuto_ShouldBeFalse()
    {
        Assert.False(ToolChoice.None.IsAuto);
    }

    [Fact]
    public void None_IsRequired_ShouldBeFalse()
    {
        Assert.False(ToolChoice.None.IsRequired);
    }

    [Fact]
    public void None_ToString_ShouldReturnNone()
    {
        Assert.Equal("none", ToolChoice.None.ToString());
    }

    [Fact]
    public void Auto_ShouldHaveValueOfAuto()
    {
        Assert.Equal("auto", ToolChoice.Auto.Value);
    }

    [Fact]
    public void Auto_IsAuto_ShouldBeTrue()
    {
        Assert.True(ToolChoice.Auto.IsAuto);
    }

    [Fact]
    public void Auto_IsNone_ShouldBeFalse()
    {
        Assert.False(ToolChoice.Auto.IsNone);
    }

    [Fact]
    public void Auto_IsRequired_ShouldBeFalse()
    {
        Assert.False(ToolChoice.Auto.IsRequired);
    }

    [Fact]
    public void Auto_ToString_ShouldReturnAuto()
    {
        Assert.Equal("auto", ToolChoice.Auto.ToString());
    }

    [Fact]
    public void Required_ShouldHaveValueOfRequired()
    {
        Assert.Equal("required", ToolChoice.Required.Value);
    }

    [Fact]
    public void Required_IsRequired_ShouldBeTrue()
    {
        Assert.True(ToolChoice.Required.IsRequired);
    }

    [Fact]
    public void Required_IsNone_ShouldBeFalse()
    {
        Assert.False(ToolChoice.Required.IsNone);
    }

    [Fact]
    public void Required_IsAuto_ShouldBeFalse()
    {
        Assert.False(ToolChoice.Required.IsAuto);
    }

    [Fact]
    public void Required_ToString_ShouldReturnRequired()
    {
        Assert.Equal("required", ToolChoice.Required.ToString());
    }

    [Fact]
    public void Constructor_WithCustomToolName_ShouldSetValue()
    {
        var toolChoice = new ToolChoice("my_tool");
        Assert.Equal("my_tool", toolChoice.Value);
    }

    [Fact]
    public void Constructor_WithCustomToolName_IsNone_ShouldBeFalse()
    {
        var toolChoice = new ToolChoice("my_tool");
        Assert.False(toolChoice.IsNone);
    }

    [Fact]
    public void Constructor_WithCustomToolName_IsAuto_ShouldBeFalse()
    {
        var toolChoice = new ToolChoice("my_tool");
        Assert.False(toolChoice.IsAuto);
    }

    [Fact]
    public void Constructor_WithCustomToolName_IsRequired_ShouldBeFalse()
    {
        var toolChoice = new ToolChoice("my_tool");
        Assert.False(toolChoice.IsRequired);
    }

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        var toolChoice = new ToolChoice("some_tool");
        Assert.Equal("some_tool", toolChoice.ToString());
    }

    [Fact]
    public void IsNone_ShouldBeCaseInsensitive()
    {
        var toolChoice = new ToolChoice("NONE");
        Assert.True(toolChoice.IsNone);
    }

    [Fact]
    public void IsAuto_ShouldBeCaseInsensitive()
    {
        var toolChoice = new ToolChoice("AUTO");
        Assert.True(toolChoice.IsAuto);
    }

    [Fact]
    public void IsRequired_ShouldBeCaseInsensitive()
    {
        var toolChoice = new ToolChoice("REQUIRED");
        Assert.True(toolChoice.IsRequired);
    }
}
