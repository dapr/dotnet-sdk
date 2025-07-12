using System;
using System.Threading;
using Dapr.Client;
using DistributedLock.Model;

namespace DistributedLock.Services;

public class GeneratorService
{
    Timer generateDataTimer;

    public GeneratorService()
    {
        // Generate some data every second.
        if (Environment.GetEnvironmentVariable("APP_ID") == "generator")
        {
            generateDataTimer = new Timer(GenerateData, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        }
    }

    public async void GenerateData(Object stateInfo)
    {
        using (var client = new DaprClientBuilder().Build())
        {
            var rand = new Random();
            var state = new StateData(rand.Next(100));

            await client.InvokeBindingAsync("localstorage", "create", state);
        }
    }
}