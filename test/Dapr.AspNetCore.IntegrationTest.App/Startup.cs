// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest.App
{
    using System.Threading.Tasks;
    using Dapr.Client;
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
            services.AddGrpc();

            services.AddControllers().AddDapr();

            services.AddDaprGrpcService<DaprGrpcService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseCloudEvents();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapAppCallback();

                endpoints.MapSubscribeHandler();
                endpoints.MapControllers();

                endpoints.MapPost("/topic-a", context => Task.CompletedTask).WithTopic("testpubsub", "A");

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
}
