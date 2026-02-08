using System;
using System.Threading;
using Dapr.Client;
using DistributedLock.Model;

namespace DistributedLock.Services;

public class GeneratorService
{
    Timer generateDataTimer;

    [Obsolete]
    public GeneratorService()
    {
        // Generate some data every second.
        if (Environment.GetEnvironmentVariable("APP_ID") == "generator")
        {
            generateDataTimer = new Timer(GenerateData, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        }
    }

    [Obsolete]
    public async void GenerateData(Object stateInfo)
    {
        using (var client = new DaprClientBuilder().Build())
        {
            var rand = new Random();
            var state = new StateData(rand.Next(100));

            // NOTE: It is no longer best practice to use this method - this example will be modified in the 1.18 release
            await client.InvokeBindingAsync("localstorage", "create", state);
        }
    }
}
