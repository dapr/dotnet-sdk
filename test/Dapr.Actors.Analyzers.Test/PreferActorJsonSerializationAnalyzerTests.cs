// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.Actors.Analyzers.Test;
using Dapr.Analyzers.Common;

namespace Dapr.Actors.Analyzers.Tests;

public class PreferActorJsonSerializationAnalyzerTests
{
    [Fact]
    public async Task ReportDiagnostic()
    {
        const string testCode = """
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
                                """;

        var expected = VerifyAnalyzer
            .Diagnostic(PreferActorJsonSerializationAnalyzer.DiagnosticDescriptorJsonSerialization)
            .WithSpan(10, 25, 12, 27)
            .WithMessage("Set options.UseJsonSerialization to true to support interoperability with non-.NET actors");

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<PreferActorJsonSerializationAnalyzer>(testCode, expected);
    }

    [Fact]
    public async Task ReportNoDiagnostic()
    {
        const string testCode = """
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
                                """;

        var analyzer = new VerifyAnalyzer(Utilities.GetReferences());
        await analyzer.VerifyAnalyzerAsync<PreferActorJsonSerializationAnalyzer>(testCode);
    }
}
