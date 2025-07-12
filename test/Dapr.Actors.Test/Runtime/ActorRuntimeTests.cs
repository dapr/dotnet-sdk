// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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
using System.Buffers;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Runtime;
using Microsoft.Extensions.Logging;
using Xunit;
using Dapr.Actors.Client;
using System.Reflection;
using System.Threading;

public sealed class ActorRuntimeTests
{
    private const string RenamedActorTypeName = "MyRenamedActor";
    private const string ParamActorTypeName = "AnotherRenamedActor";
    private readonly ILoggerFactory loggerFactory = new LoggerFactory();
    private readonly ActorActivatorFactory activatorFactory = new DefaultActorActivatorFactory();

    private readonly IActorProxyFactory proxyFactory = ActorProxy.DefaultProxyFactory;

    private interface ITestActor : IActor
    {
    }

    [Fact]
    public void TestInferredActorType()
    {
        var actorType = typeof(TestActor);
            
        var options = new ActorRuntimeOptions();
        options.Actors.RegisterActor<TestActor>();
        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);

        Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);
    }

    [Fact]
    public void TestExplicitActorType()
    {
        var actorType = typeof(RenamedActor);
        var options = new ActorRuntimeOptions();
        options.Actors.RegisterActor<RenamedActor>();
        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);

        Assert.NotEqual(RenamedActorTypeName, actorType.Name);
        Assert.Contains(RenamedActorTypeName, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);
    }

    [Fact]
    public void TestExplicitActorTypeAsParamShouldOverrideInferred()
    {
        var actorType = typeof(TestActor);
        var options = new ActorRuntimeOptions();
        options.Actors.RegisterActor<TestActor>(ParamActorTypeName);
        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);

        Assert.NotEqual(ParamActorTypeName, actorType.Name);
        Assert.Contains(ParamActorTypeName, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);
    }

    [Fact]
    public void TestExplicitActorTypeAsParamShouldOverrideActorAttribute()
    {
        var actorType = typeof(RenamedActor);
        var options = new ActorRuntimeOptions();
        options.Actors.RegisterActor<RenamedActor>(ParamActorTypeName);
        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);

        Assert.NotEqual(ParamActorTypeName, actorType.Name);
        Assert.NotEqual(ParamActorTypeName, actorType.GetCustomAttribute<ActorAttribute>().TypeName);
        Assert.Contains(ParamActorTypeName, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);
    }

    // This tests the change that removed the Activate message from Dapr runtime -> app.
    [Fact]
    public async Task NoActivateMessageFromRuntime()
    {
        var actorType = typeof(MyActor);

        var options = new ActorRuntimeOptions();
        options.Actors.RegisterActor<MyActor>();
        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);
        Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);

        var output = new MemoryStream();
        await runtime.DispatchWithoutRemotingAsync(actorType.Name, "abc", nameof(MyActor.MyMethod), new MemoryStream(), output);
        var text = Encoding.UTF8.GetString(output.ToArray());

        Assert.Equal("\"hi\"", text);
        Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);
    }

    public interface INotRemotedActor : IActor
    {
        Task<string> NoArgumentsAsync();

        Task<string> NoArgumentsWithCancellationAsync(CancellationToken cancellationToken = default);

        Task<string> SingleArgumentAsync(bool arg);

        Task<string> SingleArgumentWithCancellationAsync(bool arg, CancellationToken cancellationToken = default);
    }

    public sealed class NotRemotedActor : Actor, INotRemotedActor
    {
        public NotRemotedActor(ActorHost host)
            : base(host)
        {
        }

        public Task<string> NoArgumentsAsync()
        {
            return Task.FromResult(nameof(NoArgumentsAsync));
        }

        public Task<string> NoArgumentsWithCancellationAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(nameof(NoArgumentsWithCancellationAsync));
        }

        public Task<string> SingleArgumentAsync(bool arg)
        {
            return Task.FromResult(nameof(SingleArgumentAsync));
        }

        public Task<string> SingleArgumentWithCancellationAsync(bool arg, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(nameof(SingleArgumentWithCancellationAsync));
        }
    }

    public async Task<string> InvokeMethod<T>(string methodName, object arg = null) where T : Actor
    {
        var options = new ActorRuntimeOptions();

        options.Actors.RegisterActor<T>();

        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);

        using var input = new MemoryStream();

        if (arg is not null)
        {
            JsonSerializer.Serialize(input, arg);

            input.Seek(0, SeekOrigin.Begin);
        }

        using var output = new MemoryStream();
            
        await runtime.DispatchWithoutRemotingAsync(typeof(T).Name, ActorId.CreateRandom().ToString(), methodName, input, output);

        output.Seek(0, SeekOrigin.Begin);

        return JsonSerializer.Deserialize<string>(output);
    }

    [Fact]
    public async Task NoRemotingMethodWithNoArguments()
    {
        string methodName = nameof(INotRemotedActor.NoArgumentsAsync);
            
        string result = await InvokeMethod<NotRemotedActor>(methodName);

        Assert.Equal(methodName, result);
    }

    [Fact]
    public async Task NoRemotingMethodWithNoArgumentsWithCancellation()
    {
        string methodName = nameof(INotRemotedActor.NoArgumentsWithCancellationAsync);
            
        string result = await InvokeMethod<NotRemotedActor>(methodName);

        Assert.Equal(methodName, result);
    }

    [Fact]
    public async Task NoRemotingMethodWithSingleArgument()
    {
        string methodName = nameof(INotRemotedActor.SingleArgumentAsync);
            
        string result = await InvokeMethod<NotRemotedActor>(methodName, true);

        Assert.Equal(methodName, result);
    }

    [Fact]
    public async Task NoRemotingMethodWithSingleArgumentWithCancellation()
    {
        string methodName = nameof(INotRemotedActor.SingleArgumentWithCancellationAsync);
            
        string result = await InvokeMethod<NotRemotedActor>(methodName, true);

        Assert.Equal(methodName, result);
    }

    [Fact]
    public async Task Actor_UsesCustomActivator()
    {
        var activator = new TestActivator();
        var actorType = typeof(MyActor);

        var options = new ActorRuntimeOptions();
        options.Actors.RegisterActor<MyActor>(options =>
        {
            options.Activator = activator;
        });
        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);
        Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);

        var output = new MemoryStream();
        await runtime.DispatchWithoutRemotingAsync(actorType.Name, "abc", nameof(MyActor.MyMethod), new MemoryStream(), output);

        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.Equal("\"hi\"", text);

        await runtime.DeactivateAsync(actorType.Name, "abc");
        Assert.Equal(1, activator.CreateCallCount);
        Assert.Equal(1, activator.DeleteCallCount);
    }

    [Fact]
    public async Task TestActorSettings()
    {
        var actorType = typeof(TestActor);

        var options = new ActorRuntimeOptions();
        options.Actors.RegisterActor<TestActor>();
        options.ActorIdleTimeout = TimeSpan.FromSeconds(33);
        options.ActorScanInterval = TimeSpan.FromSeconds(44);
        options.DrainOngoingCallTimeout = TimeSpan.FromSeconds(55);
        options.DrainRebalancedActors = true;

        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);

        Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);

        ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
        await runtime.SerializeSettingsAndRegisteredTypes(writer);

        // read back the serialized json
        var array = writer.WrittenSpan.ToArray();
        string s = Encoding.UTF8.GetString(array, 0, array.Length);

        JsonDocument document = JsonDocument.Parse(s);
        JsonElement root = document.RootElement;

        // parse out the entities array 
        JsonElement element = root.GetProperty("entities");
        Assert.Equal(1, element.GetArrayLength());

        JsonElement arrayElement = element[0];
        string actor = arrayElement.GetString();
        Assert.Equal("TestActor", actor);

        // validate the other properties have expected values
        element = root.GetProperty("actorIdleTimeout");
        Assert.Equal(TimeSpan.FromSeconds(33), ConverterUtils.ConvertTimeSpanFromDaprFormat(element.GetString()));

        element = root.GetProperty("actorScanInterval");
        Assert.Equal(TimeSpan.FromSeconds(44), ConverterUtils.ConvertTimeSpanFromDaprFormat(element.GetString()));

        element = root.GetProperty("drainOngoingCallTimeout");
        Assert.Equal(TimeSpan.FromSeconds(55), ConverterUtils.ConvertTimeSpanFromDaprFormat(element.GetString()));

        element = root.GetProperty("drainRebalancedActors");
        Assert.True(element.GetBoolean());

        bool found = root.TryGetProperty("remindersStoragePartitions", out element);
        Assert.False(found, "remindersStoragePartitions should not be serialized");

        JsonElement jsonValue;
        Assert.False(root.GetProperty("reentrancy").TryGetProperty("maxStackDepth", out jsonValue));
    }

    [Fact]
    public async Task TestActorSettingsWithRemindersStoragePartitions()
    {
        var actorType = typeof(TestActor);

        var options = new ActorRuntimeOptions();
        options.Actors.RegisterActor<TestActor>();
        options.RemindersStoragePartitions = 12;

        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);

        Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);

        ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
        await runtime.SerializeSettingsAndRegisteredTypes(writer);

        // read back the serialized json
        var array = writer.WrittenSpan.ToArray();
        string s = Encoding.UTF8.GetString(array, 0, array.Length);

        JsonDocument document = JsonDocument.Parse(s);
        JsonElement root = document.RootElement;

        // parse out the entities array 
        JsonElement element = root.GetProperty("entities");
        Assert.Equal(1, element.GetArrayLength());

        JsonElement arrayElement = element[0];
        string actor = arrayElement.GetString();
        Assert.Equal("TestActor", actor);

        element = root.GetProperty("remindersStoragePartitions");
        Assert.Equal(12, element.GetInt64());
    }

    [Fact]
    public async Task TestActorSettingsWithReentrancy() 
    {
        var actorType = typeof(TestActor);

        var options = new ActorRuntimeOptions();
        options.Actors.RegisterActor<TestActor>();
        options.ActorIdleTimeout = TimeSpan.FromSeconds(33);
        options.ActorScanInterval = TimeSpan.FromSeconds(44);
        options.DrainOngoingCallTimeout = TimeSpan.FromSeconds(55);
        options.DrainRebalancedActors = true;
        options.ReentrancyConfig.Enabled = true;
        options.ReentrancyConfig.MaxStackDepth = 64;

        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);

        Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);

        ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
        await runtime.SerializeSettingsAndRegisteredTypes(writer);

        // read back the serialized json
        var array = writer.WrittenSpan.ToArray();
        string s = Encoding.UTF8.GetString(array, 0, array.Length);

        JsonDocument document = JsonDocument.Parse(s);
        JsonElement root = document.RootElement;

        // parse out the entities array 
        JsonElement element = root.GetProperty("entities");
        Assert.Equal(1, element.GetArrayLength());

        element = root.GetProperty("reentrancy").GetProperty("enabled");
        Assert.True(element.GetBoolean());

        element = root.GetProperty("reentrancy").GetProperty("maxStackDepth");
        Assert.Equal(64, element.GetInt32());
    }

    [Fact]
    public async Task TestActorSettingsWithPerActorConfigurations()
    {
        var actorType = typeof(TestActor);
        var options = new ActorRuntimeOptions();
        options.ActorIdleTimeout = TimeSpan.FromSeconds(33);
        options.ActorScanInterval = TimeSpan.FromSeconds(44);
        options.DrainOngoingCallTimeout = TimeSpan.FromSeconds(55);
        options.DrainRebalancedActors = true;
        options.ReentrancyConfig.Enabled = true;
        options.ReentrancyConfig.MaxStackDepth = 32;
        options.Actors.RegisterActor<TestActor>(options);

        var runtime = new ActorRuntime(options, loggerFactory, activatorFactory, proxyFactory);

        Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);

        ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
        await runtime.SerializeSettingsAndRegisteredTypes(writer);

        // read back the serialized json
        var array = writer.WrittenSpan.ToArray();
        string s = Encoding.UTF8.GetString(array, 0, array.Length);

        JsonDocument document = JsonDocument.Parse(s);
        JsonElement root = document.RootElement;

        JsonElement element = root.GetProperty("entities");
        Assert.Equal(1, element.GetArrayLength());

        element = root.GetProperty("entitiesConfig");
        Assert.Equal(1, element.GetArrayLength());

        var perEntityConfig = element[0];

        element = perEntityConfig.GetProperty("actorIdleTimeout");
        Assert.Equal(TimeSpan.FromSeconds(33), ConverterUtils.ConvertTimeSpanFromDaprFormat(element.GetString()));

        element = perEntityConfig.GetProperty("actorScanInterval");
        Assert.Equal(TimeSpan.FromSeconds(44), ConverterUtils.ConvertTimeSpanFromDaprFormat(element.GetString()));

        element = perEntityConfig.GetProperty("drainOngoingCallTimeout");
        Assert.Equal(TimeSpan.FromSeconds(55), ConverterUtils.ConvertTimeSpanFromDaprFormat(element.GetString()));

        element = perEntityConfig.GetProperty("drainRebalancedActors");
        Assert.True(element.GetBoolean());

        element = root.GetProperty("reentrancy").GetProperty("enabled");
        Assert.True(element.GetBoolean());

        element = root.GetProperty("reentrancy").GetProperty("maxStackDepth");
        Assert.Equal(32, element.GetInt32());
    }

    private sealed class TestActor : Actor, ITestActor
    {
        public TestActor(ActorHost host)
            : base(host)
        {
        }
    }

    [Actor(TypeName = RenamedActorTypeName)]
    private sealed class RenamedActor : Actor, ITestActor
    {
        public RenamedActor(ActorHost host)
            : base(host)
        {
        }
    }

    private interface IAnotherActor : IActor
    {
        public Task<string> MyMethod();
    }

    private sealed class MyActor : Actor, IAnotherActor
    {
        public MyActor(ActorHost host)
            : base(host)
        {
        }

        public Task<string> MyMethod()
        {
            return Task.FromResult("hi");
        }
    }

    private class TestActivator : DefaultActorActivator
    {
        public int CreateCallCount { get; set; }

        public int DeleteCallCount { get; set; }

        public override Task<ActorActivatorState> CreateAsync(ActorHost host)
        {
            CreateCallCount++;;
            return base.CreateAsync(host);
        }

        public override Task DeleteAsync(ActorActivatorState state)
        {
            DeleteCallCount++;
            return base.DeleteAsync(state);
        }
    }
}