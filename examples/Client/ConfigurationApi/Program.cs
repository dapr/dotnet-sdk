using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Client;

namespace ConfigurationApi
{
    public class Program
    {
        private static readonly string ConfigStore = "redisconfig";
        private static readonly string QueryKey = "greeting";

        [Obsolete]
        public static async Task Main(string[] args)
        {
            using var client = new DaprClientBuilder().Build();

            Console.WriteLine($"Querying Configuration with key: {QueryKey}");
            var configItems = await client.GetConfiguration(ConfigStore, new List<string>() { QueryKey });

            if (configItems.Items.Count == 0)
            {
                Console.WriteLine($"Could not find {QueryKey} in the configuration store.");
                return;
            }

            Console.WriteLine($"Got configuration item:\nKey: {configItems.Items[0].Key}\nValue: {configItems.Items[0].Value}\nVersion: {configItems.Items[0].Version}");
        }
    }
}
