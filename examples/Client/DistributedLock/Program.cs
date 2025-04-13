using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace DistributedLock;

public class Program
{
    static string DEFAULT_APP_PORT = "22222";
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>()
                    .UseUrls($"http://localhost:{Environment.GetEnvironmentVariable("APP_PORT") ?? DEFAULT_APP_PORT}");
            });
}