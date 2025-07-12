using System;
using System.Text.Json;
using Xunit;

namespace Dapr.Common.Test;

public class DaprGenericClientBuilderTest
{
    [Fact]
    public void DaprGenericClientBuilder_ShouldUpdateHttpEndpoint_WhenHttpEndpointIsProvided()
    {
        // Arrange
        var builder = new SampleDaprGenericClientBuilder();
        const string endpointValue = "http://sample-endpoint";

        // Act
        builder.UseHttpEndpoint(endpointValue);

        // Assert
        Assert.Equal(endpointValue, builder.HttpEndpoint);
    }

    [Fact]
    public void DaprGenericClientBuilder_ShouldUpdateHttpEndpoint_WhenGrpcEndpointIsProvided()
    {
        // Arrange
        var builder = new SampleDaprGenericClientBuilder();
        const string endpointValue = "http://sample-endpoint";
            
        // Act
        builder.UseGrpcEndpoint(endpointValue);

        // Assert
        Assert.Equal(endpointValue, builder.GrpcEndpoint);
    }

    [Fact]
    public void DaprGenericClientBuilder_ShouldUpdateJsonSerializerOptions()
    {
        // Arrange
        const int maxDepth = 8;
        const bool writeIndented = true;
        var builder = new SampleDaprGenericClientBuilder();
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            MaxDepth = maxDepth
        };

        // Act
        builder.UseJsonSerializationOptions(options);

        // Assert
        Assert.Equal(writeIndented, builder.JsonSerializerOptions.WriteIndented);
        Assert.Equal(maxDepth, builder.JsonSerializerOptions.MaxDepth);
    }

    [Fact]
    public void DaprGenericClientBuilder_ShouldUpdateDaprApiToken()
    {
        // Arrange
        const string apiToken = "abc123";
        var builder = new SampleDaprGenericClientBuilder();
        
        // Act
        builder.UseDaprApiToken(apiToken);

        // Assert
        Assert.Equal(apiToken, builder.DaprApiToken);
    }

    [Fact]
    public void DaprGenericClientBuilder_ShouldUpdateTimeout()
    {
        // Arrange
        var timeout = new TimeSpan(4, 2, 1, 2);
        var builder = new SampleDaprGenericClientBuilder();
        
        // Act
        builder.UseTimeout(timeout);
        
        // Assert
        Assert.Equal(timeout, builder.Timeout);
    }

    private sealed class SampleDaprGenericClientBuilder : DaprGenericClientBuilder<SampleDaprGenericClientBuilder>, IDaprClient
    {
        public override SampleDaprGenericClientBuilder Build()
        {
            // Implementation
            throw new NotImplementedException();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
