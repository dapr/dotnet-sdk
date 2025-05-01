// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Cryptography.Examples;
using Dapr.Crypto.Encryption.Extensions;

const string ComponentName = "localstorage";
const string KeyName = "rsa-private-key.pem"; //This should match the name of your generated key - this sample expects an RSA symmetrical key.

if (int.TryParse(args[0], out var exampleId))
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddDaprEncryptionClient((sp, opt) =>
    {
        opt.UseHttpEndpoint("http://localhost:6552");
        opt.UseGrpcEndpoint("http://localhost:6551");
    });
    builder.Services.AddTransient<EncryptDecryptStringExample>();
    builder.Services.AddTransient<EncryptDecryptFileStreamExample>();
    builder.Services.AddTransient<EncryptDecryptLargeFileExample>();
    var app = builder.Build();

    var ctx = new CancellationTokenSource();
    Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) => ctx.Cancel();

    switch (exampleId)
    {
        case 0:
        {
            var ex0 = app.Services.GetRequiredService<EncryptDecryptStringExample>();
            await ex0.RunAsync(ComponentName, KeyName, ctx.Token);
            return 0;
        }
        case 1:
        {
            var ex1 = app.Services.GetRequiredService<EncryptDecryptFileStreamExample>();
            await ex1.RunAsync(ComponentName, KeyName, ctx.Token);
            return 0;
        }
        case 2:
        {
            var ex2 = app.Services.GetRequiredService<EncryptDecryptLargeFileExample>();
            await ex2.RunAsync(ComponentName, KeyName, ctx.Token);
            return 0;
        }
    }
}

Console.WriteLine("Please choose a sample to run by passing your selection's number into the arguments, e.g. 'dotnet run 0':");
Console.WriteLine($"0: {EncryptDecryptStringExample.DisplayName}");
Console.WriteLine($"1: {EncryptDecryptFileStreamExample.DisplayName}");
Console.WriteLine($"2: {EncryptDecryptLargeFileExample.DisplayName}");
Console.WriteLine();
return 1;
