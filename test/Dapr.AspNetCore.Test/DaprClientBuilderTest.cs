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

using System;
using System.Text.Json;
using Dapr.Client;
using Grpc.Net.Client;
using Xunit;

namespace Dapr.AspNetCore.Test;

public class DaprClientBuilderTest
{
    [Fact]
    public void DaprClientBuilder_UsesPropertyNameCaseHandlingInsensitiveByDefault()
    {
        DaprClientBuilder builder = new DaprClientBuilder();
        Assert.True(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void DaprClientBuilder_UsesPropertyNameCaseHandlingAsSpecified()
    {
        var builder = new DaprClientBuilder();
        builder.UseJsonSerializationOptions(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false
        });
        Assert.False(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void DaprClientBuilder_UsesThrowOperationCanceledOnCancellation_ByDefault()
    {
        var builder = new DaprClientBuilder();
            
        Assert.True(builder.GrpcChannelOptions.ThrowOperationCanceledOnCancellation);
    }

    [Fact]
    public void DaprClientBuilder_DoesNotOverrideUserGrpcChannelOptions()
    {
        var builder = new DaprClientBuilder();
        builder.UseGrpcChannelOptions(new GrpcChannelOptions()).Build();
        Assert.False(builder.GrpcChannelOptions.ThrowOperationCanceledOnCancellation);
    }

    [Fact]
    public void DaprClientBuilder_ValidatesGrpcEndpointScheme()
    {
        var builder = new DaprClientBuilder();
        builder.UseGrpcEndpoint("ftp://example.com");
            
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("The gRPC endpoint must use http or https.", ex.Message);
    }

    [Fact]
    public void DaprClientBuilder_ValidatesHttpEndpointScheme()
    {
        var builder = new DaprClientBuilder();
        builder.UseHttpEndpoint("ftp://example.com");
            
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("The HTTP endpoint must use http or https.", ex.Message);
    }

    [Fact]
    public void DaprClientBuilder_SetsApiToken()
    {
        var builder = new DaprClientBuilder();
        builder.UseDaprApiToken("test_token");
        builder.Build();
        Assert.Equal("test_token", builder.DaprApiToken);
    }

    [Fact]
    public void DaprClientBuilder_SetsNullApiToken()
    {
        var builder = new DaprClientBuilder();
        builder.UseDaprApiToken(null);
        builder.Build();
        Assert.Null(builder.DaprApiToken);
    }

    [Fact]
    public void DaprClientBuilder_ApiTokenSet_SetsApiTokenHeader()
    {
        var builder = new DaprClientBuilder();
        builder.UseDaprApiToken("test_token");
        var entry = DaprClient.GetDaprApiTokenHeader(builder.DaprApiToken);
        Assert.NotNull(entry);
        Assert.Equal("test_token", entry.Value.Value);
    }

    [Fact]
    public void DaprClientBuilder_ApiTokenNotSet_EmptyApiTokenHeader()
    {
        var builder = new DaprClientBuilder();
        var entry = DaprClient.GetDaprApiTokenHeader(builder.DaprApiToken);
        Assert.Equal(default, entry);
    }

    [Fact]
    public void DaprClientBuilder_SetsTimeout()
    {
        var builder = new DaprClientBuilder();
        builder.UseTimeout(TimeSpan.FromSeconds(2));
        builder.Build();
        Assert.Equal(2, builder.Timeout.Seconds);
    }
}