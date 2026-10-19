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

using Dapr.Actors;
using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.Activation;

public interface IDependencyInjectionActor : IActor
{
    Task<int> IncrementAsync();
}

public sealed class DependencyInjectionActor(
    ActorHost host,
    CounterService counter) : Actor(host), IDependencyInjectionActor, IPingActor
{
    public Task<int> IncrementAsync() => Task.FromResult(counter.Value++);

    public Task Ping(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class CounterService
{
    public int Value { get; set; }
}
