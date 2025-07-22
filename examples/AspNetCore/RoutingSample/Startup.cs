// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace RoutingSample;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr;
using Dapr.AspNetCore;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Startup class.
/// </summary>
public class Startup
{
    /// <summary>
    /// State store name.
    /// </summary>
    public const string StoreName = "statestore";

    /// <summary>
    /// Pubsub component name.  "pubsub" is name of the default pub/sub configured by the Dapr CLI.
    /// </summary>
    public const string PubsubName = "pubsub";

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    public Startup(IConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// Configures Services.
    /// </summary>
    /// <param name="services">Service Collection.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDaprClient();

        services.AddSingleton(new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        });
    }

    /// <summary>
    /// Configures Application Builder and WebHost environment.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <param name="env">Webhost environment.</param>
    /// <param name="serializerOptions">Options for JSON serialization.</param>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, JsonSerializerOptions serializerOptions,
        ILogger<Startup> logger)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseCloudEvents();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapSubscribeHandler();

            var depositTopicOptions = new TopicOptions();
            depositTopicOptions.PubsubName = PubsubName;
            depositTopicOptions.Name = "deposit";
            depositTopicOptions.DeadLetterTopic = "amountDeadLetterTopic";

            var withdrawTopicOptions = new TopicOptions();
            withdrawTopicOptions.PubsubName = PubsubName;
            withdrawTopicOptions.Name = "withdraw";
            withdrawTopicOptions.DeadLetterTopic = "amountDeadLetterTopic";
                
            var multiDepositTopicOptions = new TopicOptions { PubsubName = PubsubName, Name = "multideposit" };

            var bulkSubscribeTopicOptions = new BulkSubscribeTopicOptions
            {
                TopicName = "multideposit", MaxMessagesCount = 250, MaxAwaitDurationMs = 1000
            };

            endpoints.MapGet("{id}", Balance);
            endpoints.MapPost("deposit", Deposit).WithTopic(depositTopicOptions);
            endpoints.MapPost("multideposit", MultiDeposit).WithTopic(multiDepositTopicOptions).WithBulkSubscribe(bulkSubscribeTopicOptions);
            endpoints.MapPost("deadLetterTopicRoute", ViewErrorMessage).WithTopic(PubsubName, "amountDeadLetterTopic");
            endpoints.MapPost("withdraw", Withdraw).WithTopic(withdrawTopicOptions);
        });

        async Task Balance(HttpContext context)
        {
            logger.LogInformation("Enter Balance");
            var client = context.RequestServices.GetRequiredService<DaprClient>();

            var id = (string)context.Request.RouteValues["id"];
            logger.LogInformation("id is {0}", id);
            var account = await client.GetStateAsync<Account>(StoreName, id);
            if (account == null)
            {
                logger.LogInformation("Account not found");
                context.Response.StatusCode = 404;
                return;
            }

            logger.LogInformation("Account balance is {0}", account.Balance);

            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
        }

        async Task Deposit(HttpContext context)
        {
            logger.LogInformation("Enter Deposit");

            var client = context.RequestServices.GetRequiredService<DaprClient>();
            var transaction = await JsonSerializer.DeserializeAsync<Transaction>(context.Request.Body, serializerOptions);

            logger.LogInformation("Id is {0}, Amount is {1}", transaction.Id, transaction.Amount);

            var account = await client.GetStateAsync<Account>(StoreName, transaction.Id);
            if (account == null)
            {
                account = new Account() { Id = transaction.Id, };
            }

            if (transaction.Amount < 0m)
            {
                logger.LogInformation("Invalid amount");
                context.Response.StatusCode = 400;
                return;
            }

            account.Balance += transaction.Amount;
            await client.SaveStateAsync(StoreName, transaction.Id, account);
            logger.LogInformation("Balance is {0}", account.Balance);

            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
        }
            
        async Task MultiDeposit(HttpContext context)
        {
            logger.LogInformation("Enter bulk deposit");

            var client = context.RequestServices.GetRequiredService<DaprClient>();
                
            var bulkMessage = await JsonSerializer.DeserializeAsync<BulkSubscribeMessage<BulkMessageModel<Transaction>>>(
                context.Request.Body, serializerOptions);
                
            List<BulkSubscribeAppResponseEntry> entries = new List<BulkSubscribeAppResponseEntry>();

            if (bulkMessage != null)
            {
                foreach (var entry in bulkMessage.Entries)
                {
                    try
                    {
                        var transaction = entry.Event.Data;

                        var state = await client.GetStateEntryAsync<Account>(StoreName, transaction.Id);
                        state.Value ??= new Account() { Id = transaction.Id, };
                        logger.LogInformation("Id is {0}, the amount to be deposited is {1}",
                            transaction.Id, transaction.Amount);

                        if (transaction.Amount < 0m)
                        {
                            logger.LogInformation("Invalid amount");
                            context.Response.StatusCode = 400;
                            return;
                        }

                        state.Value.Balance += transaction.Amount;
                        logger.LogInformation("Balance is {0}", state.Value.Balance);
                        await state.SaveAsync();
                        entries.Add(new BulkSubscribeAppResponseEntry(entry.EntryId,
                            BulkSubscribeAppResponseStatus.SUCCESS));
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e.Message);
                        entries.Add(new BulkSubscribeAppResponseEntry(entry.EntryId,
                            BulkSubscribeAppResponseStatus.RETRY));
                    }
                }
            }

            await JsonSerializer.SerializeAsync(context.Response.Body, 
                new BulkSubscribeAppResponse(entries), serializerOptions);
        }

        async Task ViewErrorMessage(HttpContext context)
        {
            var transaction = await JsonSerializer.DeserializeAsync<Transaction>(context.Request.Body, serializerOptions);

            logger.LogInformation("The amount cannot be negative: {0}", transaction.Amount);

            return;
        }

        async Task Withdraw(HttpContext context)
        {
            logger.LogInformation("Enter Withdraw");

            var client = context.RequestServices.GetRequiredService<DaprClient>();
            var transaction = await JsonSerializer.DeserializeAsync<Transaction>(context.Request.Body, serializerOptions);

            logger.LogInformation("Id is {0}, Amount is {1}", transaction.Id, transaction.Amount);

            var account = await client.GetStateAsync<Account>(StoreName, transaction.Id);
            if (account == null)
            {
                logger.LogInformation("Account not found");
                context.Response.StatusCode = 404;
                return;
            }

            if (transaction.Amount < 0m)
            {
                logger.LogInformation("Invalid amount");
                context.Response.StatusCode = 400;
                return;
            }

            account.Balance -= transaction.Amount;
            await client.SaveStateAsync(StoreName, transaction.Id, account);
            logger.LogInformation("Balance is {0}", account.Balance);

            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
        }
    }
}