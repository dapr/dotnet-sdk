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

namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Provides an extension point for tests to inject state-store failures at typed state boundaries.
/// </summary>
public interface IActorStateFaultInjector
{
    /// <summary>
    /// Runs before a typed state value is written to the backing store.
    /// </summary>
    ValueTask BeforeWriteAsync(
        Type stateType,
        string actorType,
        string actorId,
        string stateName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs after an enrolled state value has been identified and before its migrating read folds.
    /// </summary>
    ValueTask BeforeMigrationAsync(
        Type targetStateType,
        string actorType,
        string actorId,
        string stateName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs before a migration hop is applied.
    /// </summary>
    ValueTask BeforeUpcastHopAsync(
        Type fromStateType,
        Type toStateType,
        string actorType,
        string actorId,
        string stateName,
        CancellationToken cancellationToken = default);
}
