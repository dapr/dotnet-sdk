namespace SecretStoreConfigurationProviderSample
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;
    using Dapr.Client;
    using Dapr.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using System.Collections.Generic;

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
                    configBuilder.AddDaprSecretStore("demosecrets", client);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }
    }
}