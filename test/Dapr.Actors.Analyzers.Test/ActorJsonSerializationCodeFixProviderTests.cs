namespace Dapr.Actors.Analyzers.Test;

public class ActorJsonSerializationCodeFixProviderTests
{
    [Fact]
    public async Task UseJsonSerialization()
    {
        var code = @"
            using Dapr.Actors.Runtime;
            using Microsoft.Extensions.DependencyInjection;
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    services.AddActors(options =>
                    {                        
                    });
                }
            }
            ";

        var expectedChangedCode = @"
            using Dapr.Actors.Runtime;
            using Microsoft.Extensions.DependencyInjection;
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    services.AddActors(options =>
                    {
                        options.UseJsonSerialization = true;
                    });
                }
            }
            ";

        await VerifyCodeFix.RunTest<ActorJsonSerializationCodeFixProvider>(code, expectedChangedCode);
    }
}
