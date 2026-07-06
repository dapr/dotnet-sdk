// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Dapr.Messaging.PublishSubscribe;

namespace Dapr.Messaging.Test.PublishSubscribe;

public class DaprPublishSubscribeClientBuilderTests
{
    /// <summary>
    /// Path 1: The gRPC endpoint scheme is neither "http" nor "https" — Build() must throw.
    /// </summary>
    [Fact]
    public void Build_InvalidGrpcScheme_ThrowsInvalidOperationException()
    {
        var builder = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint("ftp://localhost:50001");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("gRPC endpoint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Path 2: gRPC scheme is "http" (valid) but the HTTP endpoint scheme is invalid — Build() must throw.
    /// As a side-effect the Http2UnencryptedSupport AppContext switch is set before the second check is reached.
    /// </summary>
    [Fact]
    public void Build_HttpGrpcEndpointAndInvalidHttpScheme_ThrowsInvalidOperationException()
    {
        var builder = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint("http://localhost:50001")
            .UseHttpEndpoint("ftp://localhost:3500");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("HTTP endpoint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Path 3: gRPC scheme is "https" (valid, no AppContext switch) but the HTTP endpoint scheme is invalid —
    /// Build() must throw.
    /// </summary>
    [Fact]
    public void Build_HttpsGrpcEndpointAndInvalidHttpScheme_ThrowsInvalidOperationException()
    {
        var builder = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint("https://localhost:50001")
            .UseHttpEndpoint("ftp://localhost:3500");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("HTTP endpoint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Path 4: gRPC scheme is "http" and HTTP endpoint scheme is valid — Build() succeeds,
    /// returns a DaprPublishSubscribeGrpcClient, and sets the Http2UnencryptedSupport AppContext switch.
    /// </summary>
    [Fact]
    public void Build_HttpGrpcEndpointAndValidHttpEndpoint_ReturnsDaprPublishSubscribeGrpcClient()
    {
        var builder = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint("http://localhost:50001")
            .UseHttpEndpoint("http://localhost:3500");

        var client = builder.Build();

        Assert.NotNull(client);
        Assert.IsType<DaprPublishSubscribeGrpcClient>(client);
        Assert.True(
            AppContext.TryGetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", out var enabled) && enabled,
            "The Http2UnencryptedSupport switch must be enabled for plain-text gRPC.");
    }

    /// <summary>
    /// Path 5: gRPC scheme is "https" and HTTP endpoint scheme is valid — Build() succeeds and returns a
    /// DaprPublishSubscribeGrpcClient without setting the Http2UnencryptedSupport switch.
    /// </summary>
    [Fact]
    public void Build_HttpsGrpcEndpointAndValidHttpsEndpoint_ReturnsDaprPublishSubscribeGrpcClient()
    {
        var builder = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint("https://localhost:50001")
            .UseHttpEndpoint("https://localhost:3500");

        var client = builder.Build();

        Assert.NotNull(client);
        Assert.IsType<DaprPublishSubscribeGrpcClient>(client);
    }
}
