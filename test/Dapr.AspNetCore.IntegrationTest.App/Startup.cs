// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest.App;

using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthentication().AddDapr(options => options.Token = "abcdefg");

        services.AddAuthorization(o => o.AddDapr());

        services.AddControllers().AddDapr();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseAuthentication();

        app.UseAuthorization();

        app.UseCloudEvents();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapSubscribeHandler();
            endpoints.MapControllers();

            endpoints.MapPost("/topic-a", context => Task.CompletedTask).WithTopic("testpubsub", "A").WithTopic("testpubsub", "A.1");

            endpoints.MapPost("/splitTopics", context => Task.CompletedTask).WithTopic("pubsub", "splitTopicBuilder");

            endpoints.MapPost("/splitMetadataTopics", context => Task.CompletedTask).WithTopic("pubsub", "splitMetadataTopicBuilder", new Dictionary<string, string> { { "n1", "v1" }, { "n2", "v1" } });

            endpoints.MapPost("/routingwithstateentry/{widget}", async context =>
            {
                var daprClient = context.RequestServices.GetRequiredService<DaprClient>();
                var state = await daprClient.GetStateEntryAsync<Widget>("testStore", (string)context.Request.RouteValues["widget"]);
                state.Value.Count++;
                await state.SaveAsync();
            });
        });
    }
}