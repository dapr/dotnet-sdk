namespace Dapr.Pubsub.Analyzers.Test;

public class MapSubscribeHandlerCodeFixProviderTests
{
    [Fact]
    public async Task MapSubscribeHandler()
    {
        var code = @"
            using Microsoft.AspNetCore.Builder;

            public static class Program
            {
                public static void Main()
                {
                    var builder = WebApplication.CreateBuilder();
                    var app = builder.Build();

                    app.MapPost(""/subscribe"", () => {})
                        .WithTopic(""pubSubName"", ""topicName"");
                }
            }
            ";

        var expectedChangedCode = @"
            using Microsoft.AspNetCore.Builder;

            public static class Program
            {
                public static void Main()
                {
                    var builder = WebApplication.CreateBuilder();
                    var app = builder.Build();

                    app.MapSubscribeHandler();

                    app.MapPost(""/subscribe"", () => {})
                        .WithTopic(""pubSubName"", ""topicName"");
                }
            }
            ";

        await VerifyCodeFix.RunTest<MapSubscribeHandlerCodeFixProvider>(code, expectedChangedCode);
    }

    [Fact]
    public async Task MapSubscribeHandler_TopLevelStatements()
    {
        var code = @"
            using Microsoft.AspNetCore.Builder;

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.MapPost(""/subscribe"", () => {})
                .WithTopic(""pubSubName"", ""topicName"");
            ";

        var expectedChangedCode = @"
            using Microsoft.AspNetCore.Builder;

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.MapSubscribeHandler();

            app.MapPost(""/subscribe"", () => {})
                .WithTopic(""pubSubName"", ""topicName"");
            ";

        await VerifyCodeFix.RunTest<MapSubscribeHandlerCodeFixProvider>(code, expectedChangedCode);
    }
}
