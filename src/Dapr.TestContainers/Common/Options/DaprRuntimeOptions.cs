// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System;

namespace Dapr.TestContainers.Common.Options;

/// <summary>
/// The various options used to spin up the Dapr containers.
/// </summary>
/// <param name="Version">The version of the Dapr images to use.</param>
public sealed record DaprRuntimeOptions(string Version = "latest")
{
    /// <summary>
    /// The application's port.
    /// </summary>
    public int AppPort { get; set; } = 8080;
    
    /// <summary>
    /// The ID of the test application.
    /// </summary>
    public string AppId { get; private set; } = $"test-app-{Guid.NewGuid():N}";
    
    /// <summary>
    /// The level of Dapr logs to show.
    /// </summary>
    public DaprLogLevel LogLevel { get; private set; } = DaprLogLevel.Info;

    /// <summary>
    /// The image tag for the Dapr runtime.
    /// </summary>
	public string RuntimeImageTag => $"daprio/daprd:{Version}";
    /// <summary>
    /// The image tag for the Dapr placement service.
    /// </summary>
	public string PlacementImageTag => $"daprio/placement:{Version}";
    /// <summary>
    /// The image tag for the Dapr scheduler service.
    /// </summary>
	public string SchedulerImageTag => $"daprio/scheduler:{Version}";

    /// <summary>
    /// Sets the Dapr log level.
    /// </summary>
    /// <param name="logLevel">The log level to specify.</param>
    public DaprRuntimeOptions WithLogLevel(DaprLogLevel logLevel)
    {
        LogLevel = logLevel;
        return this;
    }

    /// <summary>
    /// Sets the Dapr App ID.
    /// </summary>
    /// <param name="appId">The App ID to use for the test application.</param>
    public DaprRuntimeOptions WithAppId(string appId)
    {
        AppId = appId;
        return this;
    }
}
