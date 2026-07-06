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

﻿namespace SecretStoreConfigurationProviderSample;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Dapr.Client;
using Dapr.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

/// <summary>
/// Secret Store Configuration Provider Sample.
/// </summary>
public class Program
{
    /// <summary>
    /// Main for Secret Store Configuration Provider Sample.
    /// </summary>
    /// <param name="args">Arguments.</param>
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    /// <summary>
    /// Creates WebHost Builder.
    /// </summary>
    /// <param name="args">Arguments.</param>
    /// <returns>Returns IHostbuilder.</returns>
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        // Create Dapr Client
        var client = new DaprClientBuilder()
            .Build();

        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((services) =>
            {
                // Add the DaprClient to DI.
                services.AddSingleton<DaprClient>(client);
            })
            .ConfigureAppConfiguration((configBuilder) =>
            {
                // To retrive specific secrets use secretDescriptors
                // Create descriptors for the secrets you want to rerieve from the Dapr Secret Store.
                // var secretDescriptors = new DaprSecretDescriptor[]
                // {
                //     new DaprSecretDescriptor("super-secret")
                // };
                // configBuilder.AddDaprSecretStore("demosecrets", secretDescriptors, client);

                // Add the secret store Configuration Provider to the configuration builder.
                // Including a TimeSpan allows us to dictate how long we should wait for the Sidecar to start.
                configBuilder.AddDaprSecretStore("demosecrets", client, TimeSpan.FromSeconds(10));
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    }
}