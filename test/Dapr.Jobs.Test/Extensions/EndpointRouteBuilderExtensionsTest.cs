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

namespace Dapr.Jobs.Test.Extensions;

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
