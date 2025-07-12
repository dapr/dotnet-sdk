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

/// <summary>
/// Represents the configuration required for Actor Reentrancy.
///
/// See: https://docs.dapr.io/developing-applications/building-blocks/actors/actor-reentrancy/
/// </summary>
public sealed class ActorReentrancyConfig 
{
    private bool enabled;
    private int? maxStackDepth;

    /// <summary>
    /// Determines if Actor Reentrancy is enabled or disabled.
    /// </summary>
    public bool Enabled
    {
        get 
        {
            return this.enabled;
        }

        set 
        {
            this.enabled = value;
        }
    }

    /// <summary>
    /// Optional parameter that will stop a reentrant call from progressing past the defined
    /// limit. This is a safety measure against infinite reentrant calls.
    /// </summary>
    public int? MaxStackDepth
    {
        get
        {
            return this.maxStackDepth;
        }

        set
        {
            this.maxStackDepth = value;
        }
    }
}