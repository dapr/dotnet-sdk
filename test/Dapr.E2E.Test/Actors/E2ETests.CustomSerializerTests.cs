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
    }
}
