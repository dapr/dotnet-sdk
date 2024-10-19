using Dapr.Common.Data.Extensions;
using Dapr.Common.Data.Operations;
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
    public void WithDaprOperation_ShouldRegisterSingletonService()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithDaprOperation<MockOperation>(ServiceLifetime.Singleton);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetService<MockOperation>();
        Assert.NotNull(service);
    }

    [Fact]
    public void WithDaprOperation_ShouldRegisterScopedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithDaprOperation<MockOperation>(ServiceLifetime.Scoped);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        using (var scope = serviceProvider.CreateScope())
        {
            var service = scope.ServiceProvider.GetService<MockOperation>();
            Assert.NotNull(service);
        }
    }

    [Fact]
    public void WithDaprOperation_ShouldRegisterTransientService()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new DaprDataProcessingPipelineBuilder(services);

        // Act
        builder.WithDaprOperation<MockOperation>(ServiceLifetime.Transient);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var service1 = serviceProvider.GetService<MockOperation>();
        var service2 = serviceProvider.GetService<MockOperation>();
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2);
    }

    private class MockOperation : IDaprDataOperation
    {
        /// <summary>
        /// The name of the operation.
        /// </summary>
        public string Name => "Test";
    }
}

