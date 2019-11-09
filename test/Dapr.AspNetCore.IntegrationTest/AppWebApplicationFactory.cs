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
    using Microsoft.Extensions.Logging;

    public class AppWebApplicationFactory : WebApplicationFactory<Startup>
    {
        public StateTestClient StateClient { get; } = new StateTestClient();

        protected override IHostBuilder CreateHostBuilder()
        {
            var builder = base.CreateHostBuilder();
            builder.ConfigureLogging(b =>
            {
                b.SetMinimumLevel(LogLevel.None);
            });
            return builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton<StateClient>(this.StateClient);
            });
        }
    }
}