// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.Workflow.Client;
using Dapr.Workflow.Serialization;

namespace Dapr.Workflow.Test.Client;

public class WorkflowMetadataTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", true)]
    [InlineData("instance-1", true)]
    public void Exists_ShouldBeTrueOnlyWhenInstanceIdIsNotNullOrEmpty(string? instanceId, bool expected)
    {
        var metadata = new WorkflowMetadata(
            instanceId ?? string.Empty,
            "workflow",
            WorkflowRuntimeStatus.Running,
            DateTime.MinValue,
            DateTime.MinValue,
            new CapturingSerializer());

        Assert.Equal(expected, metadata.Exists);
    }

    [Fact]
    public void ReadInputAs_ShouldReturnDefault_WhenSerializedInputIsNull()
    {
        var serializer = new CapturingSerializer();
        var metadata = new WorkflowMetadata(
            "i",
            "w",
            WorkflowRuntimeStatus.Running,
            DateTime.MinValue,
            DateTime.MinValue,
            serializer)
        {
            SerializedInput = null
        };

        var value = metadata.ReadInputAs<int>();

        Assert.Equal(default, value);
        Assert.Equal(0, serializer.GenericDeserializeCallCount);
    }

    [Fact]
    public void ReadInputAs_ShouldReturnDefault_WhenSerializedInputIsEmpty()
    {
        var serializer = new CapturingSerializer();
        var metadata = new WorkflowMetadata(
            "i",
            "w",
            WorkflowRuntimeStatus.Running,
            DateTime.MinValue,
            DateTime.MinValue,
            serializer)
        {
            SerializedInput = ""
        };

        var value = metadata.ReadInputAs<TestPayload>();

        Assert.Null(value);
        Assert.Equal(0, serializer.GenericDeserializeCallCount);
    }

    [Fact]
    public void ReadInputAs_ShouldUseSerializer_WhenSerializedInputIsPresent()
    {
        var expected = new TestPayload { Value = "from-input" };
        var serializer = new CapturingSerializer { NextGenericResult = expected };

        var metadata = new WorkflowMetadata(
            "i",
            "w",
            WorkflowRuntimeStatus.Running,
            DateTime.MinValue,
            DateTime.MinValue,
            serializer)
        {
            SerializedInput = "{\"value\":\"from-input\"}"
        };

        var value = metadata.ReadInputAs<TestPayload>();

        Assert.Same(expected, value);
        Assert.Equal(1, serializer.GenericDeserializeCallCount);
        Assert.Equal("{\"value\":\"from-input\"}", serializer.LastGenericDeserializeData);
    }

    [Fact]
    public void ReadOutputAs_ShouldReturnDefault_WhenSerializedOutputIsNullOrEmpty()
    {
        var serializer = new CapturingSerializer();
        var metadataNull = new WorkflowMetadata("i", "w", WorkflowRuntimeStatus.Running, DateTime.MinValue, DateTime.MinValue, serializer)
        {
            SerializedOutput = null
        };
        var metadataEmpty = metadataNull with { SerializedOutput = "" };

        Assert.Equal(default, metadataNull.ReadOutputAs<int>());
        Assert.Equal(default, metadataEmpty.ReadOutputAs<int>());
        Assert.Equal(0, serializer.GenericDeserializeCallCount);
    }

    [Fact]
    public void ReadCustomStatusAs_ShouldReturnDefault_WhenSerializedCustomStatusIsNullOrEmpty()
    {
        var serializer = new CapturingSerializer();
        var metadataNull = new WorkflowMetadata("i", "w", WorkflowRuntimeStatus.Running, DateTime.MinValue, DateTime.MinValue, serializer)
        {
            SerializedCustomStatus = null
        };
        var metadataEmpty = metadataNull with { SerializedCustomStatus = "" };

        Assert.Null(metadataNull.ReadCustomStatusAs<TestPayload>());
        Assert.Null(metadataEmpty.ReadCustomStatusAs<TestPayload>());
        Assert.Equal(0, serializer.GenericDeserializeCallCount);
    }

    private sealed class TestPayload
    {
        public string? Value { get; set; }
    }

    private sealed class CapturingSerializer : IWorkflowSerializer
    {
        public int GenericDeserializeCallCount { get; private set; }
        public string? LastGenericDeserializeData { get; private set; }
        public object? NextGenericResult { get; set; }

        public string Serialize(object? value, Type? inputType = null) => throw new NotSupportedException();

        public T? Deserialize<T>(string? data)
        {
            GenericDeserializeCallCount++;
            LastGenericDeserializeData = data;
            return (T?)NextGenericResult;
        }

        public object? Deserialize(string? data, Type returnType) => throw new NotSupportedException();
    }
}
