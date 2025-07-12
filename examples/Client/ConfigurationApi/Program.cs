using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Dapr.Client;
using Dapr.Extensions.Configuration;
using System.Collections.Generic;

namespace ConfigurationApi;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Starting application.");
        CreateHostBuilder(args).Build().Run();
        Console.WriteLine("Closing application.");
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
                config.AddDaprConfigurationStore("redisconfig", new List<string>() { "withdrawVersion" }, client, TimeSpan.FromSeconds(20));
                config.AddStreamingDaprConfigurationStore("redisconfig", new List<string>() { "withdrawVersion", "source" }, client, TimeSpan.FromSeconds(20));
                    
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    }
}