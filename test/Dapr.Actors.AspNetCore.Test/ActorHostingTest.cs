// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Linq;
using Dapr.Actors.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dapr.Actors.AspNetCore
{
    public class ActorHostingTest
    {
        [Fact]
        public void CanRegisterActorsInSingleCalls()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions();
            services.AddActors(options =>
            {
                options.Actors.RegisterActor<TestActor1>();
                options.Actors.RegisterActor<TestActor2>();
            });
            
            var runtime = services.BuildServiceProvider().GetRequiredService<ActorRuntime>();

            Assert.Collection(
                runtime.RegisteredActors.Select(r => r.Type.ActorTypeName).OrderBy(t => t),
                t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor1)).ActorTypeName, t),
                t => Assert.Equal(ActorTypeInformation.Get(typeof(TestActor2)).ActorTypeName, t));
        }

        [Fact]
        public void CanRegisterActorsInMultipleCalls()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions();
            services.AddActors(options =>
            {
                options.Actors.RegisterActor<TestActor1>();
            });
            
            services.AddActors(options =>
            {
                options.Actors.RegisterActor<TestActor2>();
            });

            var runtime = services.BuildServiceProvider().GetRequiredService<ActorRuntime>();

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
