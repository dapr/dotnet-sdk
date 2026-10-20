using System.Collections.Immutable;
using System.Text.Json;
using Dapr.Common.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Workflow.Analyzers.Test;

public sealed class SemanticSafetyTests
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);
    private static readonly MetadataReference[] References = Utilities.GetReferences()
        .Concat(((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)))
        .GroupBy(r => r.Display).Select(g => g.First()).ToArray();

    private static void AssertCompiles(Compilation compilation) =>
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

    [Theory]
    [InlineData("", "private readonly string _service; public Example(string service) { _service = service; }", "", false)]
    [InlineData("(string service)", "private readonly string _service = service;", "", false)]
    [InlineData("(string service)", "private string Value => service;", "", false)]
    [InlineData("", "public Example(string service) { }", "", true)]
    [InlineData("(string service)", "", "", true)]
    [InlineData("", "public Example(string service) { }", "public class Caller { object Create() => new Example(\"x\"); }", false)]
    [InlineData("(string service)", "", "public class Caller { object Create() => new Example(\"x\"); }", false)]
    [InlineData("", "public Example(string service) { } public Example() { }", "", false)]
    [InlineData("", "public Example(string service) { } public Example(int x) : this(x.ToString()) { }", "", false)]
    [InlineData("", "public Example(string service) { }", "public class Derived : Example { public Derived() : base(\"x\") { } }", false)]
    [InlineData("(string service)", "", "public class Derived() : Example(\"x\") { }", false)]
    [InlineData("", "public Example(string service) { _ = nameof(service); }", "", false)]
    public async Task ConstructorFixIsOfferedOnlyForUnreferencedUnambiguousParameters(
        string primary, string members, string caller, bool offered)
    {
        var source = $$"""
            using Dapr.Workflow;
            using System.Threading.Tasks;
            public class Example{{primary}} : Workflow<string, string>
            {
                {{members}}
                public override Task<string> RunAsync(WorkflowContext context, string input) => Task.FromResult(input);
            }
            """;
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Safety", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(ParseOptions).AddMetadataReferences(References);
        var document = project.AddDocument("Workflow.cs", source);
        // Separate documents ensure the fix checks the solution, not just the constructor body.
        project = document.Project.AddDocument("Caller.cs", caller).Project;
        document = project.GetDocument(document.Id)!;
        var compilation = (await project.GetCompilationAsync(TestContext.Current.CancellationToken))!;
        AssertCompiles(compilation);
        var diagnostics = await compilation.WithAnalyzers([new WorkflowDependencyInjectionAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        var diagnostic = diagnostics.First(d => d.Id == "DAPR1305");
        var actions = new List<CodeAction>();
        await new WorkflowDependencyInjectionCodeFixProvider().RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), TestContext.Current.CancellationToken));
        Assert.Equal(offered ? 1 : 0, actions.Count);
        if (!offered)
            return;
        var operations = await actions[0].GetOperationsAsync(TestContext.Current.CancellationToken);
        var changes = Assert.Single(operations.OfType<ApplyChangesOperation>());
        AssertCompiles((await changes.ChangedSolution.GetProject(project.Id)!.GetCompilationAsync(TestContext.Current.CancellationToken))!);
    }

    [Theory]
    [InlineData("client.ScheduleNewWorkflowAsync(nameof(Target), input: new Payload())", false, "DAPR1303")]
    [InlineData("context.CallChildWorkflowAsync<string>(nameof(Target), new Payload())", false, "DAPR1303")]
    [InlineData("context.ScheduleNewDetachedWorkflowAsync(nameof(Target), input: new Payload())", false, "DAPR1303")]
    [InlineData("context.CallActivityAsync<string>(nameof(Target), new Payload())", true, "DAPR1303")]
    [InlineData("context.CallChildWorkflowAsync<string>(nameof(Target), \"x\")", false, "DAPR1304")]
    [InlineData("context.CallActivityAsync<string>(nameof(Target), \"x\")", true, "DAPR1304")]
    public async Task UserDefinedConversionsAreNotWireConversions(string call, bool activity, string expectedId)
    {
        var output = expectedId == "DAPR1304" ? "Payload" : "string";
        var source = $$"""
            using Dapr.Workflow;
            using System.Threading.Tasks;
            public class Payload
            {
                public string Value { get; set; } = "data";
                public static implicit operator string(Payload value) => value.Value;
            }
            public sealed class Target : {{(activity ? "WorkflowActivity" : "Workflow")}}<string, {{output}}>
            {
                public override Task<{{output}}> RunAsync({{(activity ? "WorkflowActivityContext" : "WorkflowContext")}} context, string input)
                    => Task.FromResult({{(output == "Payload" ? "new Payload()" : "input")}});
            }
            public class Caller
            {
                public Task Invoke(DaprWorkflowClient client, WorkflowContext context) => {{call}};
            }
            """;
        var diagnostics = await Analyze(source);
        Assert.Equal(expectedId, Assert.Single(diagnostics).Id);
    }

    [Theory]
    [InlineData("string", "(string)new Payload()")]
    [InlineData("long", "42")]
    [InlineData("long?", "42")]
    [InlineData("Base", "new Derived()")]
    [InlineData("object", "new Derived()")]
    public async Task ExecutedConversionsAndCompatiblePayloadsRemainValid(string inputType, string input)
    {
        var source = $$"""
            using Dapr.Workflow;
            using System.Threading.Tasks;
            public class Payload { public static implicit operator string(Payload p) => "data"; }
            public class Base { public string Value { get; set; } = "data"; }
            public class Derived : Base { }
            public sealed class Target : Workflow<{{inputType}}, {{inputType}}>
            {
                public override Task<{{inputType}}> RunAsync(WorkflowContext context, {{inputType}} input) => Task.FromResult(input);
            }
            public class Caller
            {
                public Task Invoke(DaprWorkflowClient client) => client.ScheduleNewWorkflowAsync(nameof(Target), input: {{input}});
            }
            """;
        Assert.Empty(await Analyze(source));
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        var compilation = CSharpCompilation.Create("Safety",
            [CSharpSyntaxTree.ParseText(source, ParseOptions)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        AssertCompiles(compilation);
        return await compilation.WithAnalyzers([new WorkflowTypeSafetyAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("int", "long", false)]
    [InlineData("int?", "long?", false)]
    [InlineData("Derived", "Base", false)]
    [InlineData("(int, string)", "(long, string)", false)]
    [InlineData("(Payload, int)", "(string, int)", true)]
    [InlineData("char", "int", true)]
    public async Task OutputCompatibilityChecksSerializedContracts(string declared, string requested, bool mismatch)
    {
        var source = $$"""
            using Dapr.Workflow;
            using System.Threading.Tasks;
            public class Base { public string Value { get; set; } = "data"; }
            public class Derived : Base { }
            public class Payload { public static implicit operator string(Payload p) => "data"; }
            public sealed class Target : Workflow<string, {{declared}}>
            {
                public override Task<{{declared}}> RunAsync(WorkflowContext context, string input)
                    => Task.FromResult(default({{declared}}));
            }
            public class Caller
            {
                public Task Invoke(WorkflowContext context) => context.CallChildWorkflowAsync<{{requested}}>(nameof(Target), "input");
            }
            """;
        var diagnostics = await Analyze(source);
        if (mismatch)
            Assert.Equal("DAPR1304", Assert.Single(diagnostics).Id);
        else
            Assert.Empty(diagnostics);
    }

    [Fact]
    public void SerializerDoesNotInvokeConversionOperators()
    {
        var serializer = new JsonDaprSerializer();
        var payload = new ConvertiblePayload();
        var serialized = serializer.Serialize<object>(payload);
        Assert.Throws<JsonException>(() => serializer.Deserialize<string>(serialized));
        Assert.Equal("data", serializer.Deserialize<string>(serializer.Serialize((string)payload)));
        Assert.Equal(42L, serializer.Deserialize<long>(serializer.Serialize<object>(42)));
        Assert.Throws<JsonException>(() => serializer.Deserialize<int>(serializer.Serialize<object>('x')));
        Assert.Equal("data", serializer.Deserialize<BasePayload>(serializer.Serialize<object>(new DerivedPayload()))!.Value);
    }

    public sealed class ConvertiblePayload
    {
        public string Value { get; set; } = "data";
        public static implicit operator string(ConvertiblePayload input) => input.Value;
    }

    public class BasePayload
    {
        public string Value { get; set; } = "data";
    }

    public sealed class DerivedPayload : BasePayload;
}
