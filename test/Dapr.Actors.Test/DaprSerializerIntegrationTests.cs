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
// ------------------------------------------------------------------------

namespace Dapr.Actors.Test;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Dapr.Actors.Communication;
using Dapr.Actors.Runtime;
using Dapr.Common.Serialization;
using Moq;
using Xunit;

/// <summary>
/// Tests for IDaprSerializer integration in Dapr.Actors.
/// </summary>
public class DaprSerializerIntegrationTests
{
    /// <summary>
    /// Verifies that DaprStateProvider uses IDaprSerializer for state serialization when configured.
    /// </summary>
    [Fact]
    public async Task SaveStateAsync_WithDaprSerializer_UsesSerializerForSerialization()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var serializer = new JsonDaprSerializer();
        var provider = new DaprStateProvider(interactor.Object, serializer);
        var token = new CancellationToken();

        var stateChangeList = new List<ActorStateChange>
        {
            new ActorStateChange("key1", typeof(string), "value1", StateChangeKind.Add, null),
            new ActorStateChange("key2", typeof(int), 42, StateChangeKind.Add, null),
        };

        string content = null;
        interactor
            .Setup(d => d.SaveStateTransactionallyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((actorType, actorId, data, t) => content = data)
            .Returns(Task.FromResult(true));

        await provider.SaveStateAsync("actorType", "actorId", stateChangeList, token);

        Assert.NotNull(content);
        Assert.Contains("\"key\":\"key1\"", content);
        Assert.Contains("\"value1\"", content);
        Assert.Contains("\"key\":\"key2\"", content);
        Assert.Contains("42", content);
    }

    /// <summary>
    /// Verifies that DaprStateProvider uses IDaprSerializer for state deserialization when configured.
    /// </summary>
    [Fact]
    public async Task TryLoadStateAsync_WithDaprSerializer_UsesSerializerForDeserialization()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var serializer = new JsonDaprSerializer();
        var provider = new DaprStateProvider(interactor.Object, serializer);
        var token = new CancellationToken();

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("\"hello world\"", null)));

        var resp = await provider.TryLoadStateAsync<string>("actorType", "actorId", "key", token);

        Assert.True(resp.HasValue);
        Assert.Equal("hello world", resp.Value.Value);
    }

    /// <summary>
    /// Verifies that DaprStateProvider returns empty result when data is empty, even with IDaprSerializer.
    /// </summary>
    [Fact]
    public async Task TryLoadStateAsync_WithDaprSerializer_EmptyValue_ReturnsFalse()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var serializer = new JsonDaprSerializer();
        var provider = new DaprStateProvider(interactor.Object, serializer);
        var token = new CancellationToken();

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("", null)));

        var resp = await provider.TryLoadStateAsync<string>("actorType", "actorId", "key", token);

        Assert.False(resp.HasValue);
    }

    /// <summary>
    /// Verifies that ActorProxyOptions.DaprSerializer can be set and retrieved.
    /// </summary>
    [Fact]
    public void ActorProxyOptions_DaprSerializer_CanBeSetAndRetrieved()
    {
        var serializer = new JsonDaprSerializer();
        var options = new ActorProxyOptions
        {
            DaprSerializer = serializer,
        };

        Assert.Same(serializer, options.DaprSerializer);
    }

    /// <summary>
    /// Verifies that ActorProxyOptions.DaprSerializer defaults to null (backwards compatibility).
    /// </summary>
    [Fact]
    public void ActorProxyOptions_DaprSerializer_DefaultsToNull()
    {
        var options = new ActorProxyOptions();
        Assert.Null(options.DaprSerializer);
    }

    /// <summary>
    /// Verifies that ActorRuntimeOptions.DaprSerializer can be set and retrieved.
    /// </summary>
    [Fact]
    public void ActorRuntimeOptions_DaprSerializer_CanBeSetAndRetrieved()
    {
        var serializer = new JsonDaprSerializer();
        var options = new ActorRuntimeOptions
        {
            DaprSerializer = serializer,
        };

        Assert.Same(serializer, options.DaprSerializer);
    }

    /// <summary>
    /// Verifies that ActorRuntimeOptions.DaprSerializer defaults to null (backwards compatibility).
    /// </summary>
    [Fact]
    public void ActorRuntimeOptions_DaprSerializer_DefaultsToNull()
    {
        var options = new ActorRuntimeOptions();
        Assert.Null(options.DaprSerializer);
    }

    /// <summary>
    /// Verifies that ActorTestOptions.DaprSerializer can be set and retrieved.
    /// </summary>
    [Fact]
    public void ActorTestOptions_DaprSerializer_CanBeSetAndRetrieved()
    {
        var serializer = new JsonDaprSerializer();
        var options = new ActorTestOptions
        {
            DaprSerializer = serializer,
        };

        Assert.Same(serializer, options.DaprSerializer);
    }

    /// <summary>
    /// Verifies that ActorTestOptions.DaprSerializer defaults to null (backwards compatibility).
    /// </summary>
    [Fact]
    public void ActorTestOptions_DaprSerializer_DefaultsToNull()
    {
        var options = new ActorTestOptions();
        Assert.Null(options.DaprSerializer);
    }

    /// <summary>
    /// Verifies that ActorHost.DaprSerializer is populated when created via CreateForTest with a DaprSerializer.
    /// </summary>
    [Fact]
    public void ActorHost_CreateForTest_WithDaprSerializer_SetsDaprSerializer()
    {
        var serializer = new JsonDaprSerializer();
        var options = new ActorTestOptions
        {
            DaprSerializer = serializer,
        };

        var host = ActorHost.CreateForTest<TestActor>(options);

        Assert.Same(serializer, host.DaprSerializer);
    }

    /// <summary>
    /// Verifies that ActorHost.DaprSerializer is null when created via CreateForTest without a DaprSerializer (backwards compatibility).
    /// </summary>
    [Fact]
    public void ActorHost_CreateForTest_WithoutDaprSerializer_DaprSerializerIsNull()
    {
        var host = ActorHost.CreateForTest<TestActor>();

        Assert.Null(host.DaprSerializer);
    }

    /// <summary>
    /// Verifies that DaprStateProvider with IDaprSerializer handles delete operations correctly.
    /// </summary>
    [Fact]
    public async Task SaveStateAsync_WithDaprSerializer_Remove_EmitsDeleteOperation()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var serializer = new JsonDaprSerializer();
        var provider = new DaprStateProvider(interactor.Object, serializer);
        var token = new CancellationToken();

        var stateChanges = new List<ActorStateChange>
        {
            new ActorStateChange("key1", typeof(string), null, StateChangeKind.Remove, null),
        };

        string capturedContent = null;
        interactor
            .Setup(d => d.SaveStateTransactionallyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, data, _) => capturedContent = data)
            .Returns(Task.CompletedTask);

        await provider.SaveStateAsync("actorType", "actorId", stateChanges, token);

        Assert.NotNull(capturedContent);
        Assert.Equal(
            "[{\"operation\":\"delete\",\"request\":{\"key\":\"key1\"}}]",
            capturedContent);
    }

    /// <summary>
    /// Verifies that DaprStateProvider with IDaprSerializer handles TTL correctly.
    /// </summary>
    [Fact]
    public async Task SaveStateAsync_WithDaprSerializer_WithTTL_IncludesTTLMetadata()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var serializer = new JsonDaprSerializer();
        var provider = new DaprStateProvider(interactor.Object, serializer);
        var token = new CancellationToken();

        var stateChanges = new List<ActorStateChange>
        {
            new ActorStateChange("key1", typeof(string), "value1", StateChangeKind.Add, DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(60))),
        };

        string capturedContent = null;
        interactor
            .Setup(d => d.SaveStateTransactionallyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, data, _) => capturedContent = data)
            .Returns(Task.CompletedTask);

        await provider.SaveStateAsync("actorType", "actorId", stateChanges, token);

        Assert.NotNull(capturedContent);
        Assert.Contains("\"key\":\"key1\"", capturedContent);
        Assert.Contains("\"value1\"", capturedContent);
        Assert.Contains("\"ttlInSeconds\"", capturedContent);
    }

    /// <summary>
    /// Verifies that ActorProxyFactory creates a non-remoting proxy with IDaprSerializer set when configured.
    /// </summary>
    [Fact]
    public void ActorProxyFactory_Create_WithDaprSerializer_CreatesProxy()
    {
        var serializer = new JsonDaprSerializer();
        var options = new ActorProxyOptions
        {
            DaprSerializer = serializer,
        };

        var factory = new ActorProxyFactory();
        var proxy = factory.Create(new ActorId("test"), "TestActor", options);

        Assert.NotNull(proxy);
    }

    /// <summary>
    /// Verifies that DaprStateProvider handles complex types with IDaprSerializer correctly.
    /// </summary>
    [Fact]
    public async Task SaveStateAsync_WithDaprSerializer_ComplexType_SerializesCorrectly()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var serializer = new JsonDaprSerializer();
        var provider = new DaprStateProvider(interactor.Object, serializer);
        var token = new CancellationToken();

        var complexObj = new TestData { Name = "test", Value = 42 };
        var stateChanges = new List<ActorStateChange>
        {
            new ActorStateChange("key1", typeof(TestData), complexObj, StateChangeKind.Add, null),
        };

        string capturedContent = null;
        interactor
            .Setup(d => d.SaveStateTransactionallyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, data, _) => capturedContent = data)
            .Returns(Task.CompletedTask);

        await provider.SaveStateAsync("actorType", "actorId", stateChanges, token);

        Assert.NotNull(capturedContent);
        Assert.Contains("\"name\"", capturedContent);
        Assert.Contains("\"test\"", capturedContent);
        Assert.Contains("\"value\"", capturedContent);
        Assert.Contains("42", capturedContent);
    }

    /// <summary>
    /// Verifies that TryLoadStateAsync with IDaprSerializer handles complex types.
    /// </summary>
    [Fact]
    public async Task TryLoadStateAsync_WithDaprSerializer_ComplexType_DeserializesCorrectly()
    {
        var interactor = new Mock<TestDaprInteractor>();
        var serializer = new JsonDaprSerializer();
        var provider = new DaprStateProvider(interactor.Object, serializer);
        var token = new CancellationToken();

        interactor
            .Setup(d => d.GetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ActorStateResponse<string>("{\"name\":\"test\",\"value\":42}", null)));

        var resp = await provider.TryLoadStateAsync<TestData>("actorType", "actorId", "key", token);

        Assert.True(resp.HasValue);
        Assert.Equal("test", resp.Value.Value.Name);
        Assert.Equal(42, resp.Value.Value.Value);
    }

    /// <summary>
    /// A simple test data class.
    /// </summary>
    public class TestData
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }
}
