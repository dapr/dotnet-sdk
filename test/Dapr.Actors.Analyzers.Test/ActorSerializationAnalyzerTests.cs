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

public class ActorSerializationAnalyzerTests
{
#if NET8_0
    private static readonly ReferenceAssemblies assemblies = ReferenceAssemblies.Net.Net80;
#elif NET9_0
    private static readonly ReferenceAssemblies assemblies = ReferenceAssemblies.Net.Net90;
#elif NET10_0
    private static readonly ReferenceAssemblies assemblies = ReferenceAssemblies.Net.Net100;
#endif

    private static CSharpAnalyzerTest<ActorSerializationAnalyzer, DefaultVerifier> CreateTest() =>
        new()
        {
            ReferenceAssemblies = assemblies.AddPackages([new("Dapr.Actors", "1.16.1")])
        };

    [Fact]
    public async Task ActorInterface_WithoutIActor_ShouldReportDAPR1405()
    {
        var context = CreateTest();
        context.TestCode = """
            using System.Threading.Tasks;
            using Dapr.Actors;
            using Dapr.Actors.Runtime;

            public interface ITestActor
            {
                Task<string> GetDataAsync();
            }

            public class TestActor : Actor, ITestActor
            {
                public TestActor(ActorHost host) : base(host) { }
                public Task<string> GetDataAsync() => Task.FromResult("data");
            }
            """;

        // From AnalyzeInterfaceDeclaration: interface identifier location
        context.ExpectedDiagnostics.Add(
            new DiagnosticResult(ActorSerializationAnalyzer.ActorInterfaceMissingIActor)
                .WithSpan(5, 18, 5, 28)
                .WithArguments("ITestActor"));

        // From CheckActorInterfaces: class identifier location
        context.ExpectedDiagnostics.Add(
            new DiagnosticResult(ActorSerializationAnalyzer.ActorInterfaceMissingIActor)
                .WithSpan(10, 14, 10, 23)
                .WithArguments("ITestActor"));

        // From CheckActorClassImplementsIActorInterface: class identifier location
        context.ExpectedDiagnostics.Add(
            new DiagnosticResult(ActorSerializationAnalyzer.ActorClassMissingInterface)
                .WithSpan(10, 14, 10, 23)
                .WithArguments("TestActor"));

        await context.RunAsync();
    }

    [Fact]
    public async Task ActorInterface_WithIActor_ShouldNotReportDAPR1405()
    {
        var context = CreateTest();
        context.TestCode = """
            using System.Threading.Tasks;
            using Dapr.Actors;
            using Dapr.Actors.Runtime;

            public interface ITestActor : IActor
            {
                Task<string> GetDataAsync();
            }

            public class TestActor : Actor, ITestActor
            {
                public TestActor(ActorHost host) : base(host) { }
                public Task<string> GetDataAsync() => Task.FromResult("data");
            }
            """;

        context.ExpectedDiagnostics.Clear();
        await context.RunAsync();
    }

    [Fact]
    public async Task EnumWithoutEnumMember_ShouldReportDAPR1406()
    {
        var context = CreateTest();
        context.TestCode = """
            using System.Threading.Tasks;
            using Dapr.Actors.Runtime;

            namespace Test
            {
                public enum TestEnum
                {
                    Value1,
                    Value2
                }
            }
            """;

        context.ExpectedDiagnostics.Add(
            new DiagnosticResult(ActorSerializationAnalyzer.EnumMissingEnumMemberAttribute)
                .WithSpan(8, 9, 8, 15)
                .WithArguments("Value1", "TestEnum"));

        context.ExpectedDiagnostics.Add(
            new DiagnosticResult(ActorSerializationAnalyzer.EnumMissingEnumMemberAttribute)
                .WithSpan(9, 9, 9, 15)
                .WithArguments("Value2", "TestEnum"));

        await context.RunAsync();
    }

    [Fact]
    public async Task EnumWithEnumMember_ShouldNotReportDAPR1406()
    {
        var context = CreateTest();
        context.TestCode = """
            using System.Runtime.Serialization;

            namespace Test
            {
                public enum Season
                {
                    [EnumMember]
                    Spring,
                    [EnumMember]
                    Summer
                }
            }
            """;

        context.ExpectedDiagnostics.Clear();
        await context.RunAsync();
    }

    [Fact]
    public async Task ActorMethodWithComplexParameter_ShouldReportDAPR1409()
    {
        var context = CreateTest();
        context.TestCode = """
            using System.Threading.Tasks;
            using Dapr.Actors;
            using Dapr.Actors.Runtime;

            public class ComplexType
            {
                public string Name { get; set; } = string.Empty;
            }

            public interface ITestActor : IActor
            {
                Task ProcessDataAsync(ComplexType data);
            }

            public class TestActor : Actor, ITestActor
            {
                public TestActor(ActorHost host) : base(host) { }
                public Task ProcessDataAsync(ComplexType data) => Task.CompletedTask;
            }
            """;

        context.ExpectedDiagnostics.Add(
            new DiagnosticResult(ActorSerializationAnalyzer.ActorMethodParameterNeedsValidation)
                .WithSpan(18, 46, 18, 50)
                .WithArguments("data", "ComplexType", "ProcessDataAsync"));

        await context.RunAsync();
    }

    [Fact]
    public async Task ActorMethodWithComplexReturnType_ShouldReportDAPR1410()
    {
        var context = CreateTest();
        context.TestCode = """
            using System.Threading.Tasks;
            using Dapr.Actors;
            using Dapr.Actors.Runtime;

            public class ComplexResult
            {
                public string Value { get; set; } = string.Empty;
            }

            public interface ITestActor : IActor
            {
                Task<ComplexResult> GetResultAsync();
            }

            public class TestActor : Actor, ITestActor
            {
                public TestActor(ActorHost host) : base(host) { }
                public Task<ComplexResult> GetResultAsync() => Task.FromResult(new ComplexResult());
            }
            """;

        context.ExpectedDiagnostics.Add(
            new DiagnosticResult(ActorSerializationAnalyzer.ActorMethodReturnTypeNeedsValidation)
                .WithSpan(18, 32, 18, 46)
                .WithArguments("ComplexResult", "GetResultAsync"));

        await context.RunAsync();
    }

    [Fact]
    public async Task ActorMethodWithDataContractType_ShouldNotReportDAPR1409()
    {
        var context = CreateTest();
        context.TestCode = """
            using System.Runtime.Serialization;
            using System.Threading.Tasks;
            using Dapr.Actors;
            using Dapr.Actors.Runtime;

            [DataContract]
            public class ComplexType
            {
                [DataMember]
                public string Name { get; set; } = string.Empty;
            }

            public interface ITestActor : IActor
            {
                Task ProcessDataAsync(ComplexType data);
            }

            public class TestActor : Actor, ITestActor
            {
                public TestActor(ActorHost host) : base(host) { }
                public Task ProcessDataAsync(ComplexType data) => Task.CompletedTask;
            }
            """;

        context.ExpectedDiagnostics.Clear();
        await context.RunAsync();
    }

    [Fact]
    public async Task ActorMethodWithPrimitiveTypes_ShouldNotReportDiagnostics()
    {
        var context = CreateTest();
        context.TestCode = """
            using System.Threading.Tasks;
            using Dapr.Actors;
            using Dapr.Actors.Runtime;

            public interface ITestActor : IActor
            {
                Task<string> ProcessAsync(string input, int count);
            }

            public class TestActor : Actor, ITestActor
            {
                public TestActor(ActorHost host) : base(host) { }
                public Task<string> ProcessAsync(string input, int count) => Task.FromResult(input);
            }
            """;

        context.ExpectedDiagnostics.Clear();
        await context.RunAsync();
    }

    [Fact]
    public async Task ActorClassWithoutIActorInterface_ShouldReportDAPR1413()
    {
        var context = CreateTest();
        context.TestCode = """
            using System.Threading.Tasks;
            using Dapr.Actors.Runtime;

            public class TestActor : Actor
            {
                public TestActor(ActorHost host) : base(host) { }
                public Task<string> GetDataAsync() => Task.FromResult("data");
            }
            """;

        context.ExpectedDiagnostics.Add(
            new DiagnosticResult(ActorSerializationAnalyzer.ActorClassMissingInterface)
                .WithSpan(4, 14, 4, 23)
                .WithArguments("TestActor"));

        await context.RunAsync();
    }

    [Fact]
    public async Task RecordWithoutDataContract_ShouldReportDAPR1412()
    {
        var context = CreateTest();
        context.TestCode = """
            using System;
            using System.Threading.Tasks;
            using Dapr.Actors;
            using Dapr.Actors.Runtime;

            public record Doodad(Guid Id, string Name);

            public interface IDoodadActor : IActor
            {
                Task<Doodad> GetDoodadAsync();
            }

            public class DoodadActor : Actor, IDoodadActor
            {
                public DoodadActor(ActorHost host) : base(host) { }
                public Task<Doodad> GetDoodadAsync() => Task.FromResult(new Doodad(Guid.NewGuid(), "test"));
            }
            """;

        context.ExpectedDiagnostics.Add(
            new DiagnosticResult(ActorSerializationAnalyzer.RecordTypeNeedsDataContractAttributes)
                .WithSpan(16, 25, 16, 39)
                .WithArguments("Doodad"));

        await context.RunAsync();
    }
}
