namespace Dapr.Actors.Analyzers.Test;

public class ActorJsonSerializationCodeFixProviderTests
{
    [Fact]
    public async Task UseJsonSerialization()
    {
        var code = @"
            //using Dapr.Actors.Runtime;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;
            
            public static class Program
            {
                public static void Main()
                {
                    var builder = WebApplication.CreateBuilder();
                        
                    builder.Services.AddActors(options =>
                    {                        
                    });

                    var app = builder.Build();

                    app.MapActorsHandlers();
                }
            }
            ";

        var expectedChangedCode = @"
            //using Dapr.Actors.Runtime;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;

            public static class Program
            {
                public static void Main()
                {
                    var builder = WebApplication.CreateBuilder();

                    builder.Services.AddActors(options =>
                    {
                        options.UseJsonSerialization = true;
                    });

                    var app = builder.Build();

                    app.MapActorsHandlers();
                }
            }
            ";

        await VerifyCodeFix.RunTest<ActorJsonSerializationCodeFixProvider>(code, expectedChangedCode);
    }
}
