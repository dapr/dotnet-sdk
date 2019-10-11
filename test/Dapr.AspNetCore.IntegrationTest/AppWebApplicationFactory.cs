// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest
{
    using Dapr.AspNetCore.IntegrationTest.App;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public class AppWebApplicationFactory : WebApplicationFactory<Startup>
    {
        public StateTestClient StateClient { get; } = new StateTestClient();

        protected override IHostBuilder CreateHostBuilder()
        {
            var builder = base.CreateHostBuilder();
            return builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<StateClient>(this.StateClient);
            });
        }
    }
}