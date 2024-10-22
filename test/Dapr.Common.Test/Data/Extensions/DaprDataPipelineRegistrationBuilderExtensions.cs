using Dapr.Common.Data.Extensions;
using Dapr.Common.Data.Operations;
using Dapr.Common.Data.Operations.Providers.Compression;
using Dapr.Common.Data.Operations.Providers.Integrity;
using Dapr.Common.Data.Operations.Providers.Masking;
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
        var builder = new DaprDataPipelineBuilder(services);

        // Act
        var result = builder.AddDaprDataProcessingPipeline();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DaprDataProcessingPipelineBuilder>(result);
    }

    [Fact]
    public void WithSerializer_ShouldRegisterSerializationService()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithSerializer<SystemTextJsonSerializer<SampleRecord>>();

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
        var builder = new DaprDataProcessingPipelineBuilder(services);
        
        // Act
        builder.WithSerializer(_ => new SystemTextJsonSerializer<SampleRecord>());

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
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithCompressor<GzipCompressor>();
        
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
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithCompressor(_ => new GzipCompressor());
        
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
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithIntegrity<Sha256Validator>();
        
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
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithIntegrity(_ => new Sha256Validator());
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
        Assert.True(service is Sha256Validator);
    }

    [Fact]
    public void WithMasking_ShouldRegisterType()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithMasking<RegularExpressionMasker>();
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
        Assert.True(service is RegularExpressionMasker);
    }

    [Fact]
    public void WithMasking_ShouldRegisterFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithMasking(_ => new RegularExpressionMasker());
        
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service);
        Assert.True(service is RegularExpressionMasker);
    }
    
    [Fact]
    public void WithDaprOperation_ShouldRegisterScopedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new DaprDataProcessingPipelineBuilder(services);
    
        // Act
        builder.WithSerializer<SystemTextJsonSerializer<SampleRecord>>(ServiceLifetime.Scoped);
    
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
        var builder = new DaprDataProcessingPipelineBuilder(services);
    
        // Act
        builder.WithSerializer(_ => new SystemTextJsonSerializer<SampleRecord>(), ServiceLifetime.Transient);
    
        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service1 = serviceProvider.GetService<IDaprDataOperation>();
        var service2 = serviceProvider.GetService<IDaprDataOperation>();
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2);
    }
    
    private record SampleRecord(string Name, int Count);

    private class MockOperation : IDaprDataOperation
    {
        /// <summary>
        /// The name of the operation.
        /// </summary>
        public string Name => "Test";
    }
}

