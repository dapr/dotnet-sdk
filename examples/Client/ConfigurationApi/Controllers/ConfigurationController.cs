using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConfigurationApi.Controllers
{
    [ApiController]
    [Route("configuration")]
    [Obsolete]
    public class ConfigurationController : ControllerBase
    {
        private ILogger<ConfigurationController> logger;
        private IConfiguration configuration;
        private DaprClient client;

        public ConfigurationController(ILogger<ConfigurationController> logger, IConfiguration configuration, [FromServices] DaprClient client)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.client = client;
        }

        [HttpGet("get/{configStore}/{queryKey}")]
        public async Task GetConfiguration([FromRoute] string configStore, [FromRoute] string queryKey)
        {
            logger.LogInformation($"Querying Configuration with key: {queryKey}");
            var configItems = await client.GetConfiguration(configStore, new List<string>() { queryKey });

            if (configItems.Items.Count == 0)
            {
                logger.LogInformation($"No configuration item found for key: {queryKey}");
            }

            foreach (var item in configItems.Items)
            {
                logger.LogInformation($"Got configuration item:\nKey: {item.Key}\nValue: {item.Value}\nVersion: {item.Version}");
            }
        }

        [HttpGet("subscribe/{configStore}/{queryKey}")]
        public async Task SubscribeConfiguration([FromRoute] string configStore, [FromRoute] string queryKey)
        {
            logger.LogInformation($"Subscribing to {configStore} watching {queryKey}.");
            var source = await client.SubscribeConfiguration(configStore, new List<string>() { queryKey });

            logger.LogInformation("Watching configuration for 1 minute.");
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await Task.Run(async () =>
            {
                try
                {
                    await foreach (var items in source.Source.WithCancellation(cts.Token))
                    {
                        foreach (var item in items)
                        {
                            logger.LogInformation($"Got item: {item.Key} -> {item.Value} - {item.Version}");
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    logger.LogInformation("Stopping listening to the subscription stream.");
                }
            });

            if (!string.IsNullOrEmpty(source.Id))
            {
                logger.LogInformation("Cancelling subscription.");
                await client.UnsubscribeConfiguration(configStore, source.Id);
            }
        }

        [HttpGet("extension")]
        public Task SubscribeAndWatchConfiguration()
        {
            logger.LogInformation($"Getting values from Configuration Extension, watched values ['greeting', 'response'].");

            logger.LogInformation($"Greeting from extension: {configuration["greeting"]}");
            logger.LogInformation($"Response from extension: {configuration["response"]}");

            return Task.CompletedTask;
        }
    }
}
