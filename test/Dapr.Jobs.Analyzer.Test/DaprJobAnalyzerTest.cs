namespace Dapr.Jobs.Analyzer.Tests
{
    using Microsoft.CodeAnalysis.CSharp.Testing;
    using Microsoft.CodeAnalysis.Testing;
    using Microsoft.CodeAnalysis;
    using Microsoft.AspNetCore.Builder;
    using Dapr.Jobs.Models;
    using System.Text;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public class DaprJobsAnalyzerAnalyzerTests
    {

#if NET6_0
        private static readonly ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net60;
#elif NET7_0
        private static readonly ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net70;
#elif NET8_0
        private static readonly ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net80;
#elif NET9_0
        private static readonly ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net90;
#endif

        private static readonly DiagnosticDescriptor DaprJobHandlerRule = new DiagnosticDescriptor(
#pragma warning disable RS2008 // Enable analyzer release tracking
            id: "DAPRJOBS0001",
#pragma warning restore RS2008 // Enable analyzer release tracking
            title: "Ensure Post Mapper handler is present for all the Scheduled Jobs",
            messageFormat: "There should be a mapping post endpoint for each scheduled job to make sure app receives notifications for all the scheduled jobs",
            category: "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        [Fact]
        public async Task AnalyzeJobSchedulerHandler_ShouldRaiseDiagnostic_WhenJobHasNoEndpointMapping()
        {
            var testCode = @"
                using System;
                using System.Text;
                using System.Threading.Tasks;   
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;
                using Dapr.Jobs;
                using Dapr.Jobs.Extensions;
                using Dapr.Jobs.Models;

                public static class Program
                {
                    public static void Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        builder.Services.AddDaprJobsClient();
                        var app = builder.Build();
                        using var scope = app.Services.CreateScope();

                        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

                        daprJobsClient.ScheduleJobAsync(""myJob"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();
                    }
                }";

            var expectedDiagnostic = new DiagnosticResult(DaprJobHandlerRule);

            await VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }

        [Fact]
        public async Task AnalyzeJobSchedulerHandler_ShouldRaiseDiagnostic_WhenJobHasHasEndpointButDoesNotMapToJobName()
        {
            var testCode = @"
                using System;
                using System.Text;
                using System.Threading.Tasks;   
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;
                using Dapr.Jobs;
                using Dapr.Jobs.Extensions;
                using Dapr.Jobs.Models;

                public static class Program
                {
                    public static void Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        builder.Services.AddDaprJobsClient();
                        var app = builder.Build();
                        using var scope = app.Services.CreateScope();

                        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

                        daprJobsClient.ScheduleJobAsync(""myJob"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost(""/job/JobA"", async context =>
                            {
                            });
                        });
                    }
                }";

            var expectedDiagnostic = new DiagnosticResult(DaprJobHandlerRule);

            await VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }

        [Fact]
        public async Task AnalyzeJobSchedulerHandler_ShouldNotRaiseDiagnostic_WhenScheduleJobIsNotCalled()
        {
            var testCode = @"
                using System;
                using System.Text;
                using System.Threading.Tasks;   
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;
                using Dapr.Jobs;
                using Dapr.Jobs.Extensions;
                using Dapr.Jobs.Models;

                public static class Program
                {
                    public static void Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        builder.Services.AddDaprJobsClient();
                        var app = builder.Build();
                        using var scope = app.Services.CreateScope();

                        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost(""/job/JobA"", async context =>
                            {
                            });
                        });
                    }
                }";

            var expectedDiagnostic = new DiagnosticResult(DaprJobHandlerRule);

            await VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task AnalyzeJobSchedulerHandler_ShouldRaiseDiagnostic_ForEachInstanceOfScheduledJobsDontHaveMappings()
        {
            var testCode = @"
                using System;
                using System.Text;
                using System.Threading.Tasks;   
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;
                using Dapr.Jobs;
                using Dapr.Jobs.Extensions;
                using Dapr.Jobs.Models;

                public static class Program
                {
                    public static void Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        builder.Services.AddDaprJobsClient();
                        var app = builder.Build();
                        using var scope = app.Services.CreateScope();

                        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

                        daprJobsClient.ScheduleJobAsync(""myJob"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();
                        daprJobsClient.ScheduleJobAsync(""myJob2"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();
                    }
                }";

            var expectedDiagnostic = new DiagnosticResult(DaprJobHandlerRule);

            await VerifyAnalyzerAsync(testCode, expectedDiagnostic, expectedDiagnostic);
        }

        [Fact]
        public async Task AnalyzeJobSchedulerHandler_ShouldRaiseDiagnostic_OnlyOnceWhenOneJobHasMappingAndOtherDoesNot()
        {
            var testCode = @"
                using System;
                using System.Text;
                using System.Threading.Tasks;   
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;
                using Dapr.Jobs;
                using Dapr.Jobs.Extensions;
                using Dapr.Jobs.Models;

                public static class Program
                {
                    public static void Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        builder.Services.AddDaprJobsClient();
                        var app = builder.Build();
                        using var scope = app.Services.CreateScope();

                        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

                        daprJobsClient.ScheduleJobAsync(""myJob"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();
                        daprJobsClient.ScheduleJobAsync(""myJob2"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost(""/job/myJob"", async context =>
                            {
                            });
                        });
                    }
                }";

            var expectedDiagnostic = new DiagnosticResult(DaprJobHandlerRule);

            await VerifyAnalyzerAsync(testCode, expectedDiagnostic);
        }

        [Fact]
        public async Task AnalyzeJobSchedulerHandler_ShouldNotRaiseDiagnostic_WhenTheMappingExistsAsAParameter()
        {
            var testCode = @"
                using System;
                using System.Text;
                using System.Threading.Tasks;   
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;
                using Dapr.Jobs;
                using Dapr.Jobs.Extensions;
                using Dapr.Jobs.Models;

                public static class Program
                {
                    public static void Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        builder.Services.AddDaprJobsClient();
                        var app = builder.Build();
                        using var scope = app.Services.CreateScope();

                        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

                        daprJobsClient.ScheduleJobAsync(""myJob"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();
                        daprJobsClient.ScheduleJobAsync(""myJob1"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();
                        daprJobsClient.ScheduleJobAsync(""myJob2"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost(""/job/{myJob}"", async context =>
                            {
                            });
                        });
                    }
                }";

            await VerifyAnalyzerAsync(testCode);
        }


        [Fact]
        public async Task AnalyzeJobSchedulerHandler_ShouldNotRaiseDiagnostic_WhenJobHasEndpointMapping()
        {
            var testCode = @"
                using System;
                using System.Text;
                using System.Threading.Tasks;   
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;
                using Dapr.Jobs;
                using Dapr.Jobs.Extensions;
                using Dapr.Jobs.Models;

                public static class Program
                {
                    public static void Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        builder.Services.AddDaprJobsClient();
                        var app = builder.Build();
                        using var scope = app.Services.CreateScope();

                        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

                        daprJobsClient.ScheduleJobAsync(""myJob"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();
                        daprJobsClient.ScheduleJobAsync(""myJob2"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost(""/job/myJob"", async context =>
                            {
                            });
                            endpoints.MapPost(""/job/myJob2"", async context =>
                            {
                            });
                        });
                    }
                }";

            await VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task AnalyzeJobSchedulerHandler_ShouldNotRaiseDiagnostic_WhenJobHasEndpointMappingIrrespectiveOfNumberOfMethodCallsOnScheduleJob()
        {
            var testCode = @"
                using System;
                using System.Text;
                using System.Threading.Tasks;   
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;
                using Dapr.Jobs;
                using Dapr.Jobs.Extensions;
                using Dapr.Jobs.Models;

                public static class Program
                {
                    public static async Task Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        builder.Services.AddDaprJobsClient();
                        var app = builder.Build();
                        using var scope = app.Services.CreateScope();

                        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

                        daprJobsClient.ScheduleJobAsync(""myJob"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10).GetAwaiter().GetResult();
                        await daprJobsClient.ScheduleJobAsync(""myJob2"", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                            Encoding.UTF8.GetBytes(""This is a test""), repeats: 10);

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost(""/job/myJob"", async context =>
                            {
                            });
                            endpoints.MapPost(""/job/myJob2"", async context =>
                            {
                            });
                        });
                    }
                }";

            await VerifyAnalyzerAsync(testCode);
        }

        [Fact]
        public async Task AnalyzeJobSchedulerHandler_ShouldNotRaiseDiagnostic_WhenScheduleJobDoesNotBelongToDaprJobClient()
        {
            var testCode = @"
                using System;
                using System.Text;
                using System.Threading.Tasks;   
                using Microsoft.Extensions.DependencyInjection;
                using Microsoft.AspNetCore.Builder;
                using Dapr.Jobs;
                using Dapr.Jobs.Extensions;
                using Dapr.Jobs.Models;

                public static class Program
                {
                    public static async Task Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        builder.Services.AddDaprJobsClient();
                        var app = builder.Build();
                        using var scope = app.Services.CreateScope();

                        var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

                        await ScheduleJobAsync(""myJob"");
                    }

                    public static Task ScheduleJobAsync(string jobNAme)
                    {
                        return Task.CompletedTask;
                    }
                }
                ";

            await VerifyAnalyzerAsync(testCode);
        }

        private static async Task VerifyAnalyzerAsync(string testCode, params DiagnosticResult[] expectedDiagnostics)
        {
            var test = new CSharpAnalyzerTest<DaprJobsAnalyzerAnalyzer, DefaultVerifier>
            {
                
                TestCode = testCode,
            };

            test.TestState.ReferenceAssemblies = referenceAssemblies;

            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(WebApplication).Assembly.Location));
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(DaprJobsClient).Assembly.Location));
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(DaprJobSchedule).Assembly.Location));
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(EndpointRouteBuilderExtensions).Assembly.Location));
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(IApplicationBuilder).Assembly.Location));
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(
                    typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly.Location));
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(IHost).Assembly.Location));

            test.ExpectedDiagnostics.AddRange(expectedDiagnostics);
            await test.RunAsync();
        }
    }
}
