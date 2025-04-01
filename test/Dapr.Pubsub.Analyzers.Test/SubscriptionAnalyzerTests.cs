// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace Dapr.Pubsub.Analyzers.Test;

public class SubscriptionAnalyzerTests
{
    public class MapSubscribeHandler
    {
        [Fact]
        public async Task ReportDiagnostic_WithTopic()
        {
            var testCode = @"
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

            var expected = VerifyAnalyzer.Diagnostic("DAPR2001", DiagnosticSeverity.Warning)
                .WithSpan(11, 25, 12, 66).WithMessage("Call app.MapSubscribeHandler to map endpoints for Dapr subscriptions");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }

        [Fact]
        public async Task ReportDiagnostic_TopicAttribute()
        {
            var testCode = @"
                using Dapr;
                using Microsoft.AspNetCore.Builder;

                public static class Program
                {
                    public static void Main()
                    {
                        var builder = WebApplication.CreateBuilder();
                        var app = builder.Build();

                        app.MapPost(""/subscribe"", [Topic(""pubsubName"", ""topicName"")] () => {});
                    }
                }
                ";

            var expected = VerifyAnalyzer.Diagnostic("DAPR2001", DiagnosticSeverity.Warning)
                .WithSpan(12, 25, 12, 95).WithMessage("Call app.MapSubscribeHandler to map endpoints for Dapr subscriptions");

            await VerifyAnalyzer.VerifyAnalyzerAsync(testCode, expected);
        }
    }
    
}
