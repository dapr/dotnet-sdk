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

using System;
using System.Collections.Generic;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Tools;
using Google.Protobuf.WellKnownTypes;

namespace Dapr.AI.Test.Conversation;

public class ConversationOptionsTest
{
    [Fact]
    public void ConversationOptions_ShouldSetComponentId()
    {
        var options = new ConversationOptions("my-llm-component");
        Assert.Equal("my-llm-component", options.ConversationComponentId);
    }

    [Fact]
    public void ConversationOptions_DefaultContextId_ShouldBeNull()
    {
        var options = new ConversationOptions("component");
        Assert.Null(options.ContextId);
    }

    [Fact]
    public void ConversationOptions_DefaultTemperature_ShouldBeNull()
    {
        var options = new ConversationOptions("component");
        Assert.Null(options.Temperature);
    }

    [Fact]
    public void ConversationOptions_DefaultScrubPII_ShouldBeNull()
    {
        var options = new ConversationOptions("component");
        Assert.Null(options.ScrubPII);
    }

    [Fact]
    public void ConversationOptions_DefaultMetadata_ShouldBeEmpty()
    {
        var options = new ConversationOptions("component");
        Assert.Empty(options.Metadata);
    }

    [Fact]
    public void ConversationOptions_DefaultParameters_ShouldBeEmpty()
    {
        var options = new ConversationOptions("component");
        Assert.Empty(options.Parameters);
    }

    [Fact]
    public void ConversationOptions_DefaultTools_ShouldBeEmpty()
    {
        var options = new ConversationOptions("component");
        Assert.Empty(options.Tools);
    }

    [Fact]
    public void ConversationOptions_DefaultToolChoice_ShouldBeNull()
    {
        var options = new ConversationOptions("component");
        Assert.Null(options.ToolChoice);
    }

    [Fact]
    public void ConversationOptions_DefaultPromptCacheRetention_ShouldBeNull()
    {
        var options = new ConversationOptions("component");
        Assert.Null(options.PromptCacheRetention);
    }

    [Fact]
    public void ConversationOptions_DefaultResponseFormat_ShouldBeNull()
    {
        var options = new ConversationOptions("component");
        Assert.Null(options.ResponseFormat);
    }

    [Fact]
    public void ConversationOptions_WithContextId_ShouldSetContextId()
    {
        var options = new ConversationOptions("component") { ContextId = "ctx-abc" };
        Assert.Equal("ctx-abc", options.ContextId);
    }

    [Fact]
    public void ConversationOptions_WithTemperature_ShouldSetTemperature()
    {
        var options = new ConversationOptions("component") { Temperature = 0.7 };
        Assert.Equal(0.7, options.Temperature);
    }

    [Fact]
    public void ConversationOptions_WithScrubPIITrue_ShouldSetTrue()
    {
        var options = new ConversationOptions("component") { ScrubPII = true };
        Assert.True(options.ScrubPII);
    }

    [Fact]
    public void ConversationOptions_WithScrubPIIFalse_ShouldSetFalse()
    {
        var options = new ConversationOptions("component") { ScrubPII = false };
        Assert.False(options.ScrubPII);
    }

    [Fact]
    public void ConversationOptions_WithMetadata_ShouldSetMetadata()
    {
        var metadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        var options = new ConversationOptions("component") { Metadata = metadata };

        Assert.Equal(2, options.Metadata.Count);
        Assert.Equal("value1", options.Metadata["key1"]);
        Assert.Equal("value2", options.Metadata["key2"]);
    }

    [Fact]
    public void ConversationOptions_WithToolChoiceNone_ShouldSetNone()
    {
        var options = new ConversationOptions("component") { ToolChoice = ToolChoice.None };

        Assert.NotNull(options.ToolChoice);
        Assert.True(options.ToolChoice!.Value.IsNone);
    }

    [Fact]
    public void ConversationOptions_WithToolChoiceAuto_ShouldSetAuto()
    {
        var options = new ConversationOptions("component") { ToolChoice = ToolChoice.Auto };

        Assert.NotNull(options.ToolChoice);
        Assert.True(options.ToolChoice!.Value.IsAuto);
    }

    [Fact]
    public void ConversationOptions_WithToolChoiceRequired_ShouldSetRequired()
    {
        var options = new ConversationOptions("component") { ToolChoice = ToolChoice.Required };

        Assert.NotNull(options.ToolChoice);
        Assert.True(options.ToolChoice!.Value.IsRequired);
    }

    [Fact]
    public void ConversationOptions_WithNamedToolChoice_ShouldSetValue()
    {
        var options = new ConversationOptions("component") { ToolChoice = new ToolChoice("get_weather") };

        Assert.NotNull(options.ToolChoice);
        Assert.Equal("get_weather", options.ToolChoice!.Value.Value);
    }

    [Fact]
    public void ConversationOptions_WithSingleTool_ShouldSetTools()
    {
        var tool = new ToolFunction("my_func") { Description = "Does something" };
        var options = new ConversationOptions("component") { Tools = [tool] };

        Assert.Single(options.Tools);
        var toolFunction = Assert.IsType<ToolFunction>(options.Tools[0]);
        Assert.Equal("my_func", toolFunction.Name);
    }

    [Fact]
    public void ConversationOptions_WithMultipleTools_ShouldSetAll()
    {
        var tools = new ITool[]
        {
            new ToolFunction("func_a"),
            new ToolFunction("func_b")
        };
        var options = new ConversationOptions("component") { Tools = tools };

        Assert.Equal(2, options.Tools.Count);
    }

    [Fact]
    public void ConversationOptions_WithPromptCacheRetention_ShouldSetRetention()
    {
        var retention = TimeSpan.FromHours(24);
        var options = new ConversationOptions("component") { PromptCacheRetention = retention };

        Assert.Equal(retention, options.PromptCacheRetention);
    }

    [Fact]
    public void ConversationOptions_WithResponseFormat_ShouldSetResponseFormat()
    {
        var responseFormat = new Struct();
        responseFormat.Fields["type"] = Value.ForString("json_schema");
        var options = new ConversationOptions("component") { ResponseFormat = responseFormat };

        Assert.NotNull(options.ResponseFormat);
        Assert.Equal("json_schema", options.ResponseFormat!.Fields["type"].StringValue);
    }

    [Fact]
    public void ConversationOptions_Equality_SameInstanceShouldBeEqual()
    {
        var options = new ConversationOptions("component-a");
        Assert.Equal(options, options);
    }

    [Fact]
    public void ConversationOptions_Equality_DifferentComponentIdsShouldNotBeEqual()
    {
        // Share the same collection instances so record equality only differs on the component ID
        var sharedMetadata = new Dictionary<string, string>();
        var options1 = new ConversationOptions("component-a") { Metadata = sharedMetadata };
        var options2 = new ConversationOptions("component-b") { Metadata = sharedMetadata };
        Assert.NotEqual(options1, options2);
    }
}
