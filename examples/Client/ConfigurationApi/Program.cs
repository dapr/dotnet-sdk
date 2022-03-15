using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Dapr.Client;
using Dapr.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading;

namespace ConfigurationApi
{
    public class Program
    {
        private static CancellationTokenSource cts;

        [Obsolete]
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting application.");
            // This cancellation token is used to stop the Streaming configuration.
            using (cts = new CancellationTokenSource())
            {
                CreateHostBuilder(args).Build().Run();
                Console.WriteLine("Closing application.");
                cts.Cancel();
            }                
        }

        /// <summary>
        /// Creates WebHost Builder.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Returns IHostbuilder.</returns>
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var client = new DaprClientBuilder().Build();
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(config =>
                {
                    // Get the initial value and continue to watch it for changes.
                    config.AddDaprConfigurationStore("redisconfig", new List<string>() { "greeting", "response" }, client, TimeSpan.FromSeconds(20));
                    config.AddStreamingDaprConfigurationStore("redisconfig", new List<string>() { "greeting", "response" }, client,
                        TimeSpan.FromSeconds(20), cancellationToken: cts.Token);
                    
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }
    }
}
