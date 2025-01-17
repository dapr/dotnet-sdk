namespace Dapr.Actors.Analyzers.Test;

public class MapActorsHandlersCodeFixProviderTests
{
    [Fact]
    public async Task RegisterActor()
    {
        var code = @"
            using Dapr.Actors.Runtime;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;
            
            public static class Program
            {
                public static void Main()
                {
                    var builder = WebApplication.CreateBuilder();
                        
                    builder.Services.AddActors(options =>
                    {                        
                        options.Actors.RegisterActor<TestActor>();
                        options.UseJsonSerialization = true;
                    });

                    var app = builder.Build();
                }
            }

            class TestActor : Actor
            { 
                public TestActor(ActorHost host) : base(host)
                {
                }
            }
            ";

        var expectedChangedCode = @"
            using Dapr.Actors.Runtime;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;
            
            public static class Program
            {
                public static void Main()
                {
                    var builder = WebApplication.CreateBuilder();
                        
                    builder.Services.AddActors(options =>
                    {                        
                        options.Actors.RegisterActor<TestActor>();
                        options.UseJsonSerialization = true;
                    });

                    var app = builder.Build();

                    app.MapActorsHandlers();
                }
            }

            class TestActor : Actor
            { 
                public TestActor(ActorHost host) : base(host)
                {
                }
            }
            ";

        await VerifyCodeFix.RunTest<MapActorsHandlersCodeFixProvider>(code, expectedChangedCode);
    }

    [Fact]
    public async Task RegisterActor_TopLevelStatements()
    {
        var code = @"
            using Dapr.Actors.Runtime;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;
            
            var builder = WebApplication.CreateBuilder();
                        
            builder.Services.AddActors(options =>
            {                        
                options.Actors.RegisterActor<TestActor>();
                options.UseJsonSerialization = true;
            });

            var app = builder.Build();

            class TestActor : Actor
            { 
                public TestActor(ActorHost host) : base(host)
                {
                }
            }
            ";

        var expectedChangedCode = @"
            using Dapr.Actors.Runtime;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.Extensions.DependencyInjection;
            
            var builder = WebApplication.CreateBuilder();
                        
            builder.Services.AddActors(options =>
            {                        
                options.Actors.RegisterActor<TestActor>();
                options.UseJsonSerialization = true;
            });

            var app = builder.Build();

            app.MapActorsHandlers();

            class TestActor : Actor
            { 
                public TestActor(ActorHost host) : base(host)
                {
                }
            }
            ";

        await VerifyCodeFix.RunTest<MapActorsHandlersCodeFixProvider>(code, expectedChangedCode);
    }
}
