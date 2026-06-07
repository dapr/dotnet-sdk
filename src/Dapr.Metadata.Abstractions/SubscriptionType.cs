// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using System.Runtime.Serialization;

namespace Dapr.Metadata.Abstractions;

/// <summary>
/// The type fo the subscription.
/// </summary>
public enum SubscriptionType
{
    /// <summary>
    /// Identifies a declarative subscription.
    /// </summary>
    [EnumMember(Value="DECLARATIVE")]
    Declarative,
    /// <summary>
    /// Identifies a streaming subscription.
    /// </summary>
    [EnumMember(Value="STREAMING")]
    Streaming,
    /// <summary>
    /// Identifies a programmatic subscription.
    /// </summary>
    [EnumMember(Value="PROGRAMMATIC")]
    Programmatic
}
