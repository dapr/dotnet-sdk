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

namespace Dapr.Jobs;

/// <summary>
/// The exception type thrown when an malformed request is made to the Dapr Jobs service.
/// </summary>
[Serializable]
public class MalformedJobException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="DaprJobsServiceException"/> for a non-successful HTTP request.
    /// </summary>
    /// <param name="response"></param>
    public MalformedJobException(HttpResponseMessage? response) : base(FormatExceptionForFailedRequest())
    {
        Response = response;
    }

    /// <summary>
    /// Gets the <see cref="HttpResponseMessage"/> of the request that failed. Will be <c>null</c> if the
    /// failure was not related to an HTTP request or preventing the response from being received.
    /// </summary>
    public HttpResponseMessage? Response { get; }
    
    private static string FormatExceptionForFailedRequest() => "The request made to the Jobs API was malformed";
}
