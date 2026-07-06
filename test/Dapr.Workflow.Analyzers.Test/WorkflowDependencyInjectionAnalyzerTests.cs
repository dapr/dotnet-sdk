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

using Dapr.Analyzers.Common;

namespace Dapr.Workflow.Analyzers.Test;

public sealed class WorkflowDependencyInjectionAnalyzerTests
{
    // -------------------------------------------------------------------------
    // Diagnostics should be reported
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyDiagnostic_WhenWorkflowHasSingleConstructorParameter()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public interface IMyService { }

                                public sealed class OrderWorkflow : Workflow<string, string>
                                {
                                    public OrderWorkflow(IMyService service) { }

                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowDependencyInjectionAnalyzer.WorkflowDependencyInjectionDescriptor)
            .WithSpan(8, 26, 8, 44)
            .WithMessage("Workflow 'OrderWorkflow' has constructor parameter 'service' of type 'IMyService', but dependency injection is not supported in workflow implementations. Move dependencies to a WorkflowActivity instead.");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyDiagnostic_WhenWorkflowHasMultipleConstructorParameters()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public interface IServiceA { }
                                public interface IServiceB { }

                                public sealed class OrderWorkflow : Workflow<string, string>
                                {
                                    public OrderWorkflow(IServiceA serviceA, IServiceB serviceB) { }

                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var expectedA = VerifyAnalyzer.Diagnostic(WorkflowDependencyInjectionAnalyzer.WorkflowDependencyInjectionDescriptor)
            .WithSpan(9, 26, 9, 44)
            .WithMessage("Workflow 'OrderWorkflow' has constructor parameter 'serviceA' of type 'IServiceA', but dependency injection is not supported in workflow implementations. Move dependencies to a WorkflowActivity instead.");

        var expectedB = VerifyAnalyzer.Diagnostic(WorkflowDependencyInjectionAnalyzer.WorkflowDependencyInjectionDescriptor)
            .WithSpan(9, 46, 9, 64)
            .WithMessage("Workflow 'OrderWorkflow' has constructor parameter 'serviceB' of type 'IServiceB', but dependency injection is not supported in workflow implementations. Move dependencies to a WorkflowActivity instead.");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode, expectedA, expectedB);
    }

    [Fact]
    public async Task VerifyDiagnostic_WhenWorkflowHasConcreteTypeConstructorParameter()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public class MyConcreteService { }

                                public sealed class OrderWorkflow : Workflow<string, string>
                                {
                                    public OrderWorkflow(MyConcreteService service) { }

                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowDependencyInjectionAnalyzer.WorkflowDependencyInjectionDescriptor)
            .WithSpan(8, 26, 8, 51)
            .WithMessage("Workflow 'OrderWorkflow' has constructor parameter 'service' of type 'MyConcreteService', but dependency injection is not supported in workflow implementations. Move dependencies to a WorkflowActivity instead.");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyDiagnostic_WhenIndirectWorkflowSubclassHasConstructorParameter()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public interface IMyService { }

                                public abstract class BaseOrderWorkflow : Workflow<string, string> { }

                                public sealed class ConcreteOrderWorkflow : BaseOrderWorkflow
                                {
                                    public ConcreteOrderWorkflow(IMyService service) { }

                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowDependencyInjectionAnalyzer.WorkflowDependencyInjectionDescriptor)
            .WithSpan(10, 34, 10, 52)
            .WithMessage("Workflow 'ConcreteOrderWorkflow' has constructor parameter 'service' of type 'IMyService', but dependency injection is not supported in workflow implementations. Move dependencies to a WorkflowActivity instead.");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode, expected);
    }

    // -------------------------------------------------------------------------
    // No diagnostics should be reported
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NoDiagnostic_WhenWorkflowHasParameterlessConstructor()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow : Workflow<string, string>
                                {
                                    public OrderWorkflow() { }

                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode);
    }

    [Fact]
    public async Task NoDiagnostic_WhenWorkflowHasNoExplicitConstructor()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow : Workflow<string, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode);
    }

    [Fact]
    public async Task NoDiagnostic_WhenActivityHasConstructorParameter()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public interface IMyService { }

                                public sealed class NotifyActivity : WorkflowActivity<string, string>
                                {
                                    private readonly IMyService _service;

                                    public NotifyActivity(IMyService service)
                                    {
                                        _service = service;
                                    }

                                    public override Task<string> RunAsync(WorkflowActivityContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNonWorkflowClassHasConstructorParameter()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public interface IMyService { }

                                public sealed class SomeOtherClass
                                {
                                    public SomeOtherClass(IMyService service) { }
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode);
    }

    [Fact]
    public async Task NoDiagnostic_WhenWorkflowHasOnlyParameterlessConstructorAlongWithNoOtherConstructors()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow : Workflow<int, string>
                                {
                                    public OrderWorkflow() { }

                                    public override Task<string> RunAsync(WorkflowContext context, int input)
                                        => Task.FromResult(input.ToString());
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyDiagnostic_WhenWorkflowUsesPrimaryConstructorWithParameter()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public interface IMyService { }

                                public sealed class OrderWorkflow(IMyService service) : Workflow<string, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowDependencyInjectionAnalyzer.WorkflowDependencyInjectionDescriptor)
            .WithSpan(6, 35, 6, 53)
            .WithMessage("Workflow 'OrderWorkflow' has constructor parameter 'service' of type 'IMyService', but dependency injection is not supported in workflow implementations. Move dependencies to a WorkflowActivity instead.");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task VerifyDiagnostic_WhenWorkflowUsesPrimaryConstructorWithMultipleParameters()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public interface IServiceA { }
                                public interface IServiceB { }

                                public sealed class OrderWorkflow(IServiceA serviceA, IServiceB serviceB) : Workflow<string, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var expectedA = VerifyAnalyzer.Diagnostic(WorkflowDependencyInjectionAnalyzer.WorkflowDependencyInjectionDescriptor)
            .WithSpan(7, 35, 7, 53)
            .WithMessage("Workflow 'OrderWorkflow' has constructor parameter 'serviceA' of type 'IServiceA', but dependency injection is not supported in workflow implementations. Move dependencies to a WorkflowActivity instead.");

        var expectedB = VerifyAnalyzer.Diagnostic(WorkflowDependencyInjectionAnalyzer.WorkflowDependencyInjectionDescriptor)
            .WithSpan(7, 55, 7, 73)
            .WithMessage("Workflow 'OrderWorkflow' has constructor parameter 'serviceB' of type 'IServiceB', but dependency injection is not supported in workflow implementations. Move dependencies to a WorkflowActivity instead.");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode, expectedA, expectedB);
    }

    [Fact]
    public async Task NoDiagnostic_WhenWorkflowUsesPrimaryConstructorWithNoParameters()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public sealed class OrderWorkflow() : Workflow<string, string>
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode);
    }

    [Fact]
    public async Task NoDiagnostic_WhenActivityUsesPrimaryConstructorWithParameter()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public interface IMyService { }

                                public sealed class NotifyActivity(IMyService service) : WorkflowActivity<string, string>
                                {
                                    public override Task<string> RunAsync(WorkflowActivityContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNonDaprGenericBaseClassSubclassHasConstructorParameter()
    {
        // A class that derives from a user-defined generic base class — NOT Dapr.Workflow.Workflow<,> —
        // should never trigger DAPR1305 even if the base class looks structurally similar.
        const string testCode = """
                                using System.Threading.Tasks;

                                public interface IMyService { }

                                // Custom generic base class unrelated to Dapr.Workflow
                                public abstract class Processor<TInput, TOutput>
                                {
                                    public abstract Task<TOutput> RunAsync(TInput input);
                                }

                                public sealed class OrderProcessor : Processor<string, string>
                                {
                                    public OrderProcessor(IMyService service) { }

                                    public override Task<string> RunAsync(string input) => Task.FromResult(input);
                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode);
    }

    [Fact]
    public async Task VerifyDiagnostic_WhenIndirectWorkflowSubclassUsesPrimaryConstructorWithParameter()
    {
        const string testCode = """
                                using Dapr.Workflow;
                                using System.Threading.Tasks;

                                public interface IMyService { }

                                public abstract class BaseOrderWorkflow : Workflow<string, string> { }

                                public sealed class ConcreteOrderWorkflow(IMyService service) : BaseOrderWorkflow
                                {
                                    public override Task<string> RunAsync(WorkflowContext context, string input)
                                        => Task.FromResult(input);
                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(WorkflowDependencyInjectionAnalyzer.WorkflowDependencyInjectionDescriptor)
            .WithSpan(8, 43, 8, 61)
            .WithMessage("Workflow 'ConcreteOrderWorkflow' has constructor parameter 'service' of type 'IMyService', but dependency injection is not supported in workflow implementations. Move dependencies to a WorkflowActivity instead.");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowDependencyInjectionAnalyzer>(testCode, expected);
    }
}
