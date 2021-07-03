// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Reentrancy
{
    public class ReentrantActor : Actor, IReentrantActor
    {
        public ReentrantActor(ActorHost host)
            : base(host)
        {
        }

        public Task Ping()
        {
            return Task.CompletedTask;
        }

        public async Task ReentrantCall(ReentrantCallOptions callOptions)
        {
            try
            {
                await UpdateState(true);
                var actor = this.ProxyFactory.CreateActorProxy<IReentrantActor>(this.Id, "ReentrantActor");
                if (callOptions == null || callOptions.CallsRemaining <= 1)
                {   
                    await actor.Ping();
                }
                else
                {
                    await actor.ReentrantCall(new ReentrantCallOptions { CallsRemaining = callOptions.CallsRemaining - 1 });
                }                
            } 
            finally 
            {
                await UpdateState(false);
            }
        }

        public Task<State> GetState() 
        {
            return this.StateManager.GetOrAddStateAsync<State>("reentrant-record", new State());
        }

        private async Task UpdateState(bool isEnter)
        {
            var state = await this.StateManager.GetOrAddStateAsync<State>("reentrant-record", new State());
            state.Records.Add(new CallRecord { IsEnter = true, Timestamp = System.DateTime.Now });
            await this.StateManager.SetStateAsync<State>("reentrant-record", state);            
        }
    }
}