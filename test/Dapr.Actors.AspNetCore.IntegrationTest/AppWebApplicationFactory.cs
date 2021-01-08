// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.AspNetCore.IntegrationTest
{
    using Dapr.Actors.AspNetCore.IntegrationTest.App;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    // The customizations here suppress logging from showing up in the console when
    // running at the command line.
    public class AppWebApplicationFactory : WebApplicationFactory<Startup>
    {
        protected override IHostBuilder CreateHostBuilder()
        {
            var builder = base.CreateHostBuilder();
            if (builder == null)
            {
                return null;
            }

            builder.ConfigureLogging(b =>
            {
                b.SetMinimumLevel(LogLevel.None);
            });
            return builder;
        }
    }
}
