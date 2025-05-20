using Dapr.Actors.Generators.Extensions;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Actors.Generators.Test.Extensions;

public class INamespaceExtensionsTests
{
    [Fact]
    public void GetNamespaceTypes_ReturnsAllTypesInNamespace()
    {
        // Arrange
        const string source = @"
namespace TestNamespace
{
    public class ClassA { }
    public class ClassB { }

    namespace NestedNamespace
    {
        public class ClassC { }
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("TestCompilation", new[] { syntaxTree });
        var namespaceSymbol = compilation.GlobalNamespace.GetNamespaceMembers().FirstOrDefault(n => n.Name == "TestNamespace");

        // Act
        if (namespaceSymbol != null)
        {
            var types = namespaceSymbol.GetNamespaceTypes().ToList();

            // Assert
            Assert.NotNull(namespaceSymbol);
            Assert.Equal(3, types.Count);
            Assert.Contains(types, t => t.Name == "ClassA");
            Assert.Contains(types, t => t.Name == "ClassB");
            Assert.Contains(types, t => t.Name == "ClassC");
        }
    }
}
