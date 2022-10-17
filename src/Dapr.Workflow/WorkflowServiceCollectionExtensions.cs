using Microsoft.DurableTask.Grpc;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Workflows;

/// <summary>
/// Contains extension methods for using Dapr Workflow with dependency injection.
/// </summary>
public static class WorkflowsServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Workflow support to the service collection.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">A delegate used to configure actor options and register workflow functions.</param>
    public static IServiceCollection AddWorkflow(
        this IServiceCollection serviceCollection,
        Action<WorkflowRuntimeOptions> configure)
    {
        if (serviceCollection == null)
        {
            throw new ArgumentNullException(nameof(serviceCollection));
        }

        serviceCollection.AddSingleton<WorkflowRuntimeOptions>();
        serviceCollection.AddDaprClient();
        serviceCollection.AddSingleton(services => new WorkflowClient(services));

        serviceCollection.AddHostedService(services =>
        {
            DurableTaskGrpcWorker.Builder workerBuilder = DurableTaskGrpcWorker.CreateBuilder().UseServices(services);

            WorkflowRuntimeOptions options = services.GetRequiredService<WorkflowRuntimeOptions>();
            configure?.Invoke(options);

            workerBuilder.UseServices(services);

            workerBuilder.AddTasks(registry =>
            {
                options.AddWorkflowsToRegistry(registry);
            });

            DurableTaskGrpcWorker worker = workerBuilder.Build();
            return worker;
        });

        return serviceCollection;
    }
}