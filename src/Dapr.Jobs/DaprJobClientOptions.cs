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
/// Options used to configure the Dapr job client.
/// </summary>
public class DaprJobClientOptions
{
    /// <summary>
    /// Initializes a new instance of <see cref="DaprJobClientOptions"/>.
    /// </summary>
    /// <param name="appId">The ID of the app .</param>
    /// <param name="appNamespace">The namespace of the app.</param>
    public DaprJobClientOptions(string appId, string appNamespace)
    {
        AppId = appId;
        Namespace = appNamespace;
    }

    /// <summary>
    /// The App ID of the requester.
    /// </summary>
    public string AppId { get; init; }

    /// <summary>
    /// The namespace of the requester.
    /// </summary>
    public string Namespace { get; init; }
}
