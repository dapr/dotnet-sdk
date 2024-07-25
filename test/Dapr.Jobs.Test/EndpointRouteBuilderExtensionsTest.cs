namespace Dapr.Jobs.Test;

public class EndpointRouteBuilderExtensionsTest
{
    //The following won't work because Moq can't test static methods - leaving here should the project ever adopt some sort of commercial tool that enables such testing
    //[Fact]
    //public void MapDaprScheduledJobs_ValidateRegisteredRoute()
    //{
    //    var endpoints = new Mock<IEndpointRouteBuilder>();
    //    endpoints.Setup(a => a.DataSources).Returns(new List<EndpointDataSource>()); //While .NET 9 changed this behavior, .NET 8 and lower will throw if this isn't setup on the mock

    //    const string jobName = "test-job";
    //    var handler = (JobDetails details) =>
    //    {
    //        //Doesn't matter what it does here
    //    };

    //    var result = endpoints.Object.MapDaprScheduledJob(jobName, handler);
        
    //    var registeredRoutes = endpoints.Invocations
    //        .Where(invocation => invocation.Method.Name == "MapPost")
    //        .ToList();

    //    Assert.Single(registeredRoutes); //One MapPost call
    //    Assert.Equal($"job/{jobName}", registeredRoutes[0].Arguments[0]); //Validate the route
    //    Assert.Equal(handler, registeredRoutes[0].Arguments[1]); //Validate the handler delegate
    //}
}
