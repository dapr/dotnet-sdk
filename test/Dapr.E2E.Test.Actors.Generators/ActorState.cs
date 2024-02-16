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

using Dapr.Actors;
using Dapr.Actors.Client;

namespace Dapr.E2E.Test.Actors.Generators;

internal static class ActorState
{
    public static async Task EnsureReadyAsync<TActor>(ActorId actorId, string actorType, ActorProxyOptions? options = null, CancellationToken cancellationToken = default)
        where TActor : IPingActor
    {
        var pingProxy = ActorProxy.Create<TActor>(actorId, actorType, options);

        while (true)
        {
            try
            {
                await pingProxy.Ping();

                break;
            }
            catch (DaprApiException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }
    }
}
