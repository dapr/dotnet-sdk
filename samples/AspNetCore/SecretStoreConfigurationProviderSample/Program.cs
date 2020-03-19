namespace SecretStoreConfigurationProviderSample
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;
    using Dapr.Client;
    using Dapr.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using System.Net.Sockets;
    using System;
    using System.Net;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Secret Store Configuration Provider Sample.
    /// </summary>
    public class Program
    {
        const string localhost = "127.0.0.1";

        static string daprPort => Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "50001";

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
            WaitForDapr();

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
                    // Create descriptors for the secrets you want to rerieve from the Dapr Secret Store.
                    var secretDescriptors = new DaprSecretDescriptor[]
                    {
                        new DaprSecretDescriptor(
                            "super-secret",
                            new Dictionary<string, string>(){ { "namespace", "default" } }
                        )
                    };

                    // Add the secret store Configuration Provider to the configuration builder.
                    configBuilder.AddDaprSecretStore("kubernetes", secretDescriptors, client);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }

        private static void WaitForDapr()
        {
            // Open the application port to trick Dapr into thinking it's ready 
            var ipadress = IPAddress.Parse(localhost);
            var server = new TcpListener(ipadress, 80);
            server.Start();
            using (var socket = server.AcceptSocket()) { }
            server.Stop();

            // Wait for Dapr Grpc port to be available
            var currentRetry = 0;
            for (; ; )
            {
                try
                {
                    using (var tcpClient = new TcpClient())
                    {
                        tcpClient.ConnectAsync(ipadress, int.Parse(daprPort)).Wait(10000);
                    }

                    break;
                }
                catch
                {
                    currentRetry++;

                    if (currentRetry > 3)
                    {
                        throw;
                    }
                }

                Task.Delay(1000).Wait();
            }
        }
    }
}