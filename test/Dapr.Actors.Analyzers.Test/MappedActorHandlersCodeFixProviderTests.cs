// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.Actors.Analyzers.Test;
using Dapr.Analyzers.Common;

namespace Dapr.Actors.Analyzers.Tests;

public class MappedActorHandlersCodeFixProviderTests
{
    [Fact]
    public async Task RegisterActor()
    {
        const string code = """
                                        using Dapr.Actors.Runtime;
                                        using Microsoft.AspNetCore.Builder;
                                        using Microsoft.Extensions.DependencyInjection;
                                        
                                        public static class Program
                                        {
                                            public static void Main()
                                            {
                                                var builder = WebApplication.CreateBuilder();
                                                    
                                                builder.Services.AddActors(options =>
                                                {                        
                                                    options.Actors.RegisterActor<TestActor>();
                                                    options.UseJsonSerialization = true;
                                                });
                                                var app = builder.Build();
                                            }
                                        }
                                        class TestActor : Actor
                                        { 
                                            public TestActor(ActorHost host) : base(host)
                                            {
                                            }
                                        }
                            """;

        const string expectedChangedCode = """
                                                       using Dapr.Actors.Runtime;
                                                       using Microsoft.AspNetCore.Builder;
                                                       using Microsoft.Extensions.DependencyInjection;
                                                       
                                                       public static class Program
                                                       {
                                                           public static void Main()
                                                           {
                                                               var builder = WebApplication.CreateBuilder();
                                                                   
                                                               builder.Services.AddActors(options =>
                                                               {                        
                                                                   options.Actors.RegisterActor<TestActor>();
                                                                   options.UseJsonSerialization = true;
                                                               });
                                                               var app = builder.Build();
                                                               app.MapActorsHandlers();
                                                           }
                                                       }
                                                       class TestActor : Actor
                                                       { 
                                                           public TestActor(ActorHost host) : base(host)
                                                           {
                                                           }
                                                       }
                                           """;

        await VerifyCodeFix.RunTest<MappedActorHandlersCodeFixProvider>(code, expectedChangedCode,
            typeof(object).Assembly.Location, Utilities.GetReferences(), Utilities.GetAnalyzers());
    }

    [Fact]
    public async Task RegisterActor_TopLevelStatements()
    {
        const string code = """
                            
                                        using Dapr.Actors.Runtime;
                                        using Microsoft.AspNetCore.Builder;
                                        using Microsoft.Extensions.DependencyInjection;
                                        
                                        var builder = WebApplication.CreateBuilder();
                                                    
                                        builder.Services.AddActors(options =>
                                        {                        
                                            options.Actors.RegisterActor<TestActor>();
                                            options.UseJsonSerialization = true;
                                        });
                                        var app = builder.Build();
                                        class TestActor : Actor
                                        { 
                                            public TestActor(ActorHost host) : base(host)
                                            {
                                            }
                                        }
                                        
                            """;

        const string expectedChangedCode = """
                                           
                                                       using Dapr.Actors.Runtime;
                                                       using Microsoft.AspNetCore.Builder;
                                                       using Microsoft.Extensions.DependencyInjection;
                                                       
                                                       var builder = WebApplication.CreateBuilder();
                                                                   
                                                       builder.Services.AddActors(options =>
                                                       {                        
                                                           options.Actors.RegisterActor<TestActor>();
                                                           options.UseJsonSerialization = true;
                                                       });
                                                       var app = builder.Build();
                                                       app.MapActorsHandlers();
                                                       class TestActor : Actor
                                                       { 
                                                           public TestActor(ActorHost host) : base(host)
                                                           {
                                                           }
                                                       }
                                                       
                                           """;

        await VerifyCodeFix.RunTest<MappedActorHandlersCodeFixProvider>(code, expectedChangedCode,
            typeof(object).Assembly.Location, Utilities.GetReferences(), Utilities.GetAnalyzers());
    }
}
