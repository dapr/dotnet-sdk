// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Dapr.AspNetCore.IntegrationTest.App
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddDapr();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapSubscribeHandler();
                endpoints.MapControllers();

                endpoints.MapPost("/topic-a", context => Task.CompletedTask).WithTopic("A");

                endpoints.MapPost("/routingwithstateentry/{widget}", async context =>
                {
                    var stateClient = context.RequestServices.GetRequiredService<StateClient>();
                    var state = await stateClient.GetStateEntryAsync<Widget>((string)context.Request.RouteValues["widget"]);
                    state.Value.Count++;
                    await state.SaveAsync();
                });
            });
        }
    }
}
