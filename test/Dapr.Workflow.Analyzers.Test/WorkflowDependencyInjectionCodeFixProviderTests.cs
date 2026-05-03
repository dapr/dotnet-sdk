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

using System.Collections.Immutable;
using Dapr.Analyzers.Common;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Workflow.Analyzers.Test;

public sealed class WorkflowDependencyInjectionCodeFixProviderTests
{
    private static ImmutableArray<DiagnosticAnalyzer> Analyzers =>
        [new WorkflowDependencyInjectionAnalyzer()];

    // -------------------------------------------------------------------------
    // Regular constructor
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemovesParameter_FromRegularConstructor_SingleParam()
    {
        const string code = """
                            using Dapr.Workflow;
                            using System.Threading.Tasks;

                            public interface IMyService { }

                            public sealed class OrderWorkflow : Workflow<string, string>
                            {
                                public OrderWorkflow(IMyService service) { }

                                public override Task<string> RunAsync(WorkflowContext context, string input)
                                    => Task.FromResult(input);
                            }

                            public static class Program { public static void Main() { } }
                            """;

        const string expectedChangedCode = """
                                           using Dapr.Workflow;
                                           using System.Threading.Tasks;

                                           public interface IMyService { }

                                           public sealed class OrderWorkflow : Workflow<string, string>
                                           {
                                               public OrderWorkflow() { }

                                               public override Task<string> RunAsync(WorkflowContext context, string input)
                                                   => Task.FromResult(input);
                                           }

                                           public static class Program { public static void Main() { } }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowDependencyInjectionCodeFixProvider>(
            code,
            expectedChangedCode,
            typeof(object).Assembly.Location,
            Utilities.GetReferences(),
            Analyzers);
    }

    [Fact]
    public async Task RemovesFirstParameter_FromRegularConstructor_MultipleParams()
    {
        // When there are multiple parameters the fix fires once per parameter.
        // Each invocation removes exactly the one flagged parameter.
        // This test targets the first parameter.
        const string code = """
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

                            public static class Program { public static void Main() { } }
                            """;

        // The code fix only removes the first flagged parameter; the second still requires its own fix.
        // VerifyCodeFix.RunTest targets the first diagnostic (serviceA).
        const string expectedChangedCode = """
                                           using Dapr.Workflow;
                                           using System.Threading.Tasks;

                                           public interface IServiceA { }
                                           public interface IServiceB { }

                                           public sealed class OrderWorkflow : Workflow<string, string>
                                           {
                                               public OrderWorkflow(IServiceB serviceB) { }

                                               public override Task<string> RunAsync(WorkflowContext context, string input)
                                                   => Task.FromResult(input);
                                           }

                                           public static class Program { public static void Main() { } }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowDependencyInjectionCodeFixProvider>(
            code,
            expectedChangedCode,
            typeof(object).Assembly.Location,
            Utilities.GetReferences(),
            Analyzers);
    }

    [Fact]
    public async Task RemovesSecondParameter_FromRegularConstructor_MultipleParams()
    {
        // Targets the second diagnostic (serviceB) to verify that removing a non-first
        // parameter does not disturb the leading trivia of the remaining first parameter.
        const string code = """
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

                            public static class Program { public static void Main() { } }
                            """;

        const string expectedChangedCode = """
                                           using Dapr.Workflow;
                                           using System.Threading.Tasks;

                                           public interface IServiceA { }
                                           public interface IServiceB { }

                                           public sealed class OrderWorkflow : Workflow<string, string>
                                           {
                                               public OrderWorkflow(IServiceA serviceA) { }

                                               public override Task<string> RunAsync(WorkflowContext context, string input)
                                                   => Task.FromResult(input);
                                           }

                                           public static class Program { public static void Main() { } }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowDependencyInjectionCodeFixProvider>(
            code,
            expectedChangedCode,
            typeof(object).Assembly.Location,
            Utilities.GetReferences(),
            Analyzers,
            diagnosticIndex: 1);
    }

    [Fact]
    public async Task RemovesParameter_FromRegularConstructor_ConcreteType()
    {
        const string code = """
                            using Dapr.Workflow;
                            using System.Threading.Tasks;

                            public class MyConcreteService { }

                            public sealed class OrderWorkflow : Workflow<string, string>
                            {
                                public OrderWorkflow(MyConcreteService service) { }

                                public override Task<string> RunAsync(WorkflowContext context, string input)
                                    => Task.FromResult(input);
                            }

                            public static class Program { public static void Main() { } }
                            """;

        const string expectedChangedCode = """
                                           using Dapr.Workflow;
                                           using System.Threading.Tasks;

                                           public class MyConcreteService { }

                                           public sealed class OrderWorkflow : Workflow<string, string>
                                           {
                                               public OrderWorkflow() { }

                                               public override Task<string> RunAsync(WorkflowContext context, string input)
                                                   => Task.FromResult(input);
                                           }

                                           public static class Program { public static void Main() { } }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowDependencyInjectionCodeFixProvider>(
            code,
            expectedChangedCode,
            typeof(object).Assembly.Location,
            Utilities.GetReferences(),
            Analyzers);
    }

    [Fact]
    public async Task RemovesParameter_FromRegularConstructor_IndirectSubclass()
    {
        const string code = """
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

                            public static class Program { public static void Main() { } }
                            """;

        const string expectedChangedCode = """
                                           using Dapr.Workflow;
                                           using System.Threading.Tasks;

                                           public interface IMyService { }

                                           public abstract class BaseOrderWorkflow : Workflow<string, string> { }

                                           public sealed class ConcreteOrderWorkflow : BaseOrderWorkflow
                                           {
                                               public ConcreteOrderWorkflow() { }

                                               public override Task<string> RunAsync(WorkflowContext context, string input)
                                                   => Task.FromResult(input);
                                           }

                                           public static class Program { public static void Main() { } }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowDependencyInjectionCodeFixProvider>(
            code,
            expectedChangedCode,
            typeof(object).Assembly.Location,
            Utilities.GetReferences(),
            Analyzers);
    }

    // -------------------------------------------------------------------------
    // Primary constructor
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemovesParameter_FromPrimaryConstructor_SingleParam()
    {
        const string code = """
                            using Dapr.Workflow;
                            using System.Threading.Tasks;

                            public interface IMyService { }

                            public sealed class OrderWorkflow(IMyService service) : Workflow<string, string>
                            {
                                public override Task<string> RunAsync(WorkflowContext context, string input)
                                    => Task.FromResult(input);
                            }

                            public static class Program { public static void Main() { } }
                            """;

        const string expectedChangedCode = """
                                           using Dapr.Workflow;
                                           using System.Threading.Tasks;

                                           public interface IMyService { }

                                           public sealed class OrderWorkflow() : Workflow<string, string>
                                           {
                                               public override Task<string> RunAsync(WorkflowContext context, string input)
                                                   => Task.FromResult(input);
                                           }

                                           public static class Program { public static void Main() { } }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowDependencyInjectionCodeFixProvider>(
            code,
            expectedChangedCode,
            typeof(object).Assembly.Location,
            Utilities.GetReferences(),
            Analyzers);
    }

    [Fact]
    public async Task RemovesFirstParameter_FromPrimaryConstructor_MultipleParams()
    {
        const string code = """
                            using Dapr.Workflow;
                            using System.Threading.Tasks;

                            public interface IServiceA { }
                            public interface IServiceB { }

                            public sealed class OrderWorkflow(IServiceA serviceA, IServiceB serviceB) : Workflow<string, string>
                            {
                                public override Task<string> RunAsync(WorkflowContext context, string input)
                                    => Task.FromResult(input);
                            }

                            public static class Program { public static void Main() { } }
                            """;

        const string expectedChangedCode = """
                                           using Dapr.Workflow;
                                           using System.Threading.Tasks;

                                           public interface IServiceA { }
                                           public interface IServiceB { }

                                           public sealed class OrderWorkflow(IServiceB serviceB) : Workflow<string, string>
                                           {
                                               public override Task<string> RunAsync(WorkflowContext context, string input)
                                                   => Task.FromResult(input);
                                           }

                                           public static class Program { public static void Main() { } }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowDependencyInjectionCodeFixProvider>(
            code,
            expectedChangedCode,
            typeof(object).Assembly.Location,
            Utilities.GetReferences(),
            Analyzers);
    }

    [Fact]
    public async Task RemovesSecondParameter_FromPrimaryConstructor_MultipleParams()
    {
        // Targets the second diagnostic (serviceB) to verify that removing a non-first
        // primary constructor parameter does not disturb the first parameter's trivia.
        const string code = """
                            using Dapr.Workflow;
                            using System.Threading.Tasks;

                            public interface IServiceA { }
                            public interface IServiceB { }

                            public sealed class OrderWorkflow(IServiceA serviceA, IServiceB serviceB) : Workflow<string, string>
                            {
                                public override Task<string> RunAsync(WorkflowContext context, string input)
                                    => Task.FromResult(input);
                            }

                            public static class Program { public static void Main() { } }
                            """;

        const string expectedChangedCode = """
                                           using Dapr.Workflow;
                                           using System.Threading.Tasks;

                                           public interface IServiceA { }
                                           public interface IServiceB { }

                                           public sealed class OrderWorkflow(IServiceA serviceA) : Workflow<string, string>
                                           {
                                               public override Task<string> RunAsync(WorkflowContext context, string input)
                                                   => Task.FromResult(input);
                                           }

                                           public static class Program { public static void Main() { } }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowDependencyInjectionCodeFixProvider>(
            code,
            expectedChangedCode,
            typeof(object).Assembly.Location,
            Utilities.GetReferences(),
            Analyzers,
            diagnosticIndex: 1);
    }

    [Fact]
    public async Task RemovesParameter_FromPrimaryConstructor_IndirectSubclass()
    {
        const string code = """
                            using Dapr.Workflow;
                            using System.Threading.Tasks;

                            public interface IMyService { }

                            public abstract class BaseOrderWorkflow : Workflow<string, string> { }

                            public sealed class ConcreteOrderWorkflow(IMyService service) : BaseOrderWorkflow
                            {
                                public override Task<string> RunAsync(WorkflowContext context, string input)
                                    => Task.FromResult(input);
                            }

                            public static class Program { public static void Main() { } }
                            """;

        const string expectedChangedCode = """
                                           using Dapr.Workflow;
                                           using System.Threading.Tasks;

                                           public interface IMyService { }

                                           public abstract class BaseOrderWorkflow : Workflow<string, string> { }

                                           public sealed class ConcreteOrderWorkflow() : BaseOrderWorkflow
                                           {
                                               public override Task<string> RunAsync(WorkflowContext context, string input)
                                                   => Task.FromResult(input);
                                           }

                                           public static class Program { public static void Main() { } }
                                           """;

        await VerifyCodeFix.RunTest<WorkflowDependencyInjectionCodeFixProvider>(
            code,
            expectedChangedCode,
            typeof(object).Assembly.Location,
            Utilities.GetReferences(),
            Analyzers);
    }
}
