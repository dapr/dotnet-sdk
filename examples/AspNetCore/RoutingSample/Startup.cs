// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace RoutingSample
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Client;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

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
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, JsonSerializerOptions serializerOptions)
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

                endpoints.MapGet("{id}", Balance);
                endpoints.MapPost("deposit", Deposit).WithTopic(PubsubName, "deposit");
                endpoints.MapPost("withdraw", Withdraw).WithTopic(PubsubName, "withdraw");
            });

            async Task Balance(HttpContext context)
            {
                Console.WriteLine("Enter Balance");
                var client = context.RequestServices.GetRequiredService<DaprClient>();

                var id = (string)context.Request.RouteValues["id"];
                Console.WriteLine("id is {0}", id);
                var account = await client.GetStateAsync<Account>(StoreName, id);
                if (account == null)
                {
                    Console.WriteLine("Account not found");
                    context.Response.StatusCode = 404;
                    return;
                }

                Console.WriteLine("Account balance is {0}", account.Balance);

                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
            }

            async Task Deposit(HttpContext context)
            {
                Console.WriteLine("Enter Deposit");
                
                var client = context.RequestServices.GetRequiredService<DaprClient>();

                var transaction = await JsonSerializer.DeserializeAsync<Transaction>(context.Request.Body, serializerOptions);
                Console.WriteLine("Id is {0}, Amount is {1}", transaction.Id, transaction.Amount);
                var account = await client.GetStateAsync<Account>(StoreName, transaction.Id);
                if (account == null)
                {
                    account = new Account() { Id = transaction.Id, };
                }

                if (transaction.Amount < 0m)
                {
                    Console.WriteLine("Invalid amount");
                    context.Response.StatusCode = 400;
                    return;
                }

                account.Balance += transaction.Amount;
                await client.SaveStateAsync(StoreName, transaction.Id, account);
                Console.WriteLine("Balance is {0}", account.Balance);

                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
            }

            async Task Withdraw(HttpContext context)
            {
                Console.WriteLine("Enter Withdraw");
                var client = context.RequestServices.GetRequiredService<DaprClient>();
                var transaction = await JsonSerializer.DeserializeAsync<Transaction>(context.Request.Body, serializerOptions);
                Console.WriteLine("Id is {0}", transaction.Id);
                var account = await client.GetStateAsync<Account>(StoreName, transaction.Id);
                if (account == null)
                {
                    Console.WriteLine("Account not found");
                    context.Response.StatusCode = 404;
                    return;
                }

                if (transaction.Amount < 0m)
                {
                    Console.WriteLine("Invalid amount");
                    context.Response.StatusCode = 400;
                    return;
                }

                account.Balance -= transaction.Amount;
                await client.SaveStateAsync(StoreName, transaction.Id, account);
                Console.WriteLine("Balance is {0}", account.Balance);

                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
            }
        }
    }
}
