using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr;
using Dapr.Client;
using DistributedLock.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DistributedLock.Controllers;

[ApiController]
public class BindingController : ControllerBase
{
    private DaprClient client;
    private ILogger<BindingController> logger;
    private string appId;

    public BindingController(DaprClient client, ILogger<BindingController> logger)
    {
        this.client = client;
        this.logger = logger;
        this.appId = Environment.GetEnvironmentVariable("APP_ID");
    }

    [HttpPost("cronbinding")]
    [Obsolete]
    public async Task<IActionResult> HandleBindingEvent()
    {
        logger.LogInformation($"Received binding event on {appId}, scanning for work.");

        var request = new BindingRequest("localstorage", "list");
        var result = client.InvokeBindingAsync(request);

        var rawData = result.Result.Data.ToArray();
        var files = JsonSerializer.Deserialize<string[]>(rawData);

        if (files != null)
        {
            foreach (var file in files.Select(file => file.Split("/").Last()).OrderBy(file => file))
            {
                await AttemptToProcessFile(file);
            }
        }
            
        return Ok();
    }


    [Obsolete]
    private async Task AttemptToProcessFile(string fileName)
    {
        // Locks are Disposable and will automatically unlock at the end of a 'using' statement.
        logger.LogInformation($"Attempting to lock: {fileName}");
        await using (var fileLock = await client.Lock("redislock", fileName, appId, 60))
        {
            if (fileLock.Success)
            {
                logger.LogInformation($"Successfully locked file: {fileName}");

                // Get the file after we've locked it, we're safe here because of the lock.
                var fileState = await GetFile(fileName);

                if (fileState == null)
                {
                    logger.LogWarning($"File {fileName} has already been processed!");
                    return;
                }

                // "Analyze" the file before committing it to our remote storage.
                fileState.Analysis = fileState.Number > 50 ? "High" : "Low";

                // Save it to remote storage.
                await client.SaveStateAsync("redisstore", fileName, fileState);

                // Remove it from local storage.
                var bindingDeleteRequest = new BindingRequest("localstorage", "delete");
                bindingDeleteRequest.Metadata["fileName"] = fileName;
                await client.InvokeBindingAsync(bindingDeleteRequest);

                logger.LogInformation($"Done processing {fileName}");
            }
            else
            {
                logger.LogWarning($"Failed to lock {fileName}.");
            }
        }
    }

    private async Task<StateData> GetFile(string fileName)
    {
        try
        {
            var bindingGetRequest = new BindingRequest("localstorage", "get");
            bindingGetRequest.Metadata["fileName"] = fileName;

            var bindingResponse = await client.InvokeBindingAsync(bindingGetRequest);
            return JsonSerializer.Deserialize<StateData>(bindingResponse.Data.ToArray());
        }
        catch (DaprException)
        {
            return null;
        }            
    }
}