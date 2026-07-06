// ------------------------------------------------------------------------
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

[GenerateActorClient]
internal interface IGenericClientActor<TGenericType1, TGenericType2>
{
    [ActorMethod(Name = "GetState")]
    Task<TGenericType1> GetStateAsync(CancellationToken cancellationToken = default);

    [ActorMethod(Name = "SetState")]
    Task SetStateAsync(TGenericType2 state, CancellationToken cancellationToken = default);
}