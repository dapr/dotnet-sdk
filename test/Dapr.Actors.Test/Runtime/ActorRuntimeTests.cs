﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Actors;
    using Dapr.Actors.Runtime;
    using Xunit;
    using Microsoft.Extensions.Logging;
    using System.Linq;
    using System.Reflection;

    public sealed class ActorRuntimeTests
    {
        private const string RenamedActorTypeName = "MyRenamedActor";
        private ILoggerFactory loggerFactory = new LoggerFactory();
        private ActorActivatorFactory activatorFactory = new DefaultActorActivatorFactory();

        private interface ITestActor : IActor
        {
        }

        [Fact]
        public void TestInferredActorType()
        {
            var actorType = typeof(TestActor);
            
            var options = new ActorRuntimeOptions();
            options.Actors.RegisterActor<TestActor>();
            var runtime = new ActorRuntime(options, loggerFactory, activatorFactory);

            Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);
        }

        [Fact]
        public void TestExplicitActorType()
        {
            var actorType = typeof(RenamedActor);
            var options = new ActorRuntimeOptions();
            options.Actors.RegisterActor<RenamedActor>();
            var runtime = new ActorRuntime(options, loggerFactory, activatorFactory);

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
            var runtime = new ActorRuntime(options, loggerFactory, activatorFactory);
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
            var runtime = new ActorRuntime(options, loggerFactory, activatorFactory);
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
        public void TestActorSettings()
        {
            var actorType = typeof(TestActor);

            var options = new ActorRuntimeOptions();
            options.Actors.RegisterActor<TestActor>();
            options.ActorIdleTimeout = TimeSpan.FromSeconds(33);
            options.ActorScanInterval = TimeSpan.FromSeconds(44);
            options.DrainOngoingCallTimeout = TimeSpan.FromSeconds(55);
            options.DrainRebalancedActors = true;

            var runtime = new ActorRuntime(options, loggerFactory, activatorFactory);

            Assert.Contains(actorType.Name, runtime.RegisteredActors.Select(a => a.Type.ActorTypeName), StringComparer.InvariantCulture);

            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
            runtime.SerializeSettingsAndRegisteredTypes(writer).GetAwaiter().GetResult();

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
        }

        private sealed class TestActor : Actor, ITestActor
        {
            public TestActor(ActorHost actorService)
                : base(actorService)
            {
            }
        }

        [Actor(TypeName = RenamedActorTypeName)]
        private sealed class RenamedActor : Actor, ITestActor
        {
            public RenamedActor(ActorHost actorService)
                : base(actorService)
            {
            }
        }

        private interface IAnotherActor : IActor
        {
            public Task<string> MyMethod();
        }

        private sealed class MyActor : Actor, IAnotherActor
        {
            public MyActor(ActorHost actorService)
                : base(actorService)
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

            public override Task<ActorActivatorState> CreateAsync(ActorHost service)
            {
                CreateCallCount++;;
                return base.CreateAsync(service);
            }

            public override ValueTask DeleteAsync(ActorActivatorState state)
            {
                DeleteCallCount++;
                return base.DeleteAsync(state);
            }
        }
    }
}
