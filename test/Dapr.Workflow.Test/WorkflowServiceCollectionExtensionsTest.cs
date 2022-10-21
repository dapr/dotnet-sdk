namespace Dapr.Client
{
    public partial class DaprWorkflowTest
    {
        public void CanRegisterWorkflow()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions();
            services.AddWorkflow(options =>
            {
                options.RegisterWorkflow<string, string>("testName", implementation: async (context, input) => 
                {
                    string result  = "Testing";
                    return result;
                });
            });
            
            var runtime = services.BuildServiceProvider().GetRequiredService<WorkflowRuntimeOptions>();
            Assert.Equal("testName", runtime.factories.Keys[0]);
        }
    }
}