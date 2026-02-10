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
using Dapr.Testcontainers.Common;

namespace Dapr.Testcontainers.Common.Options;

/// <summary>
/// The various options used to spin up the Dapr containers.
/// </summary>
public sealed record DaprRuntimeOptions
{
    private const string DEFAULT_VERSION_ENVVAR_NAME = "DAPR_RUNTIME_VERSION";
    private static readonly string[] CiEnvironmentSignals =
    [
        "CI",
        "TF_BUILD",
        "GITHUB_ACTIONS",
        "GITLAB_CI",
        "JENKINS_URL",
        "TEAMCITY_VERSION",
        "APPVEYOR",
        "BUILDKITE",
        "CIRCLECI",
        "TRAVIS",
        "BITBUCKET_BUILD_NUMBER",
        "BUILD_BUILDID",
        "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI"
    ];

    /// <summary>
    /// Initializes a new instance of the Dapr runtime options.
    /// </summary>
    /// <param name="version">The version of the Dapr images to use.</param>
    public DaprRuntimeOptions(string version = "latest")
    {
        // Get the version from an environment variable, if set
        var envVarVersion = Environment.GetEnvironmentVariable(DEFAULT_VERSION_ENVVAR_NAME);
        Version = !string.IsNullOrWhiteSpace(envVarVersion) ? envVarVersion : version;

        // Automatically enable container logs in CI environments
        TryEnableContainerLogsForCi();
    }
    
    /// <summary>
    /// The version of the Dapr images to use.
    /// </summary>
    public string Version { get; }
    
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
    /// Enables capturing container logs to files.
    /// </summary>
    public bool EnableContainerLogs { get; private set; }

    /// <summary>
    /// The directory used to write container logs.
    /// </summary>
    public string? ContainerLogsDirectory { get; private set; }

    /// <summary>
    /// Indicates whether container logs are preserved after disposal.
    /// </summary>
    public bool PreserveContainerLogs { get; private set; }

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
        TryEnableContainerLogsForCi();
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

    /// <summary>
    /// Enables container log capture to files.
    /// </summary>
    /// <param name="directory">The directory to write logs to. If null, a temp directory is created.</param>
    /// <param name="preserveOnDispose">Whether logs are preserved after disposing the environment.</param>
    public DaprRuntimeOptions WithContainerLogs(string? directory = null, bool preserveOnDispose = true)
    {
        EnableContainerLogs = true;
        PreserveContainerLogs = preserveOnDispose;
        ContainerLogsDirectory = string.IsNullOrWhiteSpace(directory)
            ? TestDirectoryManager.CreateTestDirectory("dapr-logs")
            : directory;
        return this;
    }

    internal string? EnsureContainerLogsDirectory()
    {
        if (!EnableContainerLogs)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(ContainerLogsDirectory))
        {
            ContainerLogsDirectory = TestDirectoryManager.CreateTestDirectory("dapr-logs");
        }

        return ContainerLogsDirectory;
    }

    private void TryEnableContainerLogsForCi()
    {
        if (EnableContainerLogs)
        {
            return;
        }

        if (!IsCiEnvironment())
        {
            return;
        }

        WithContainerLogs();
    }

    private static bool IsCiEnvironment()
    {
        foreach (var key in CiEnvironmentSignals)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (string.Equals(key, "CI", StringComparison.OrdinalIgnoreCase)
                && string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
