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

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Cryptography.Encryption;
using Dapr.Cryptography.Encryption.Extensions;
using Dapr.Cryptography.Encryption.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Dapr.E2E.Test;

public partial class E2ETests
{
    private const string CryptoComponentName = "localstorage";
    private const string KeyName = "rsa-private-key.pem";
    
    [Fact]
    public async Task Encrypt_SmallString()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDaprEncryptionClient();
        var app = builder.Build();
        using var daprClient = app.Services.GetRequiredService<DaprEncryptionClient>();
    
        const string plaintextString = "This is the value we will be encrypting today";
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextString);
        var encryptionResponse = await daprClient.EncryptAsync(CryptoComponentName, plaintextBytes, KeyName,
            new EncryptionOptions(KeyWrapAlgorithm.Rsa), cts.Token);
        Assert.NotEqual(plaintextBytes, encryptionResponse); //The plaintext and encrypted bytes should not equal one another
        
        cts.CancelAfter(TimeSpan.FromSeconds(20)); //Start with a 20 second timeout each operation
    
        var decryptionResponse = await daprClient.DecryptAsync(CryptoComponentName, encryptionResponse, KeyName,
            cancellationToken: cts.Token);
        var decryptionText = Encoding.UTF8.GetString(decryptionResponse.Span.ToArray());
        Assert.Equal(plaintextString, decryptionText);
    }
    
    [Fact]
    public async Task Encrypt_MediumString()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDaprEncryptionClient();
        var app = builder.Build();
        using var daprClient = app.Services.GetRequiredService<DaprEncryptionClient>();
    
        const string plaintextString = """
                                       # The Road Not Taken
                                       ## By Robert Lee Frost
                                       
                                       Two roads diverged in a yellow wood,
                                       And sorry I could not travel both
                                       And be one traveler, long I stood
                                       And looked down one as far as I could
                                       To where it bent in the undergrowth;
                                       
                                       Then took the other, as just as fair
                                       And having perhaps the better claim,
                                       Because it was grassy and wanted wear;
                                       Though as for that, the passing there
                                       Had worn them really about the same,
                                       
                                       And both that morning equally lay
                                       In leaves no step had trodden black
                                       Oh, I kept the first for another day!
                                       Yet knowing how way leads on to way,
                                       I doubted if I should ever come back.
                                       
                                       I shall be telling this with a sigh
                                       Somewhere ages and ages hence:
                                       Two roads diverged in a wood, and I,
                                       I took the one less traveled by,
                                       And that has made all the difference.
                                       """;
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextString);
        var encryptionResponse = await daprClient.EncryptAsync(CryptoComponentName, plaintextBytes, KeyName,
            new EncryptionOptions(KeyWrapAlgorithm.Rsa), cts.Token);
        Assert.NotEqual(plaintextBytes, encryptionResponse); //The plaintext and encrypted bytes should not equal one another
        
        cts.CancelAfter(TimeSpan.FromSeconds(30)); //Start with a 30 second timeout each operation
    
        var decryptionResponse = await daprClient.DecryptAsync(CryptoComponentName, encryptionResponse, KeyName,
            cancellationToken: cts.Token);
        var decryptionText = Encoding.UTF8.GetString(decryptionResponse.Span.ToArray());
        Assert.Equal(plaintextString, decryptionText);
    }
}
