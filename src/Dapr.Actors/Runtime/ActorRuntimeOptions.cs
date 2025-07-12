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

#nullable enable

using System;
using System.Text.Json;

namespace Dapr.Actors.Runtime;

/// <summary>
/// Represents Dapr actor configuration for this app. Includes the registration of actor types
/// as well as runtime options.
/// 
/// See https://docs.dapr.io/reference/api/actors_api/
/// </summary>
public sealed class ActorRuntimeOptions
{
    private TimeSpan? actorIdleTimeout;
    private TimeSpan? actorScanInterval;
    private TimeSpan? drainOngoingCallTimeout;
    private bool drainRebalancedActors;
    private ActorReentrancyConfig reentrancyConfig = new ActorReentrancyConfig
    {
        Enabled = false,
    };
    private bool useJsonSerialization = false;
    private JsonSerializerOptions jsonSerializerOptions = JsonSerializerDefaults.Web;
    private string daprApiToken = string.Empty;
    private int? remindersStoragePartitions = null;

    /// <summary>
    /// Gets the collection of <see cref="ActorRegistration" /> instances.
    /// </summary>
    public ActorRegistrationCollection Actors { get; } = new ActorRegistrationCollection();

    /// <summary>
    /// Specifies how long to wait before deactivating an idle actor. An actor is idle 
    /// if no actor method calls and no reminders have fired on it.
    /// See https://docs.dapr.io/reference/api/actors_api/#dapr-calling-to-user-service-code 
    /// for more including default values.
    /// </summary>
    public TimeSpan? ActorIdleTimeout
    {
        get
        {
            return this.actorIdleTimeout;
        }

        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(actorIdleTimeout), actorIdleTimeout, "must be positive");
            }

            this.actorIdleTimeout = value;
        }
    }

    /// <summary>
    /// A duration which specifies how often to scan for actors to deactivate idle actors. 
    /// Actors that have been idle longer than the actorIdleTimeout will be deactivated.
    /// See https://docs.dapr.io/reference/api/actors_api/#dapr-calling-to-user-service-code 
    /// for more including default values.
    /// </summary>
    public TimeSpan? ActorScanInterval
    {
        get
        {
            return this.actorScanInterval;
        }

        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(actorScanInterval), actorScanInterval, "must be positive");
            }

            this.actorScanInterval = value;
        }
    }

    /// <summary>
    /// A duration used when in the process of draining rebalanced actors. This specifies 
    /// how long to wait for the current active actor method to finish. If there is no current actor method call, this is ignored.
    /// See https://docs.dapr.io/reference/api/actors_api/#dapr-calling-to-user-service-code 
    /// for more including default values.
    /// </summary>
    public TimeSpan? DrainOngoingCallTimeout
    {
        get
        {
            return this.drainOngoingCallTimeout;
        }

        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(drainOngoingCallTimeout), drainOngoingCallTimeout, "must be positive");
            }

            this.drainOngoingCallTimeout = value;
        }
    }

    /// <summary>
    /// A bool. If true, Dapr will wait for drainOngoingCallTimeout to allow a current 
    /// actor call to complete before trying to deactivate an actor. If false, do not wait.
    /// See https://docs.dapr.io/reference/api/actors_api/#dapr-calling-to-user-service-code 
    /// for more including default values.
    /// </summary>
    public bool DrainRebalancedActors
    {
        get
        {
            return this.drainRebalancedActors;
        }

        set
        {
            this.drainRebalancedActors = value;
        }
    }

    /// <summary>
    /// A configuration that defines the Actor Reentrancy parameters. If set and enabled, Actors
    /// will be able to perform reentrant calls to themselves/others. If not set or false, Actors
    /// will continue to lock for every request.
    /// See https://docs.dapr.io/developing-applications/building-blocks/actors/actor-reentrancy/
    /// </summary>
    public ActorReentrancyConfig ReentrancyConfig
    {
        get
        {
            return this.reentrancyConfig;
        }

        set
        {
            this.reentrancyConfig = value;
        }
    }

    /// <summary>
    /// Enable JSON serialization for actor proxy message serialization in both remoting and non-remoting invocations.
    /// </summary>
    public bool UseJsonSerialization
    {
        get
        {
            return this.useJsonSerialization;
        }

        set
        {
            this.useJsonSerialization = value;
        }
    }

    /// <summary>
    /// The <see cref="JsonSerializerOptions"/> to use for actor state persistence and message deserialization
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions
    {
        get
        {
            return this.jsonSerializerOptions;
        }

        set
        {
            this.jsonSerializerOptions = value ?? throw new ArgumentNullException(nameof(JsonSerializerOptions), $"{nameof(ActorRuntimeOptions)}.{nameof(JsonSerializerOptions)} cannot be null");
        }
    }

    /// <summary>
    /// The <see cref="DaprApiToken"/> to add to the headers in requests to Dapr runtime
    /// </summary>
    public string? DaprApiToken
    {
        get
        {
            return this.daprApiToken;
        }

        set
        {
            this.daprApiToken = value ?? throw new ArgumentNullException(nameof(DaprApiToken), $"{nameof(ActorRuntimeOptions)}.{nameof(DaprApiToken)} cannot be null");
        }
    }

    /// <summary>
    /// An int used to determine how many partitions to use for reminders storage.
    /// </summary>
    public int? RemindersStoragePartitions
    {
        get
        {
            return this.remindersStoragePartitions;
        }

        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remindersStoragePartitions), remindersStoragePartitions, "must be positive");
            }

            this.remindersStoragePartitions = value;
        }
    }

    /// <summary>
    /// Gets or sets the HTTP endpoint URI used to communicate with the Dapr sidecar.
    /// </summary>
    /// <remarks>
    /// The URI endpoint to use for HTTP calls to the Dapr runtime. The default value will be 
    /// <c>DAPR_HTTP_ENDPOINT</c> first, or <c>http://127.0.0.1:DAPR_HTTP_PORT</c> as fallback
    /// where <c>DAPR_HTTP_ENDPOINT</c> and <c>DAPR_HTTP_PORT</c> represents the value of the
    /// corresponding environment variables. 
    /// </remarks>
    /// <value></value>
    public string? HttpEndpoint { get; set; }
}