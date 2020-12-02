// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.Actors.AspNetCore.IntegrationTest.App.ActivationTests
{
    public class DependencyInjectionActor : Actor, IDependencyInjectionActor
    {
        private readonly CounterService counter;
        
        public DependencyInjectionActor(ActorHost host, CounterService counter) 
            : base(host)
        {
            this.counter = counter;
        }

        public Task<int> IncrementAsync()
        {
            return Task.FromResult(this.counter.Value++);
        }
    }

    public interface IDependencyInjectionActor : IActor
    {
        Task<int> IncrementAsync();
    }

    public class CounterService
    {
        public int Value { get; set; }
    }
}
