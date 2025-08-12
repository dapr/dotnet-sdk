﻿// ------------------------------------------------------------------------
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

public class ActorRegistrationAnalyzerTests
{
    [Fact]
    public async Task NotRegistered_ReportDiagnostic_DAPR4002()
    {
        const string testCode = """
                                using Dapr.Actors.Runtime;
                                class TestActor : Actor
                                { 
                                    public TestActor(ActorHost host) : base(host)
                                    {
                                    }
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(ActorRegistrationAnalyzer.DiagnosticDescriptorActorRegistration)
            .WithSpan(2, 7, 2, 16).WithMessage("The actor type 'TestActor' is not registered with the dependency injection provider");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorRegistrationAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task NotRegistered_ReportDiagnostic_DAPR4002_FullyQualified()
    {
        const string testCode = """
                                class TestActor : Dapr.Actors.Runtime.Actor
                                { 
                                    public TestActor(Dapr.Actors.Runtime.ActorHost host) : base(host)
                                    {
                                    }
                                }             
                                """;

        var expected = VerifyAnalyzer.Diagnostic(ActorRegistrationAnalyzer.DiagnosticDescriptorActorRegistration)
            .WithSpan(1, 7, 1, 16).WithMessage("The actor type 'TestActor' is not registered with the dependency injection provider");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorRegistrationAnalyzer>(testCode, expected);
    }


    [Fact]
    public async Task NotRegistered_ReportDiagnostic_DAPR4002_NamespaceAlias()
    {
        const string testCode = """
                                        using alias = Dapr.Actors.Runtime;
                                        class TestActor : alias.Actor
                                        { 
                                            public TestActor(alias.ActorHost host) : base(host)
                                            {
                                            }
                                        }             
                                """;

        var expected = VerifyAnalyzer.Diagnostic(ActorRegistrationAnalyzer.DiagnosticDescriptorActorRegistration)
            .WithSpan(2, 15, 2, 24).WithMessage("The actor type 'TestActor' is not registered with the dependency injection provider");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorRegistrationAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task Registered_ReportNoDiagnostic()
    {
        const string testCode = """
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

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorRegistrationAnalyzer>(testCode);
    }

    [Fact]
    public async Task Registered_ReportNoDiagnostic_WithNamespace()
    {
        const string testCode = """
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
                                            options.Actors.RegisterActor<TestNamespace.TestActor>();
                                            options.UseJsonSerialization = true;
                                        });
                                        var app = builder.Build();
                                        app.MapActorsHandlers();
                                    }
                                }
                                namespace TestNamespace
                                {
                                    class TestActor : Actor
                                    { 
                                        public TestActor(ActorHost host) : base(host)
                                        {
                                        }
                                    }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorRegistrationAnalyzer>(testCode);
    }

    [Fact]
    public async Task Registered_ReportNoDiagnostic_WithNamespaceAlias()
    {
        const string testCode = """
                                        using Dapr.Actors.Runtime;
                                        using Microsoft.AspNetCore.Builder;
                                        using Microsoft.Extensions.DependencyInjection;
                                        using alias = TestNamespace;
                                        public static class Program
                                        {
                                            public static void Main()
                                            {
                                                var builder = WebApplication.CreateBuilder();
                                                
                                                builder.Services.AddActors(options =>
                                                {                    
                                                    options.Actors.RegisterActor<alias.TestActor>();
                                                    options.UseJsonSerialization = true;
                                                });
                                                var app = builder.Build();
                                                app.MapActorsHandlers();
                                            }
                                        }
                                        namespace TestNamespace
                                        {
                                            class TestActor : Actor
                                            { 
                                                public TestActor(ActorHost host) : base(host)
                                                {
                                                }
                                            }
                                        }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorRegistrationAnalyzer>(testCode);
    }
}
