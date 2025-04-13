// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Actors;

using System;

/// <summary>
/// Represents connection settings for Http/gRPC Client to interact with Dapr runtime.
/// </summary>
internal class ClientSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientSettings"/> class.
    /// </summary>
    /// <param name="clientTimeout">Timespan to wait before the request times out for the client.</param>
    public ClientSettings(TimeSpan? clientTimeout = null)
    {
        this.ClientTimeout = clientTimeout;
    }

    /// <summary>
    /// Gets or sets the Timespan to wait before the request times out for the client.
    /// </summary>
    public TimeSpan? ClientTimeout { get; set; }
}