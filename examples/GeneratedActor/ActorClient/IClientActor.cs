﻿// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

using Dapr.Actors.Generators;

namespace GeneratedActor;

internal sealed record ClientState(string Value);

[GenerateActorClient]
internal interface IClientActor
{
    [ActorMethod(Name = "GetState")]
    Task<ClientState> GetStateAsync(CancellationToken cancellationToken = default);

    [ActorMethod(Name = "SetState")]
    Task SetStateAsync(ClientState state, CancellationToken cancellationToken = default);
}

[GenerateActorClient]
internal interface IClientActor2
{
    [ActorMethod(Name = "GetState")]
    Task<ClientState> GetStateAsync(CancellationToken cancellationToken = default);

    [ActorMethod(Name = "SetState")]
    Task SetStateAsync(CancellationToken cancellationToken, ClientState state);

    Task SetStateAsync(ClientState state, string test, CancellationToken cancellationToken = default);
}
