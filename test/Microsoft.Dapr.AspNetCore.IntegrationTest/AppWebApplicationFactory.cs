// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.AspNetCore.IntegrationTest
{
    using Microsoft.Dapr.AspNetCore.IntegrationTest.App;
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
                services.AddSingleton<StateClient>(StateClient);
            });
        }
    }
}