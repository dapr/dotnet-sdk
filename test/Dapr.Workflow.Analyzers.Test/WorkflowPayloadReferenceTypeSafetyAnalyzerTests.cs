using Dapr.Analyzers.Common;
using Microsoft.CodeAnalysis.Testing;

namespace Dapr.Workflow.Analyzers.Test;

public sealed class WorkflowPayloadReferenceTypeSafetyAnalyzerTests
{
    public static TheoryData<string, string, bool> ContractPairs => new()
    {
        { "Order", "Order", false },
        { "Order", "PayloadReference<Order>", false },
        { "PayloadReference<Order>", "Order", false },
        { "PayloadReference<Order>", "PayloadReference<Order>", false },
        { "SpecialOrder", "PayloadReference<Order>", false },
        { "PayloadReference<SpecialOrder>", "Order", false },
        { "PayloadReference<SpecialOrder>", "PayloadReference<Order>", false },
        { "PayloadReference<Order>", "object", false },
        { "PayloadReference<Order>", "PayloadReference", false },
        { "int", "PayloadReference<long>", false },
        { "PayloadReference<int>", "long", false },
        { "PayloadReference<int>", "PayloadReference<long>", false },
        { "int", "PayloadReference<int?>", false },
        { "PayloadReference<int?>", "int?", false },
        { "List<Order>", "PayloadReference<List<Order>>", false },
        { "PayloadReference<Order[]>", "Order[]", false },
        { "(int Id, Order Item)", "PayloadReference<(long Id, Order Item)>", false },
        { "Wrapper<Order>", "PayloadReference<Wrapper<Order>>", false },
        { "Order", "Customer", true },
        { "Order", "PayloadReference<Customer>", true },
        { "PayloadReference<Order>", "Customer", true },
        { "PayloadReference<Order>", "PayloadReference<Customer>", true },
        { "Order", "PayloadReference<SpecialOrder>", true },
        { "PayloadReference<Order>", "SpecialOrder", true },
        { "PayloadReference<Order>", "PayloadReference<SpecialOrder>", true },
        { "Order", "PayloadReference", true },
        { "PayloadReference", "Order", true },
        { "string", "int", true },
        { "string", "PayloadReference<int>", true },
        { "PayloadReference<string>", "int", true },
        { "long", "PayloadReference<int>", true },
        { "PayloadReference<long>", "int", true },
        { "char", "int", true },
        { "char", "PayloadReference<int>", true },
        { "PayloadReference<char>", "int", true },
        { "PayloadReference<char>", "PayloadReference<int>", true },
        { "PayloadReference<int?>", "int", true },
        { "ConvertibleOrder", "Order", true },
        { "ConvertibleOrder", "PayloadReference<Order>", true },
        { "PayloadReference<ConvertibleOrder>", "Order", true },
        { "PayloadReference<ConvertibleOrder>", "PayloadReference<Order>", true },
        { "(ConvertibleOrder Item, int Id)", "(Order Item, long Id)", true },
        { "PayloadReference<(ConvertibleOrder Item, int Id)>", "(Order Item, long Id)", true },
        { "(char Value, int Id)", "PayloadReference<(int Value, long Id)>", true },
        { "List<Order>", "List<PayloadReference<Order>>", true },
        { "List<PayloadReference<Order>>", "List<Order>", true },
        { "Order[]", "PayloadReference<Order>[]", true },
        { "PayloadReference<Order>[]", "Order[]", true },
        { "Wrapper<Order>", "Wrapper<PayloadReference<Order>>", true },
        { "Task<Order>", "Task<PayloadReference<Order>>", true },
        { "(Order Item, int Id)", "(PayloadReference<Order> Item, int Id)", true },
        { "(PayloadReference<Order> Item, int Id)", "(Order Item, int Id)", true },
        { "PayloadReference<List<Order>>", "List<PayloadReference<Order>>", true },
        { "PayloadReference<List<Order>>", "PayloadReference<List<PayloadReference<Order>>>", true },
        { "PayloadReference<PayloadReference<Order>>", "Order", true },
        { "Order", "PayloadReference<PayloadReference<Order>>", true },
        { "Order", "Other.PayloadReference<Order>", true },
        { "Other.PayloadReference<Order>", "Order", true },
        { "Other.PayloadReference<Order>", "PayloadReference<Order>", true },
        { "PayloadReference<Order>", "Other.PayloadReference<Order>", true }
    };

    [Fact]
    public Task VerifyAutomaticReferenceInputAcrossBoundaries() =>
        VerifyBoundariesAsync("Order", "PayloadReference<Order>", "int", "int", false, false);

    [Theory]
    [MemberData(nameof(ContractPairs))]
    public Task VerifyBoundaryCompatibility(string actualType, string expectedType, bool reportsMismatch) =>
        VerifyBoundariesAsync(actualType, expectedType, actualType, expectedType, reportsMismatch, reportsMismatch);

    [Fact]
    public Task VerifyReferenceInputDoesNotHideOutputMismatch() =>
        VerifyBoundariesAsync("Order", "PayloadReference<Order>", "string", "int", false, true);

    [Fact]
    public Task VerifyReferenceOutputDoesNotHideInputMismatch() =>
        VerifyBoundariesAsync("string", "int", "PayloadReference<Order>", "Order", true, false);

    [Theory]
    [InlineData("Stream", "PayloadReference")]
    [InlineData("FileStream", "PayloadReference")]
    [InlineData("MemoryStream", "PayloadReference<Order>")]
    [InlineData("Stream", "PayloadReference<Order>")]
    public Task VerifyStreamReferencesAreAllowedOnlyForInitialClientScheduling(string actualType, string expectedType) =>
        VerifyBoundariesAsync(actualType, expectedType, "int", "int", true, false,
            reportSchedulingInput: false);

    [Theory]
    [InlineData("Stream", "Order")]
    [InlineData("Stream", "Wrapper<PayloadReference>")]
    [InlineData("Stream[]", "PayloadReference")]
    [InlineData("Wrapper<Stream>", "PayloadReference<Order>")]
    [InlineData("ConvertibleStream", "PayloadReference")]
    public Task VerifyStreamCompatibilityDoesNotHideOtherMismatches(string actualType, string expectedType) =>
        VerifyBoundariesAsync(actualType, expectedType, "int", "int", true, false,
            """
            public sealed class ConvertibleStream
            {
                public static implicit operator Stream(ConvertibleStream value) => Stream.Null;
            }
            """);

    [Theory]
    [InlineData("PayloadReference")]
    [InlineData("PayloadReference<Order>")]
    public Task VerifyStreamInputDoesNotAcceptImpostorReferences(string expectedType) =>
        VerifyBoundariesAsync("Stream", expectedType, "int", "int", true, false,
            """
            namespace Dapr.Workflow.Payloads
            {
                public class PayloadReference { }
                public sealed class PayloadReference<T> : PayloadReference { }
            }
            """);

    [Theory]
    [InlineData("Order", "PayloadReference<Order>")]
    [InlineData("PayloadReference<Order>", "Order")]
    public Task VerifySameFullyQualifiedNameFromAnotherAssemblyIsNotAnSdkReference(
        string actualType, string expectedType) =>
        VerifyBoundariesAsync(actualType, expectedType, actualType, expectedType, true, true,
            """
            namespace Dapr.Workflow.Payloads
            {
                public sealed class PayloadReference<T> { }
            }
            """);

    private static async Task VerifyBoundariesAsync(
        string actualInput, string expectedInput, string declaredOutput, string requestedOutput,
        bool reportInput, bool reportOutput, string additionalTypes = "", bool? reportSchedulingInput = null)
    {
        var inputArguments = Enumerable.Range(0, 5)
            .Select(index => Mark("input: input", index,
                index < 2 ? reportSchedulingInput ?? reportInput : reportInput)).ToArray();
        var childCall = Mark($"CallChildWorkflowAsync<{requestedOutput}>", 5, reportOutput);
        var activityCall = Mark($"CallActivityAsync<{requestedOutput}>", 6, reportOutput);
        var source = $$"""
                       #pragma warning disable CS0436
                       using System.Collections.Generic;
                       using System.IO;
                       using System.Threading.Tasks;
                       using Dapr.Workflow;
                       using Dapr.Workflow.Payloads;

                       public class Order { }
                       public sealed class SpecialOrder : Order { }
                       public sealed class Customer { }
                       public sealed class Wrapper<T> { }
                       public sealed class ConvertibleOrder
                       {
                           public static implicit operator Order(ConvertibleOrder value) => new Order();
                       }
                       namespace Other
                       {
                           public sealed class PayloadReference<T> { }
                       }
                       {{additionalTypes}}

                       public sealed class TargetWorkflow : Workflow<{{expectedInput}}, {{declaredOutput}}>
                       {
                           public override Task<{{declaredOutput}}> RunAsync(
                               WorkflowContext context, {{expectedInput}} input) =>
                               Task.FromResult(default({{declaredOutput}}));
                       }

                       public abstract class ActivityBase<TInput, TOutput> : WorkflowActivity<TInput, TOutput>
                       {
                           public override Task<TOutput> RunAsync(WorkflowActivityContext context, TInput input) =>
                               Task.FromResult(default(TOutput));
                       }

                       public sealed class TargetActivity : ActivityBase<{{expectedInput}}, {{declaredOutput}}> { }

                       public static class Caller
                       {
                           public static async Task InvokeAsync(DaprWorkflowClient client,
                               IDaprWorkflowClient clientInterface, WorkflowContext context, {{actualInput}} input)
                           {
                               await client.ScheduleNewWorkflowAsync(nameof(TargetWorkflow), {{inputArguments[0]}});
                               await clientInterface.ScheduleNewWorkflowAsync(nameof(TargetWorkflow), {{inputArguments[1]}});
                               await context.{{childCall}}(nameof(TargetWorkflow), {{inputArguments[2]}});
                               await context.ScheduleNewDetachedWorkflowAsync(nameof(TargetWorkflow), {{inputArguments[3]}});
                               await context.{{activityCall}}(nameof(TargetActivity), {{inputArguments[4]}});
                           }
                       }
                       """;

        var expected = new List<DiagnosticResult>();
        if (reportInput)
        {
            for (var index = reportSchedulingInput == false ? 2 : 0; index < inputArguments.Length; index++)
                expected.Add(VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.InputTypeMismatchDescriptor)
                    .WithLocation(index)
                    .WithArguments(DisplayType(actualInput), DisplayType(expectedInput),
                        index == 4 ? "workflow activity" : "workflow",
                        index == 4 ? "TargetActivity" : "TargetWorkflow"));
        }

        if (reportOutput)
        {
            expected.Add(VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.OutputTypeMismatchDescriptor)
                .WithLocation(5)
                .WithArguments(DisplayType(requestedOutput), DisplayType(declaredOutput), "workflow",
                    "TargetWorkflow"));
            expected.Add(VerifyAnalyzer.Diagnostic(WorkflowTypeSafetyAnalyzer.OutputTypeMismatchDescriptor)
                .WithLocation(6)
                .WithArguments(DisplayType(requestedOutput), DisplayType(declaredOutput), "workflow activity",
                    "TargetActivity"));
        }

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<WorkflowTypeSafetyAnalyzer>(source, expected.ToArray());
    }

    private static string Mark(string code, int location, bool report) =>
        report ? $"{{|#{location}:{code}|}}" : code;

    private static string DisplayType(string type) => type.Replace("Other.", string.Empty);
}
