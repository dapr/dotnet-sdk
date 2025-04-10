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

using Dapr.Analyzers.Common;

namespace Dapr.Pubsub.Analyzers.Test;

public class MapSubscribeHandlerCodeFixProviderTests
{
    [Fact]
    public async Task MapSubscribeHandler()
    {
        const string code = """
                                        using Microsoft.AspNetCore.Builder;
                            
                                        public static class Program
                                        {
                                            public static void Main()
                                            {
                                                var builder = WebApplication.CreateBuilder();
                                                var app = builder.Build();
                            
                                                app.MapPost("/subscribe", () => {})
                                                    .WithTopic("pubSubName", "topicName");
                                            }
                                        }         
                            """;

        const string expectedChangedCode = """
                                                       using Microsoft.AspNetCore.Builder;
                                           
                                                       public static class Program
                                                       {
                                                           public static void Main()
                                                           {
                                                               var builder = WebApplication.CreateBuilder();
                                                               var app = builder.Build();
                                           
                                                               app.MapSubscribeHandler();
                                           
                                                               app.MapPost("/subscribe", () => {})
                                                                   .WithTopic("pubSubName", "topicName");
                                                           }
                                                       }         
                                           """;

        await VerifyCodeFix.RunTest<MapSubscribeHandlerCodeFixProvider>(code, expectedChangedCode, typeof(object).Assembly.Location, Utilities.GetReferences(), Utilities.GetAnalyzers());
    }

    [Fact]
    public async Task MapSubscribeHandler_TopLevelStatements()
    {
        const string code = """       
                                        using Microsoft.AspNetCore.Builder;
                            
                                        var builder = WebApplication.CreateBuilder();
                                        var app = builder.Build();
                            
                                        app.MapPost("/subscribe", () => {})
                                            .WithTopic("pubSubName", "topicName");         
                            """;

        const string expectedChangedCode = """                      
                                                       using Microsoft.AspNetCore.Builder;
                                           
                                                       var builder = WebApplication.CreateBuilder();
                                                       var app = builder.Build();
                                           
                                                       app.MapSubscribeHandler();
                                           
                                                       app.MapPost("/subscribe", () => {})
                                                           .WithTopic("pubSubName", "topicName");         
                                           """;

        await VerifyCodeFix.RunTest<MapSubscribeHandlerCodeFixProvider>(code, expectedChangedCode, typeof(object).Assembly.Location, Utilities.GetReferences(), Utilities.GetAnalyzers());
    }
}
