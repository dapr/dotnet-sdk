using Microsoft.DurableTask;

namespace Dapr.Workflows;

public sealed class WorkflowsRuntimeOptions
{
    readonly Dictionary<string, Action<IDurableTaskRegistry>> factories = new();

    public void RegisterWorkflow<TInput, TOutput>(string name, Func<WorkflowContext, TInput?, Task<TOutput?>> implementation)
    {
        // Dapr workflows are implemented as specialized Durable Task orchestrations
        this.factories.Add(name, (IDurableTaskRegistry registry) =>
        {
            registry.AddOrchestrator<TInput, TOutput>(name, (innerContext, input) =>
            {
                WorkflowContext workflowContext = new(innerContext);
                return implementation(workflowContext, input);
            });
        });
    }

    public void RegisterActivity<TInput, TOutput>(string name, Func<ActivityContext, TInput?, Task<TOutput?>> implementation)
    {
        // Dapr activities are implemented as specialized Durable Task activities
        this.factories.Add(name, (IDurableTaskRegistry registry) =>
        {
            registry.AddActivity<TInput, TOutput>(name, (innerContext, input) =>
            {
                ActivityContext activityContext = new(innerContext);
                return implementation(activityContext, input);
            });
        });
    }

    public void RegisterActivity<TActivity>(string name, Func<ActivityContext, TInput?, Task<TOutput?>> implementation)
    {
        // Dapr activities are implemented as specialized Durable Task activities
        this.factories.Add(name, (IDurableTaskRegistry registry) =>
        {
            registry.AddActivity<TActivity>(name, (innerContext, input) =>
            {
                ActivityContext activityContext = new(innerContext);
                return implementation(activityContext, input);
            });
        });
    }

    internal void AddWorkflowsToRegistry(IDurableTaskRegistry registry)
    {
        foreach (Action<IDurableTaskRegistry> factory in this.factories.Values)
        {
            factory.Invoke(registry);
        }
    }
}