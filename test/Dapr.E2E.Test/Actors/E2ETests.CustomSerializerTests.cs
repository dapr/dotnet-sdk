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
namespace Dapr.E2E.Test
{
    using System;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors;
    using Dapr.Actors.Client;
    using Dapr.E2E.Test.Actors;
    using Xunit;
    using Xunit.Abstractions;

    public class CustomSerializerTests : DaprTestAppLifecycle
    {
        private readonly Lazy<IActorProxyFactory> proxyFactory;
        private IActorProxyFactory ProxyFactory => this.HttpEndpoint == null ? null : this.proxyFactory.Value;

        public CustomSerializerTests(ITestOutputHelper output, DaprTestAppFixture fixture) : base(output, fixture)
        {
            base.Configuration = new DaprRunConfiguration
            {
                UseAppPort = true,
                AppId = "serializerapp",
                AppJsonSerialization = true,
                TargetProject = "./../../../../../test/Dapr.E2E.Test.App/Dapr.E2E.Test.App.csproj"
            };

            this.proxyFactory = new Lazy<IActorProxyFactory>(() =>
            {
                Debug.Assert(this.HttpEndpoint != null);
                return new ActorProxyFactory(new ActorProxyOptions() { 
                    HttpEndpoint = this.HttpEndpoint,
                    JsonSerializerOptions = new JsonSerializerOptions()
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true,
                    },
                    UseJsonSerialization = true,
                });
            });
        }
        
        [Fact]
        public async Task ActorCanSupportCustomSerializer()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var proxy = this.ProxyFactory.CreateActorProxy<ISerializationActor>(ActorId.CreateRandom(), "SerializationActor");

            await ActorRuntimeChecker.WaitForActorRuntimeAsync(this.AppId, this.Output, proxy, cts.Token);

            var payload = new SerializationPayload("hello world")
            {
                Value = JsonSerializer.SerializeToElement(new { foo = "bar" }),
                ExtensionData = new System.Collections.Generic.Dictionary<string, object>()
                {
                    { "baz", "qux" },
                    { "count", 42 },
                }
            };

            var result = await proxy.SendAsync("test", payload, CancellationToken.None);

            Assert.Equal(payload.Message, result.Message);
            Assert.Equal(payload.Value.GetRawText(), result.Value.GetRawText());
            Assert.Equal(payload.ExtensionData.Count, result.ExtensionData.Count);

            foreach (var kvp in payload.ExtensionData)
            {
                Assert.True(result.ExtensionData.TryGetValue(kvp.Key, out var value));
                Assert.Equal(JsonSerializer.Serialize(kvp.Value), JsonSerializer.Serialize(value));
            }
        }

        /// <summary>
        /// This was actually a problem that is why the test exists.
        /// It just checks, if the interface of the actor has more than one method defined,
        /// that if can call it and serialize the payload correctly.
        /// </summary>
        /// <remarks>
        /// More than one methods means here, that in the exact interface must be two methods defined.
        /// That excludes hirachies. 
        /// So <see cref="IPingActor.Ping"/> wouldn't count here, because it's not directly defined in 
        /// <see cref="ISerializationActor"/>. (it's defined in the base of it.)
        /// That why <see cref="ISerializationActor.AnotherMethod(DateTime)"/> was created,
        /// so there are now more then one method.
        /// </remarks>
        [Fact]
        public async Task ActorCanSupportCustomSerializerAndCallMoreThenOneDefinedMethod()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var proxy = this.ProxyFactory.CreateActorProxy<ISerializationActor>(ActorId.CreateRandom(), "SerializationActor");

            await ActorRuntimeChecker.WaitForActorRuntimeAsync(this.AppId, this.Output, proxy, cts.Token);

            var payload = DateTime.MinValue;
            var result = await proxy.AnotherMethod(payload);

            Assert.Equal(payload, result);
        }
    }
}
