// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.DistributedLock;
using Dapr.DistributedLock.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(services =>
{
    services.AddDaprDistributedLock();
});

var app = builder.Build();

using var scope = app.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger>();
var distributedLockClient = scope.ServiceProvider.GetRequiredService<DaprDistributedLockClient>();

// Locks are disposable and will automatically unlock at the end of a using scope
const string resourceName = "myFile.txt";
const string appId = "myApp";

logger.LogInformation("Attempting to lock: {fileName}", resourceName);
await using var fileLock = await distributedLockClient.TryLockAsync("redislock", resourceName, appId, 60);
if (fileLock is not null)
{
    // Lock was acquired successfully
    logger.LogInformation("Successfully locked file: {fileName}", resourceName);
    
    // Simulate processing the file
    await Task.Delay(TimeSpan.FromSeconds(3));
    
    logger.LogInformation("Done processing: {fileName}", resourceName);
}
else
{
    logger.LogInformation("Failed to lock: {fileName}", resourceName);
}
