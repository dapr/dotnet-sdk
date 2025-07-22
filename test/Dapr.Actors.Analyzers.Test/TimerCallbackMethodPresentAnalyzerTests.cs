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

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Dapr.Actors.Analyzers.Tests;

public class TimerCallbackMethodPresentAnalyzerTests
{
    #if NET8_0
    private static readonly ReferenceAssemblies assemblies = ReferenceAssemblies.Net.Net80;
    #elif NET9_0
    private static readonly ReferenceAssemblies assemblies = ReferenceAssemblies.Net.Net90;
    #endif
    
    [Fact]
    public async Task TestActor_TimerRegistration_NotPresent()
    {
        var context = new CSharpAnalyzerTest<TimerCallbackMethodPresentAnalyzer, DefaultVerifier>();
        context.ReferenceAssemblies = assemblies.AddPackages([
            new ("Dapr.Actors", "1.15.3")
        ]);

        context.TestCode = """
                           using System;
                           using System.Threading.Tasks;
                           using Dapr.Actors.Runtime;
                           internal sealed class TestActorTimerRegistrationNotPresent(ActorHost host) : Actor(host)
                           {
                               public async Task DoSomethingAsync()
                               {
                                   await Task.Delay(TimeSpan.FromMilliseconds(250));
                               }
                           }
                           """;

        context.ExpectedDiagnostics.Clear();
        await context.RunAsync();
    }

    [Fact]
    public async Task TestActor_TimerRegistration_NameOfCallbackPresent()
    {
        var context = new CSharpAnalyzerTest<TimerCallbackMethodPresentAnalyzer, DefaultVerifier>();
        context.ReferenceAssemblies = assemblies.AddPackages([
            new ("Dapr.Actors", "1.15.3")
        ]);
        

        context.TestCode = """
                           using System;
                           using System.Threading.Tasks;
                           using Dapr.Actors.Runtime;
                           internal sealed class TestActorTimerRegistrationTimerCallbackPresent(ActorHost host) : Actor(host)
                           {
                               public async Task DoSomethingAsync()
                               {
                                   await RegisterTimerAsync("MyTimer", nameof(TimerCallback), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10));
                               }
                           
                               private static async Task TimerCallback(byte[] data)
                               {
                                   await Task.Delay(TimeSpan.FromMilliseconds(250));
                               }
                           }
                           """;

        context.ExpectedDiagnostics.Clear();
        await context.RunAsync();
    }
    
    [Fact]
    public async Task TestActor_TimerRegistration_LiteralCallbackPresent()
    {
        var context = new CSharpAnalyzerTest<TimerCallbackMethodPresentAnalyzer, DefaultVerifier>();
        context.ReferenceAssemblies = assemblies.AddPackages([
            new ("Dapr.Actors", "1.15.3")
        ]);
        

        context.TestCode = """
                           using System;
                           using System.Threading.Tasks;
                           using Dapr.Actors.Runtime;
                           internal sealed class TestActorTimerRegistrationTimerCallbackPresent(ActorHost host) : Actor(host)
                           {
                               public async Task DoSomethingAsync()
                               {
                                   await RegisterTimerAsync("MyTimer", "TimerCallback", null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10));
                               }
                           
                               private static async Task TimerCallback(byte[] data)
                               {
                                   await Task.Delay(TimeSpan.FromMilliseconds(250));
                               }
                           }
                           """;

        context.ExpectedDiagnostics.Clear();
        await context.RunAsync();
    }
    
    [Fact]
    public async Task TestActor_TimerRegistration_CallbackNotPresent()
    {
        var context = new CSharpAnalyzerTest<TimerCallbackMethodPresentAnalyzer, DefaultVerifier>();
        context.ReferenceAssemblies = assemblies.AddPackages([
            new ("Dapr.Actors", "1.15.3")
        ]);

        context.TestCode = """
                           using System;
                           using System.Threading.Tasks;
                           using Dapr.Actors.Runtime;
                           internal sealed class TestActorTimerRegistrationTimerCallbackNotPresent(ActorHost host) : Actor(host)
                           {
                               public async Task DoSomethingAsync()
                               {
                                   await RegisterTimerAsync("MyTimer", "TimerCallback", null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10));
                               }
                           }
                           """;

        context.ExpectedDiagnostics.Add(new DiagnosticResult(TimerCallbackMethodPresentAnalyzer.DaprTimerCallbackMethodRule)
            .WithSpan(8, 45, 8, 60)
            .WithArguments("TimerCallback", "TestActorTimerRegistrationTimerCallbackNotPresent"));
        await context.RunAsync();
    }
}






