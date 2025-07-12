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

using System;
using System.Text.Json;
using Grpc.Net.Client;

namespace Dapr.Jobs.Test;

public class DaprJobsClientBuilderTest
{
    [Fact]
    public void DaprClientBuilder_UsesPropertyNameCaseHandlingInsensitiveByDefault()
    {
        DaprJobsClientBuilder builder = new DaprJobsClientBuilder();
        Assert.True(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void DaprJobsClientBuilder_UsesPropertyNameCaseHandlingAsSpecified()
    {
        var builder = new DaprJobsClientBuilder();
        builder.UseJsonSerializationOptions(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false
        });
        Assert.False(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void DaprJobsClientBuilder_UsesThrowOperationCanceledOnCancellation_ByDefault()
    {
        var builder = new DaprJobsClientBuilder();
        var daprClient = builder.Build();
        Assert.True(builder.GrpcChannelOptions.ThrowOperationCanceledOnCancellation);
    }

    [Fact]
    public void DaprJobsClientBuilder_DoesNotOverrideUserGrpcChannelOptions()
    {
        var builder = new DaprJobsClientBuilder();
        var daprClient = builder.UseGrpcChannelOptions(new GrpcChannelOptions()).Build();
        Assert.False(builder.GrpcChannelOptions.ThrowOperationCanceledOnCancellation);
    }

    [Fact]
    public void DaprJobsClientBuilder_ValidatesGrpcEndpointScheme()
    {
        var builder = new DaprJobsClientBuilder();
        builder.UseGrpcEndpoint("ftp://example.com");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("The gRPC endpoint must use http or https.", ex.Message);
    }

    [Fact]
    public void DaprJobsClientBuilder_ValidatesHttpEndpointScheme()
    {
        var builder = new DaprJobsClientBuilder();
        builder.UseHttpEndpoint("ftp://example.com");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("The HTTP endpoint must use http or https.", ex.Message);
    }

    [Fact]
    public void DaprJobsClientBuilder_SetsApiToken()
    {
        var builder = new DaprJobsClientBuilder();
        builder.UseDaprApiToken("test_token");
        builder.Build();
        Assert.Equal("test_token", builder.DaprApiToken);
    }

    [Fact]
    public void DaprJobsClientBuilder_SetsNullApiToken()
    {
        var builder = new DaprJobsClientBuilder();
        builder.UseDaprApiToken(null);
        builder.Build();
        Assert.Null(builder.DaprApiToken);
    }

    [Fact]
    public void DaprJobsClientBuilder_ApiTokenSet_SetsApiTokenHeader()
    {
        var builder = new DaprJobsClientBuilder();
        builder.UseDaprApiToken("test_token");

        var entry = DaprJobsClient.GetDaprApiTokenHeader(builder.DaprApiToken);
        Assert.NotNull(entry);
        Assert.Equal("test_token", entry.Value.Value);
    }

    [Fact]
    public void DaprJobsClientBuilder_ApiTokenNotSet_EmptyApiTokenHeader()
    {
        var builder = new DaprJobsClientBuilder();
        var entry = DaprJobsClient.GetDaprApiTokenHeader(builder.DaprApiToken);
        Assert.Equal(default, entry);
    }

    [Fact]
    public void DaprJobsClientBuilder_SetsTimeout()
    {
        var builder = new DaprJobsClientBuilder();
        builder.UseTimeout(TimeSpan.FromSeconds(2));
        builder.Build();
        Assert.Equal(2, builder.Timeout.Seconds);
    }
}
