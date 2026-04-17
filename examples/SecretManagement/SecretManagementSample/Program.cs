// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
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

using Dapr.SecretsManagement;
using Dapr.SecretsManagement.Extensions;
using SecretManagementSample;

var builder = WebApplication.CreateBuilder(args);

// Register the Dapr Secrets Management client and the source-generated typed secret store.
// AddMyVaultSecrets() is a generated extension method — see IMyVaultSecrets.cs.
builder.Services.AddDaprSecretsManagementClient()
    .AddMyVaultSecrets();

var app = builder.Build();

// --- Example 1: Direct secret retrieval ---
app.MapGet("/secrets/{storeName}/{key}", async (
    string storeName,
    string key,
    DaprSecretsManagementClient secretsClient,
    CancellationToken cancellationToken) =>
{
    var secret = await secretsClient.GetSecretAsync(storeName, key, cancellationToken: cancellationToken);
    return Results.Ok(secret);
});

// --- Example 2: Bulk secret retrieval ---
app.MapGet("/secrets/{storeName}", async (
    string storeName,
    DaprSecretsManagementClient secretsClient,
    CancellationToken cancellationToken) =>
{
    var secrets = await secretsClient.GetBulkSecretAsync(storeName, cancellationToken: cancellationToken);
    return Results.Ok(secrets);
});

// --- Example 3: Using the source-generated typed secret store ---
app.MapGet("/typed-secrets", (SecretManagementSample.IMyVaultSecrets secrets) =>
{
    return Results.Ok(new
    {
        DatabaseConnection = secrets.DatabaseConnection,
        ApiKey = secrets.ApiKey
    });
});

app.Run();
