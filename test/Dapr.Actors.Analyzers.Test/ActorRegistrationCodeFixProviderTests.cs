namespace Dapr.Actors.Analyzers.Test;

public class ActorRegistrationCodeFixProviderTests
{
    [Fact]
    public async Task RegisterActor()
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

            class TestActor : Actor
            { 
                public TestActor(ActorHost host) : base(host)
                {
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
                        options.Actors.RegisterActor<TestActor>();
                    });
                }
            }

            class TestActor : Actor
            { 
                public TestActor(ActorHost host) : base(host)
                {
                }
            }
            ";

        await VerifyCodeFix.RunTest<ActorRegistrationCodeFixProvider>(code, expectedChangedCode);
    }

    [Fact]
    public async Task RegisterActor_WhenAddActorsIsNotFound()
    {
        var code = @"
            using Dapr.Actors.Runtime;
            using Microsoft.AspNetCore.Builder;
            
            public static class Program
            {
                public static void Main()
                {
                    var builder = WebApplication.CreateBuilder();

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
            
            public static class Program
            {
                public static void Main()
                {
                    var builder = WebApplication.CreateBuilder();
                    
                    builder.Services.AddActors(options =>
                    {
                        options.Actors.RegisterActor<TestActor>();
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

        await VerifyCodeFix.RunTest<ActorRegistrationCodeFixProvider>(code, expectedChangedCode);
    }

    [Fact]
    public async Task RegisterActor_WhenAddActorsIsNotFound_TopLevelStatements()
    {
        var code = @"            
            using Dapr.Actors.Runtime;
            using Microsoft.AspNetCore.Builder;

            var builder = WebApplication.CreateBuilder();
                
            var app = builder.Build();

            namespace TestNamespace
            {
                class TestActor : Actor
                {
                    public TestActor(ActorHost host) : base(host)
                    {
                    }
                }
            }
            ";

        var expectedChangedCode = @"
            using Dapr.Actors.Runtime;
            using Microsoft.AspNetCore.Builder;
            
            var builder = WebApplication.CreateBuilder();
                    
            builder.Services.AddActors(options =>
            {
                options.Actors.RegisterActor<TestNamespace.TestActor>();
            });

            var app = builder.Build();

            namespace TestNamespace
            {
                class TestActor : Actor
                {
                    public TestActor(ActorHost host) : base(host)
                    {
                    }
                }
            }
            ";

        await VerifyCodeFix.RunTest<ActorRegistrationCodeFixProvider>(code, expectedChangedCode);
    }
}
