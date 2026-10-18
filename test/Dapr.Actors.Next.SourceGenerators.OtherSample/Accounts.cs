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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Attributes;
using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Core.Activation;

namespace Dapr.Actors.Next.SourceGenerators.OtherSample
{
    public class AccountBase
    {
        public decimal Balance { get; set; }
        public virtual string Owner { get; set; } = "";
        public int Revision;
    }

    public sealed class AccountV1 : AccountBase
    {
        public override string Owner { get; set; } = "";
        public int Count { get; set; }
    }

    public sealed class AccountV2 : AccountBase
    {
        public override string Owner { get; set; } = "";
        public int Count { get; set; }
        public bool Frozen { get; set; }
    }
}

namespace Dapr.Actors.Next.SourceGenerators.OtherSample.First
{
    [GenerateActorClient]
    public interface IAccount : IActor
    {
        Task<OtherSample.AccountV2> ReadAsync(CancellationToken cancellationToken = default);

        Task<OtherSample.AccountV2> UpdateAndMigrateAsync(decimal balance,
            CancellationToken cancellationToken = default);
    }

    [DaprActor("FirstAccount")]
    public sealed class AccountActor(ActorActivationContext context) : Actor, IAccount
    {
        protected override ActorId Id => context.ActorId;
        protected override IActorStateAccessor State => context.State;

        public async Task<OtherSample.AccountV2> ReadAsync(CancellationToken cancellationToken = default) =>
            (await State.GetOrCreateAsync("account", () => new OtherSample.AccountV2(), cancellationToken)).Value;

        public async Task<OtherSample.AccountV2> UpdateAndMigrateAsync(decimal balance,
            CancellationToken cancellationToken = default)
        {
            var previous = await State.TryGetAsync<OtherSample.AccountV1>("account", cancellationToken);
            await State.SaveStateAsync(cancellationToken);
            previous!.Value.Balance = balance;
            return (await State.TryGetAsync<OtherSample.AccountV2>("account", cancellationToken))!.Value;
        }
    }
}

namespace Dapr.Actors.Next.SourceGenerators.OtherSample.Second
{
    [GenerateActorClient]
    public interface IAccount : IActor
    {
        Task<string> EchoAsync(string payload, string request, string typed, string args, string wireSerializer,
            string @event, CancellationToken cancellationToken = default);
    }

    [DaprActor("SecondAccount")]
    public sealed class AccountActor(ActorActivationContext context) : Actor, IAccount
    {
        protected override ActorId Id => context.ActorId;
        protected override IActorStateAccessor State => context.State;

        public Task<string> EchoAsync(string payload, string request, string typed, string args, string wireSerializer,
            string @event, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Join("|", payload, request, typed, args, wireSerializer, @event));
    }
}
