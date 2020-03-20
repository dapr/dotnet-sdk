// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest
{
    using Dapr.AspNetCore.IntegrationTest.App;
    using Dapr.Client;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class AppWebApplicationFactory : WebApplicationFactory<Startup>
    {
        internal StateTestClient DaprClient { get; } = new StateTestClient();

        protected override IHostBuilder CreateHostBuilder()
        {
            var builder = base.CreateHostBuilder();
            builder.ConfigureLogging(b =>
            {
                b.SetMinimumLevel(LogLevel.None);
            });
            return builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<DaprClient>(this.DaprClient);
            });
        }
    }
}
