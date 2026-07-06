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

using Dapr.AspNetCore;

namespace ControllerSample;

using System;
using Dapr;

/// <summary>
/// Sample custom <see cref="ITopicMetadata" /> implementation that returns topic metadata from environment variables.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class CustomTopicAttribute : Attribute, ITopicMetadata
{
    public CustomTopicAttribute(string pubsubName, string name)
    {
        this.PubsubName = Environment.ExpandEnvironmentVariables(pubsubName);
        this.Name = Environment.ExpandEnvironmentVariables(name);
    }

    /// <inheritdoc/>
    public string PubsubName { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public new string Match { get; }

    /// <inheritdoc/>
    public int Priority { get; }
}