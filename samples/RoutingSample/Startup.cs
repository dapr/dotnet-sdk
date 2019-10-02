using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Dapr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RoutingSample
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDaprClient();

            services.AddSingleton(new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, JsonSerializerOptions serializerOptions)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapSubscribeHandler();

                endpoints.MapGet("{id}", Balance);
                endpoints.MapPost("deposit", Deposit).WithTopic("deposit");
                endpoints.MapPost("withdraw", Withdraw).WithTopic("withdraw");
            });

            async Task Balance(HttpContext context)
            {
                var client = context.RequestServices.GetRequiredService<StateClient>();

                var id = (string)context.Request.RouteValues["id"];
                var account = await client.GetStateAsync<Account>(id);
                if (account == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
            }

            async Task Deposit(HttpContext context)
            {
                var client = context.RequestServices.GetRequiredService<StateClient>();

                var transaction = await JsonSerializer.DeserializeAsync<Transaction>(context.Request.Body, serializerOptions);
                var account = await client.GetStateAsync<Account>(transaction.Id);
                if (account == null)
                {
                    account = new Account() { Id = transaction.Id, };
                }

                if (transaction.Amount < 0m)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                account.Balance += transaction.Amount;
                await client.SaveStateAsync(transaction.Id, account);

                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
            }

            async Task Withdraw(HttpContext context)
            {
                var client = context.RequestServices.GetRequiredService<StateClient>();

                var transaction = await JsonSerializer.DeserializeAsync<Transaction>(context.Request.Body, serializerOptions);
                var account = await client.GetStateAsync<Account>(transaction.Id);
                if (account == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                if (transaction.Amount < 0m)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                account.Balance -= transaction.Amount;
                await client.SaveStateAsync(transaction.Id, account);

                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, account, serializerOptions);
            }
        }
    }
}
