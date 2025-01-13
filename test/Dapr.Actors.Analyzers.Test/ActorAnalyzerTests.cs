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

                class TestActor : Actor
                { 
                    public TestActor(ActorHost host) : base(host)
                    {
                    }
                }
                ";

            var startupCode = @"                
                using Microsoft.Extensions.DependencyInjection;

                internal static class Extensions
                {
                    public static void AddApplicationServices(this IServiceCollection services)
                    {
                        services.AddActors(options =>
                        {                
                            options.Actors.RegisterActor<TestActor>();
                            options.UseJsonSerialization = true;
                        });
                    }
                }
                ";

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, startupCode);
        }

        [Fact]
        public async Task ReportNoDiagnostic_WithNamespace()
        {
            var testCode = @"
                using Dapr.Actors.Runtime;
                
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

            var startupCode = @"
                using Microsoft.Extensions.DependencyInjection;

                internal static class Extensions
                {
                    public static void AddApplicationServices(this IServiceCollection services)
                    {
                        services.AddActors(options =>
                        {
                            options.Actors.RegisterActor<TestNamespace.TestActor>();
                            options.UseJsonSerialization = true;
                        });
                    }
                }
                ";

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, startupCode);
        }

        [Fact]
        public async Task ReportNoDiagnostic_WithNamespaceAlias ()
        {
            var testCode = @"
                using Dapr.Actors.Runtime;
                
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

            var startupCode = @"
                using Microsoft.Extensions.DependencyInjection;
                using alias = TestNamespace;

                internal static class Extensions
                {
                    public static void AddApplicationServices(this IServiceCollection services)
                    {
                        services.AddActors(options =>
                        {
                            options.Actors.RegisterActor<alias.TestActor>();
                            options.UseJsonSerialization = true;
                        });
                    }
                }
                ";

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, startupCode);
        }
    }
    
    public class JsonSerialization
    {
        [Fact]
        public async Task ReportDiagnostic()
        {
            var testCode = @"
                using Dapr.Actors.Runtime;
                using Microsoft.Extensions.DependencyInjection;

                internal static class Extensions
                {
                    public static void AddApplicationServices(this IServiceCollection services)
                    {
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

            var expected = VerifyAnalyzer.Diagnostic("DAPR0002", DiagnosticSeverity.Warning)
                .WithSpan(9, 25, 12, 27).WithMessage("Add options.UseJsonSerialization to support interoperability with non-.NET actors");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task ReportNoDiagnostic()
        {
            var testCode = @"
                using Dapr.Actors.Runtime;
                using Microsoft.Extensions.DependencyInjection;

                internal static class Extensions
                {
                    public static void AddApplicationServices(this IServiceCollection services)
                    {
                        services.AddActors(options =>
                        {
                            options.Actors.RegisterActor<TestActor>();
                            options.UseJsonSerialization = true;
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

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode);
        }
    }
}
