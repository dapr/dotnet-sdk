// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Text.Json;
using Grpc.Net.Client;

namespace Dapr.SecretsManagement.Test;

public sealed class DaprSecretsManagementClientBuilderTests
{
    [Fact]
    public void Builder_UsesPropertyNameCaseInsensitiveByDefault()
    {
        var builder = new DaprSecretsManagementClientBuilder();
        Assert.True(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void Builder_UsesPropertyNameCaseHandlingAsSpecified()
    {
        var builder = new DaprSecretsManagementClientBuilder();
        builder.UseJsonSerializationOptions(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false
        });
        Assert.False(builder.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void Builder_UsesThrowOperationCanceledOnCancellation_ByDefault()
    {
        var builder = new DaprSecretsManagementClientBuilder();
        _ = builder.Build();
        Assert.True(builder.GrpcChannelOptions.ThrowOperationCanceledOnCancellation);
    }

    [Fact]
    public void Builder_DoesNotOverrideUserGrpcChannelOptions()
    {
        var builder = new DaprSecretsManagementClientBuilder();
        _ = builder.UseGrpcChannelOptions(new GrpcChannelOptions()).Build();
        Assert.False(builder.GrpcChannelOptions.ThrowOperationCanceledOnCancellation);
    }

    [Fact]
    public void Builder_ValidatesGrpcEndpointScheme()
    {
        var builder = new DaprSecretsManagementClientBuilder();
        builder.UseGrpcEndpoint("ftp://example.com");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("The gRPC endpoint must use http or https.", ex.Message);
    }

    [Fact]
    public void Builder_ValidatesHttpEndpointScheme()
    {
        var builder = new DaprSecretsManagementClientBuilder();
        builder.UseHttpEndpoint("ftp://example.com");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("The HTTP endpoint must use http or https.", ex.Message);
    }

    [Fact]
    public void Builder_SetsApiToken()
    {
        var builder = new DaprSecretsManagementClientBuilder();
        builder.UseDaprApiToken("test_token");
        _ = builder.Build();
        Assert.Equal("test_token", builder.DaprApiToken);
    }

    [Fact]
    public void Builder_SetsNullApiToken()
    {
        var builder = new DaprSecretsManagementClientBuilder();
        builder.UseDaprApiToken(null!);
        _ = builder.Build();
        Assert.Null(builder.DaprApiToken);
    }

    [Fact]
    public void Builder_SetsTimeout()
    {
        var builder = new DaprSecretsManagementClientBuilder();
        builder.UseTimeout(TimeSpan.FromSeconds(2));
        _ = builder.Build();
        Assert.Equal(2, builder.Timeout.Seconds);
    }
}
