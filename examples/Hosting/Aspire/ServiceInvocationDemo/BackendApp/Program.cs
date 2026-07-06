using Common;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("api/simpleJsonGet", () => new Fruit("Banana", "Yellow"));
app.MapGet("api/simpleStringGet", () => "Hello Dapr world!");
app.MapGet("api/queryString",
    ([FromQuery(Name = "name")] string type, [FromQuery] string color) => Task.FromResult($"Fruit: '{type}', Color: '{color}'"));

app.Run();
