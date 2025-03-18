// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Security.Cryptography;
using CryptographySample;
using Dapr.Cryptography.Encryption.Extensions;

#pragma warning disable CS0618 // Type or member is obsolete

var fileName = "";
var checksum = "";
switch (args[0])
{
    case "1":
        (fileName, checksum) = await WriteSmallFileAsync();
        break;
    case "2":
        (fileName, checksum) = await WriteMediumFileAsync();
        break;
    default:
        Console.WriteLine("Please select from one of the following by passing in a number in your program arguments:");
        Console.WriteLine("1) Download and perform encryption/decryption on small file (~14 KB)");
        Console.WriteLine("2) Download and perform encryption/decryption on a medium file (~50 MB)");
        return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<EncryptionOperation>();
builder.Services.AddDaprEncryptionClient();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

await app.RunAsync();

return;


