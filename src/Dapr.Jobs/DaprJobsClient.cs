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

using System.Net;
using System.Net.Http.Json;

namespace Dapr.Jobs;

/// <summary>
/// Defines client operations for managing Dapr jobs.
/// </summary>
public class DaprJobsClient
{
    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprJobsClient"/> class.
    /// </summary>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to communicate with the Dapr sidecar.</param>
    public DaprJobsClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Schedules a job with a name.
    /// </summary>
    /// <param name="name">The name of the job being scheduled.</param>
    /// <param name="jsonSerializableData">A string value providing any related content. Content is returned when the reminder expires.</param>
    /// <param name="dueTime">Specifies the time after which this job is invoked.</param>
    public async Task ScheduleJobAsync(string name, object jsonSerializableData, TimeSpan dueTime)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        ArgumentNullException.ThrowIfNull(jsonSerializableData, nameof(jsonSerializableData));
        ArgumentNullException.ThrowIfNull(dueTime, nameof(dueTime));

        var options =
            new ScheduleJobOptions(new ScheduleJobInnerOptions(jsonSerializableData, dueTime.ToDurationString()));

        var response = await httpClient.PostAsJsonAsync($"/{name}", options);

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new MalformedJobException(response);
            case HttpStatusCode.InternalServerError:
                throw new DaprJobsServiceException(response);
            default:
                return;
        }
    }

    /// <summary>
    /// Gets a job from its name.
    /// </summary>
    /// <param name="name">The name of the scheduled job being retrieved.</param>
    /// <returns>The job data.</returns>
    public async Task<string> GetJobDataAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var response = await httpClient.GetAsync($"/{name}");

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new MalformedJobException(response);
            case HttpStatusCode.InternalServerError:
                throw new DaprJobsServiceException(response);
            default:
                return await response.Content.ReadAsStringAsync();
        }
    }

    /// <summary>
    /// Deletes a named job.
    /// </summary>
    /// <param name="name">The name of the job being deleted.</param>
    /// <returns></returns>
    public async Task DeleteJobAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var response = await httpClient.DeleteAsync($"/{name}");

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new MalformedJobException(response);
            case HttpStatusCode.InternalServerError:
                throw new DaprJobsServiceException(response);
            default:
                return;
        }
    }
}
