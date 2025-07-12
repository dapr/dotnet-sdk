using System.Collections.Generic;
using System.Threading.Tasks;
using ControllerSample;
using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConfigurationApi.Controllers;

[ApiController]
[Route("configuration")]
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
            logger.LogInformation($"Got configuration item:\nKey: {item.Key}\nValue: {item.Value.Value}\nVersion: {item.Value.Version}");
        }
    }

    [HttpGet("extension")]
    public Task SubscribeAndWatchConfiguration()
    {
        logger.LogInformation($"Getting values from Configuration Extension, watched values ['withdrawVersion', 'source'].");

        logger.LogInformation($"'withdrawVersion' from extension: {configuration["withdrawVersion"]}");
        logger.LogInformation($"'source' from extension: {configuration["source"]}");

        return Task.CompletedTask;
    }

#nullable enable
    [HttpPost("withdraw")]
    public async Task<ActionResult<Account>> CreateAccountHandler(Transaction transaction)
    {
        // Check if the V2 method is enabled.
        if (configuration["withdrawVersion"] == "v2")
        {
            var source = !string.IsNullOrEmpty(configuration["source"]) ? configuration["source"] : "local";
            var transactionV2 = new TransactionV2
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Channel = source
            };
            logger.LogInformation($"Calling V2 Withdraw API - Id: {transactionV2.Id} Amount: {transactionV2.Amount} Channel: {transactionV2.Channel}");
            try
            {
                return await this.client.InvokeMethodAsync<TransactionV2, Account>("controller", "withdraw.v2", transactionV2);
            }
            catch (DaprException ex)
            {
                logger.LogError($"Error executing withdrawal: {ex.Message}");
                return BadRequest();
            }
        }

        // Default to the original method.
        logger.LogInformation($"Calling V1 Withdraw API: {transaction}");
        return await this.client.InvokeMethodAsync<Transaction, Account>("controller", "withdraw", transaction);
    }
#nullable disable
}