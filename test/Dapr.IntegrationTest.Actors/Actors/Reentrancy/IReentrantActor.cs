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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors.Reentrancy;

/// <summary>
/// Options controlling how a reentrant call chain is executed.
/// </summary>
public sealed class ReentrantCallOptions
{
    /// <summary>
    /// Gets or sets the number of additional reentrant calls remaining to make.
    /// </summary>
    public int CallsRemaining { get; set; }

    /// <summary>
    /// Gets or sets the zero-based sequence number of the current call.
    /// </summary>
    public int CallNumber { get; set; }
}

/// <summary>
/// Records a single enter or exit event within a reentrant call chain.
/// </summary>
public sealed class CallRecord
{
    /// <summary>
    /// Gets or sets a value indicating whether this record represents an entry (<see langword="true"/>) or exit (<see langword="false"/>).
    /// </summary>
    public bool IsEnter { get; set; }

    /// <summary>
    /// Gets or sets the wall-clock time of this event.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the sequence number of the call that produced this record.
    /// </summary>
    public int CallNumber { get; set; }
}

/// <summary>
/// Per-call state kept by <see cref="IReentrantActor"/>.
/// </summary>
public sealed class ReentrantCallState
{
    /// <summary>
    /// Gets the ordered list of enter/exit records for a single call number.
    /// </summary>
    public List<CallRecord> Records { get; init; } = [];
}

/// <summary>
/// Actor interface that exercises Dapr actor reentrancy.
/// </summary>
public interface IReentrantActor : IPingActor, IActor
{
    /// <summary>
    /// Initiates a reentrant call chain as described by <paramref name="callOptions"/>.
    /// </summary>
    /// <param name="callOptions">Controls the depth and sequence number of the reentrant chain.</param>
    Task ReentrantCall(ReentrantCallOptions callOptions);

    /// <summary>
    /// Returns the enter/exit records accumulated for the given <paramref name="callNumber"/>.
    /// </summary>
    /// <param name="callNumber">The zero-based call number to retrieve state for.</param>
    Task<ReentrantCallState> GetState(int callNumber);
}
