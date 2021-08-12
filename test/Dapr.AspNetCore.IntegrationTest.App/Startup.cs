// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest.App
{
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
