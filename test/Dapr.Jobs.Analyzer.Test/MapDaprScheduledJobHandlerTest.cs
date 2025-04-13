using Dapr.Analyzers.Common;
using Microsoft.CodeAnalysis.Testing;

namespace Dapr.Jobs.Analyzers.Test;

public class DaprJobsAnalyzerAnalyzerTests
{

#if  NET8_0
    private static readonly ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net80;
#elif NET9_0
    private static readonly ReferenceAssemblies referenceAssemblies = ReferenceAssemblies.Net.Net90;
#endif

    [Fact]
    public async Task AnalyzeJobSchedulerHandler_ShouldRaiseDiagnostic_WhenJobHasNoEndpointMapping()
    {
        const string testCode = """
                                
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
                                
                                                        daprJobsClient.ScheduleJobAsync("myJob", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                                                            Encoding.UTF8.GetBytes("This is a test"), repeats: 10).GetAwaiter().GetResult();
                                                    }
                                                }
                                """;

        var expected = VerifyAnalyzer.Diagnostic(MapDaprScheduledJobHandlerAnalyzer.DaprJobHandlerRule)
            .WithSpan(22, 25, 23, 83)
            .WithMessage(
                "Job invocations require the MapDaprScheduledJobHandler be set and configured for job name 'myJob' on IEndpointRouteBuilder");
        
        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MapDaprScheduledJobHandlerAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task AnalyzeJobSchedulerHandler_ShouldNotRaiseDiagnostic_WhenScheduleJobIsNotCalled()
    {
        const string testCode = """
                                
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
                                                    }
                                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MapDaprScheduledJobHandlerAnalyzer>(testCode);
    }

    [Fact]
    public async Task AnalyzeJobSchedulerHandler_ShouldRaiseDiagnostic_ForEachInstanceOfScheduledJobsDontHaveMappings()
    {
        const string testCode = """
                                
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
                                
                                                        daprJobsClient.ScheduleJobAsync("myJob", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                                                            Encoding.UTF8.GetBytes("This is a test"), repeats: 10).GetAwaiter().GetResult();
                                                        daprJobsClient.ScheduleJobAsync("myJob2", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                                                            Encoding.UTF8.GetBytes("This is a test"), repeats: 10).GetAwaiter().GetResult();
                                                    }
                                                }
                                """;

        var expected1 = VerifyAnalyzer.Diagnostic(MapDaprScheduledJobHandlerAnalyzer.DaprJobHandlerRule)
            .WithSpan(22, 25, 23, 83)
            .WithMessage(
                "Job invocations require the MapDaprScheduledJobHandler be set and configured for job name 'myJob' on IEndpointRouteBuilder");
        var expected2 = VerifyAnalyzer.Diagnostic(MapDaprScheduledJobHandlerAnalyzer.DaprJobHandlerRule)
            .WithSpan(24, 25, 25, 83)
            .WithMessage("Job invocations require the MapDaprScheduledJobHandler be set and configured for job name 'myJob2' on IEndpointRouteBuilder");
        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MapDaprScheduledJobHandlerAnalyzer>(testCode, expected1, expected2);
    }

    [Fact]
    public async Task AnalyzeJobSchedulerHandler_ShouldNotRaiseDiagnostic_WhenJobHasEndpointMapping()
    {
        const string testCode = """
                                
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
                                
                                                        daprJobsClient.ScheduleJobAsync("myJob", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                                                            Encoding.UTF8.GetBytes("This is a test"), repeats: 10).GetAwaiter().GetResult();
                                                        daprJobsClient.ScheduleJobAsync("myJob2", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                                                            Encoding.UTF8.GetBytes("This is a test"), repeats: 10).GetAwaiter().GetResult();
                                
                                                        app.MapDaprScheduledJobHandler(async (string jobName, ReadOnlyMemory<byte> jobPayload) =>
                                                        {
                                                            return Task.CompletedTask;
                                                        }, TimeSpan.FromSeconds(5));
                                                    }
                                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MapDaprScheduledJobHandlerAnalyzer>(testCode);
    }

    [Fact]
    public async Task AnalyzeJobSchedulerHandler_ShouldNotRaiseDiagnostic_WhenJobHasEndpointMappingIrrespectiveOfNumberOfMethodCallsOnScheduleJob()
    {
        const string testCode = """
                                
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
                                
                                                        daprJobsClient.ScheduleJobAsync("myJob", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                                                            Encoding.UTF8.GetBytes("This is a test"), repeats: 10).GetAwaiter().GetResult();
                                                        await daprJobsClient.ScheduleJobAsync("myJob2", DaprJobSchedule.FromDuration(TimeSpan.FromSeconds(2)),
                                                            Encoding.UTF8.GetBytes("This is a test"), repeats: 10);
                                
                                                        app.MapDaprScheduledJobHandler(async (string jobName, ReadOnlyMemory<byte> jobPayload) =>
                                                        {
                                                            return Task.CompletedTask;
                                                        }, TimeSpan.FromSeconds(5));
                                                    }
                                                }
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MapDaprScheduledJobHandlerAnalyzer>(testCode);
    }

    [Fact]
    public async Task AnalyzeJobSchedulerHandler_ShouldNotRaiseDiagnostic_WhenScheduleJobDoesNotBelongToDaprJobClient()
    {
        const string testCode = """
                                
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
                                
                                                        await ScheduleJobAsync("myJob");
                                                    }
                                
                                                    public static Task ScheduleJobAsync(string jobNAme)
                                                    {
                                                        return Task.CompletedTask;
                                                    }
                                                }
                                                
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<MapDaprScheduledJobHandlerAnalyzer>(testCode);
    }
}
