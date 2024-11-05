using System.Text;
using Dapr.Actors.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Generators.Test;

public class ActorRegistrationGeneratorTests
{
    [Fact]
    public void TestActorRegistrationGenerator()
    {
        const string source = @"
using Dapr.Actors.Runtime;

public class MyActor : Actor, IMyActor
{
    public MyActor(ActorHost host) : base(host) { } 
}

public interface IMyActor : IActor
{
}
";

        const string expectedGeneratedCode = @"
using Microsoft.Extensions.DependencyInjection;
using Dapr.Actors.Runtime;

/// <summary>
/// Extension methods for registering Dapr actors.
/// </summary>
public static class ActorRegistrationExtensions
{
    /// <summary>
    /// Registers all discovered actor types with the Dapr actor runtime.
    /// </summary>
    public static void RegisterAllActors(this IServiceCollection services)
    {
        services.AddActors(options => 
        {
            options.Actors.RegisterActor<MyActor>();
        });
    }
}";

        var generatedCode = GetGeneratedCode(source);
        Assert.Equal(expectedGeneratedCode.Trim(), generatedCode.Trim());
    }

    private static string GetGeneratedCode(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8));
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Actor).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create("TestCompilation",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ActorRegistrationGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
        Assert.Single(generatedTrees);

        var generatedCode = generatedTrees[0].ToString();
        return generatedCode;
    }
}
