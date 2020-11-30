// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Linq;
using Dapr.Actors.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.Actors.AspNetCore
{
    public class ActorHostingTest
    {
        [Fact]
        public void CanRegisterActorsInSingleCalls()
        {
            var builder = new WebHostBuilder();
            builder.UseActors(options =>
            {
                options.Actors.RegisterActor<TestActor1>();
                options.Actors.RegisterActor<TestActor2>();
            });
            
            // Configuring the HTTP pipeline is required. It's ok if it's empty.
            builder.Configure(_ => {});

            var host = builder.Build();
            var runtime = host.Services.GetRequiredService<ActorRuntime>();

            Assert.Collection(
                runtime.RegisteredActors.Select(r => r.Type.ActorTypeName).OrderBy(t => t),
                t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor1)).ActorTypeName, t),
                t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor2)).ActorTypeName, t));
        }

        [Fact]
        public void CanRegisterActorsInMultipleCalls()
        {
            var builder = new WebHostBuilder();
            builder.UseActors(options =>
            {
                options.Actors.RegisterActor<TestActor1>();
            });
            
            builder.UseActors(options =>
            {
                options.Actors.RegisterActor<TestActor2>();
            });

            // Configuring the HTTP pipeline is required. It's ok if it's empty.
            builder.Configure(_ => {});

            var host = builder.Build();
            var runtime = host.Services.GetRequiredService<ActorRuntime>();

            Assert.Collection(
                runtime.RegisteredActors.Select(r => r.Type.ActorTypeName).OrderBy(t => t),
                t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor1)).ActorTypeName, t),
                t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor2)).ActorTypeName, t));
        }

        private interface ITestActor : IActor
        {
        }

        private class TestActor1 : Actor, ITestActor
        {
            public TestActor1(ActorHost host) 
                : base(host)
            {
            }
        }

        private class TestActor2 : Actor, ITestActor
        {
            public TestActor2(ActorHost host) 
                : base(host)
            {
            }
        }
    }
}
