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

using Dapr.Common.Data.Extensions;
using Dapr.Common.Data.Operations;
using Dapr.Common.Data.Operations.Providers.Compression;
using Dapr.Common.Data.Operations.Providers.Integrity;
using Dapr.Common.Data.Operations.Providers.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.Common.Test.Data.Extensions;

public class DaprDataPipelineRegistrationBuilderExtensionsTests
{
    [Fact]
    public void AddDaprDataProcessingPipeline_ShouldReturnDaprDataProcessingPipelineBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddDaprDataProcessingPipeline();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DaprDataPipelineBuilder>(result);
    }

    [Fact]
    public void WithSerializer_ShouldRegisterSerializationService()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddDaprDataProcessingPipeline()
            .WithSerializer<SystemTextJsonSerializer<SampleRecord>>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
        Assert.True(service is SystemTextJsonSerializer<SampleRecord>);
    }

    [Fact]
    public void WithSerializer_ShouldRegisterSerializationFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddDaprDataProcessingPipeline()
            .WithSerializer(_ => new SystemTextJsonSerializer<SampleRecord>());

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
        Assert.True(service is SystemTextJsonSerializer<SampleRecord>);
    }

    [Fact]
    public void WithCompressor_ShouldRegisterType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDaprDataProcessingPipeline()
            .WithCompressor<GzipCompressor>();
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
        Assert.True(service is GzipCompressor);
    }

    [Fact]
    public void WithCompressor_ShouldRegisterFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDaprDataProcessingPipeline()
            .WithCompressor(_ => new GzipCompressor());
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
        Assert.True(service is GzipCompressor);
    }

    [Fact]
    public void WithIntegrity_ShouldRegisterType()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddDaprDataProcessingPipeline()    
            .WithIntegrity<Sha256Validator>();
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
        Assert.True(service is Sha256Validator);
    }

    [Fact]
    public void WithIntegrity_ShouldRegisterFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDaprDataProcessingPipeline()    
            .WithIntegrity(_ => new Sha256Validator());
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
        Assert.True(service is Sha256Validator);
    }
    
    [Fact]
    public void WithDaprOperation_ShouldRegisterScopedService()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        services.AddDaprDataProcessingPipeline()
            .WithSerializer<SystemTextJsonSerializer<SampleRecord>>(ServiceLifetime.Scoped);
    
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
    }
    
    [Fact]
    public void WithDaprOperation_ShouldRegisterTransientService()
    {
        // Arrange
        var services = new ServiceCollection();
    
        // Act
        services.AddDaprDataProcessingPipeline()
            .WithSerializer(_ => new SystemTextJsonSerializer<SampleRecord>(), ServiceLifetime.Transient);
    
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service1 = serviceProvider.GetService<IDaprDataOperation>();
        var service2 = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2);
    }
    
    private sealed record SampleRecord(string Name, int Count);

    private class MockOperation : IDaprDataOperation
    {
        /// <summary>
        /// The name of the operation.
        /// </summary>
        public string Name => "Test";
    }
}

