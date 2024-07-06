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

using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Jobs;

/// <summary>
/// Contains extension methods for using Dapr Jobs with dependency injection.
/// </summary>
public static class JobsServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Jobs support to the service collection.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/>.</param>
    public static IServiceCollection AddDaprJobs(this IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection, nameof(serviceCollection));

        serviceCollection.AddHttpClient<DaprJobsClient>(httpClient =>
        {
            var jobsHttpEndpoint = new Uri($"{DaprDefaults.GetDefaultHttpEndpoint()}/v1.0-alpha1/jobs/");
            if (jobsHttpEndpoint.Scheme != "http" && jobsHttpEndpoint.Scheme != "https")
                throw new InvalidOperationException("The HTTP endpoint must use http or https");

            httpClient.BaseAddress = jobsHttpEndpoint;
            httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var daprApiToken = DaprDefaults.GetDefaultDaprApiToken();
            if (!string.IsNullOrEmpty(daprApiToken))
            {
                httpClient.DefaultRequestHeaders.Add("dapr-api-token", daprApiToken);
            }
        });

        return serviceCollection;
    }
}
