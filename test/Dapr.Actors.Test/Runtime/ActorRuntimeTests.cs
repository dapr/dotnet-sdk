// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
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

    public sealed class ActorRuntimeTests
    {
        private const string RenamedActorTypeName = "MyRenamedActor";
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
}
