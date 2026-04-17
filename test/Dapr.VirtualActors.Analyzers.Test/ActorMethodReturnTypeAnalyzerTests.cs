// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Dapr.Analyzers.Common;

namespace Dapr.VirtualActors.Analyzers.Test;

public class ActorMethodReturnTypeAnalyzerTests
{
    [Fact]
    public async Task TaskReturnType_NoDiagnostic()
    {
        const string testCode = """
            using System.Threading.Tasks;
            using Dapr.VirtualActors;

            public interface IMyActor : IVirtualActor
            {
                Task DoWorkAsync();
            }
            """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorMethodReturnTypeAnalyzer>(testCode);
    }

    [Fact]
    public async Task TaskOfTReturnType_NoDiagnostic()
    {
        const string testCode = """
            using System.Threading.Tasks;
            using Dapr.VirtualActors;

            public interface IMyActor : IVirtualActor
            {
                Task<string> GetNameAsync();
                Task<int> GetCountAsync();
            }
            """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorMethodReturnTypeAnalyzer>(testCode);
    }

    [Fact]
    public async Task VoidReturnType_ReportsDiagnosticDAPRVACT004()
    {
        const string testCode = """
            using Dapr.VirtualActors;

            public interface IMyActor : IVirtualActor
            {
                void DoWork();
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(AnalyzerDiagnostics.ActorMethodMustReturnTask)
            .WithSpan(5, 10, 5, 16)
            .WithMessage("Actor interface method 'DoWork' must return Task or Task<T>");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorMethodReturnTypeAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task IntReturnType_ReportsDiagnosticDAPRVACT004()
    {
        const string testCode = """
            using Dapr.VirtualActors;

            public interface IMyActor : IVirtualActor
            {
                int GetCount();
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(AnalyzerDiagnostics.ActorMethodMustReturnTask)
            .WithSpan(5, 9, 5, 17)
            .WithMessage("Actor interface method 'GetCount' must return Task or Task<T>");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorMethodReturnTypeAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task MultipleInvalidMethods_ReportsAllDiagnostics()
    {
        const string testCode = """
            using Dapr.VirtualActors;

            public interface IMyActor : IVirtualActor
            {
                void Fire();
                string GetName();
            }
            """;

        var expected1 = VerifyAnalyzer.Diagnostic(AnalyzerDiagnostics.ActorMethodMustReturnTask)
            .WithSpan(5, 10, 5, 14)
            .WithMessage("Actor interface method 'Fire' must return Task or Task<T>");

        var expected2 = VerifyAnalyzer.Diagnostic(AnalyzerDiagnostics.ActorMethodMustReturnTask)
            .WithSpan(6, 12, 6, 19)
            .WithMessage("Actor interface method 'GetName' must return Task or Task<T>");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorMethodReturnTypeAnalyzer>(testCode, expected1, expected2);
    }

    [Fact]
    public async Task InterfaceNotDerivedFromIVirtualActor_NoDiagnostic()
    {
        const string testCode = """
            public interface ISomeInterface
            {
                void DoWork();
                int GetCount();
            }
            """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorMethodReturnTypeAnalyzer>(testCode);
    }

    [Fact]
    public async Task IVirtualActorItself_NoDiagnostic()
    {
        // The IVirtualActor interface itself should not trigger diagnostics
        const string testCode = """
            using System.Threading.Tasks;
            using Dapr.VirtualActors;

            // Downstream interface that adds methods
            public interface IMyActor : IVirtualActor
            {
                Task DoWorkAsync();
            }
            """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<ActorMethodReturnTypeAnalyzer>(testCode);
    }
}
