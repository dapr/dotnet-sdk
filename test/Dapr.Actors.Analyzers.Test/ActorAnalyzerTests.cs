using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Analyzers.Test;

public class ActorAnalyzerTests
{
    public class ActorNotRegistered
    {
        [Fact]
        public async Task ReportDiagnostic_DAPR0001()
        {
            var testCode = @"
                using Dapr.Actors.Runtime;

                class TestActor : Actor
                { 
                    public TestActor(ActorHost host) : base(host)
                    {
                    }
                }
                ";

            var expected = VerifyAnalyzer.Diagnostic("DAPR0001", DiagnosticSeverity.Warning)
                .WithSpan(4, 23, 4, 32).WithMessage("The actor class 'TestActor' is not registered");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task ReportDiagnostic_DAPR0001_FullyQualified()
        {
            var testCode = @"
                class TestActor : Dapr.Actors.Runtime.Actor
                { 
                    public TestActor(Dapr.Actors.Runtime.ActorHost host) : base(host)
                    {
                    }
                }                
                ";

            var expected = VerifyAnalyzer.Diagnostic("DAPR0001", DiagnosticSeverity.Warning)
                .WithSpan(2, 23, 2, 32).WithMessage("The actor class 'TestActor' is not registered");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task ReportDiagnostic_DAPR0001_NamespaceAlias()
        {
            var testCode = @"
                using alias = Dapr.Actors.Runtime;

                class TestActor : alias.Actor
                { 
                    public TestActor(alias.ActorHost host) : base(host)
                    {
                    }
                }                
                ";

            var expected = VerifyAnalyzer.Diagnostic("DAPR0001", DiagnosticSeverity.Warning)
                .WithSpan(4, 23, 4, 32).WithMessage("The actor class 'TestActor' is not registered");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }
    }

    public class ActorRegistered
    {
        [Fact]
        public async Task ReportNoDiagnostic()
        {
            var testCode = @"
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

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ReportNoDiagnostic_WithNamespace()
        {
            var testCode = @"
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
                            options.Actors.RegisterActor<TestNamespace.TestActor>();
                            options.UseJsonSerialization = true;
                        });

                        var app = builder.Build();

                        app.MapActorsHandlers();
                    }
                }

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

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task ReportNoDiagnostic_WithNamespaceAlias ()
        {
            var testCode = @"
                using Dapr.Actors.Runtime;
                using Microsoft.AspNetCore.Builder;
                using Microsoft.Extensions.DependencyInjection;
                using alias = TestNamespace;

                public static class Program
                {
                    public static void Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        
                        builder.Services.AddActors(options =>
                        {                    
                            options.Actors.RegisterActor<alias.TestActor>();
                            options.UseJsonSerialization = true;
                        });

                        var app = builder.Build();

                        app.MapActorsHandlers();
                    }
                }

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

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode);
        }
    }
    
    public class JsonSerialization
    {
        [Fact]
        public async Task ReportDiagnostic()
        {
            var testCode = @"
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
                        });

                        var app = builder.Build();

                        app.MapActorsHandlers();
                    }
                }
                ";

            var expected = VerifyAnalyzer.Diagnostic("DAPR0002", DiagnosticSeverity.Warning)
                .WithSpan(12, 25, 14, 27).WithMessage("Add options.UseJsonSerialization to support interoperability with non-.NET actors");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task ReportNoDiagnostic()
        {
            var testCode = @"
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
                            options.UseJsonSerialization = true;
                        });

                        var app = builder.Build();

                        app.MapActorsHandlers();
                    }
                }
                ";

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode);
        }
    }

    public class MapActorsHandlers
    {
        [Fact]
        public async Task ReportDiagnostic()
        {
            var testCode = @"
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
                            options.UseJsonSerialization = true;
                        });

                        var app = builder.Build();
                    }
                }
                ";

            var expected = VerifyAnalyzer.Diagnostic("DAPR0003", DiagnosticSeverity.Warning)
                .WithSpan(12, 25, 15, 27).WithMessage("Call app.MapActorsHandlers to map endpoints for Dapr actors");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task ReportNoDiagnostic()
        {
            var testCode = @"
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
                            options.UseJsonSerialization = true;
                        });

                        var app = builder.Build();

                        app.MapActorsHandlers();
                    }
                }
                ";            

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode);
        }
    }
}
