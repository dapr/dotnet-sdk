---
type: docs
title: "Troubleshoot Pub/Sub with the .NET SDK"
linkTitle: "Troubleshoot pub/sub"
weight: 100000
description: Try out .NET virtual actors with this example
---

# Troubleshooting Pub/Sub

The most common problem with pub/sub is that the pub/sub endpoint in your application is not being called.

There are two layers to this problem with different solutions:

- The application is not registering pub/sub endpoints with Dapr
- The pub/sub endpoints are registered with Dapr, but the request is not reaching the desired endpoint

## Step 1: Verify endpoint registration

1. Start the application as you would normally (`dapr run ...`).

2. Use `curl` at the command line (or another HTTP testing tool) to access the `/dapr/subscribe` endpoint.

Here's an example command assuming your application's listening port is 5000:

```sh
curl http://localhost:5000/dapr/subscribe -v
```

For a correctly configured application the output should look like the following:

```txt
*   Trying ::1...
* TCP_NODELAY set
* Connected to localhost (::1) port 5000 (#0)
> GET /dapr/subscribe HTTP/1.1
> Host: localhost:5000
> User-Agent: curl/7.64.1
> Accept: */*
>
< HTTP/1.1 200 OK
< Date: Fri, 15 Jan 2021 22:31:40 GMT
< Content-Type: application/json
< Server: Kestrel
< Transfer-Encoding: chunked
<
* Connection #0 to host localhost left intact
[{"topic":"deposit","route":"deposit","pubsubName":"pubsub"},{"topic":"withdraw","route":"withdraw","pubsubName":"pubsub"}]* Closing connection 0
```

Pay particular attention to the HTTP status code, and the JSON output.

```txt
< HTTP/1.1 200 OK
```

A 200 status code indicates success.


The JSON blob that's included near the end is the output of `/dapr/subscribe` that's procesed by the Dapr runtime. In this case it's using the `ControllerSample` in this repo - so this is an example of correct output.

```json
[
    {"topic":"deposit","route":"deposit","pubsubName":"pubsub"},
    {"topic":"withdraw","route":"withdraw","pubsubName":"pubsub"}
]
```

--- 

With the output of this command in hand, you are ready to diagnose a problem or move on to the next step.

### Option 0: The response was a 200 included some pub/sub entries

**If you have entries in the JSON output from this test then the problem lies elsewhere, move on to step 2.**

### Option 1: The response was not a 200, or didn't contain JSON

If the response was not a 200 or did not contain JSON, then the `MapSubscribeHandler()` endpoint was not reached.

Make sure you have some code like the following in `Startup.cs` and repeat the test.

```cs
app.UseRouting();

app.UseCloudEvents();

app.UseEndpoints(endpoints =>
{
    endpoints.MapSubscribeHandler(); // This is the Dapr subscribe handler
    endpoints.MapControllers();
});
```

**If adding the subscribe handler did not resolve the problem, please open an issue on this repo and include the contents of your `Startup.cs` file.**

### Option 2: The response contained JSON but it was empty (like `[]`)

If the JSON output was an empty array (like `[]`) then the subcribe handler is registered, but no topic endpoints were registered.

---

If you're using a controller for pub/sub you should have a method like:

```C#
[Topic("pubsub", "deposit")]
[HttpPost("deposit")]
public async Task<ActionResult> Deposit(...)
```

In this example the `Topic` and `HttpPost` attributes are required, but other details might be different.

---

If you're using routing for pub/sub you should have an endpoint like:

```C#
endpoints.MapPost("deposit", ...).WithTopic("pubsub", "deposit");
```

In this example the call to `WithTopic(...)` is required but other details might be different.

---

**After correcting this code and re-testing if the JSON output is still the empty array (like `[]`) then please open an issue on this repository and include the contents of `Startup.cs` and your pub/sub endpoint.**

## Step 2: Verify endpoint reachability

In this step we'll verify that the entries registered with pub/sub are reachable. The last step should have left you with some JSON output like the following:

```json
[
    {"topic":"deposit","route":"deposit","pubsubName":"pubsub"},
    {"topic":"withdraw","route":"withdraw","pubsubName":"pubsub"}
]
```

Keep this output, as we'll use the `route` information to test the application.

1. Start the application as you would normally (`dapr run ...`).
   
2. Adjust the logging verbosity to include `Information` logging for ASP.NET Core as described [here](https://docs.microsoft.com/en-us/aspnet/core/mvc/controllers/routing?view=aspnetcore-5.0#debug-diagnostics). Set the `Microsoft` key to `Information`.

3. Use `curl` at the command line (or another HTTP testing tool) to access one of the routes registered a pub/sub endpoint.

Here's an example command assuming your application's listening port is 5000, and one of your pub/sub routes is `withdraw`:

```sh
curl http://localhost:5000/withdraw -H 'Content-Type: application/json' -d '{}' -v
```

Here's the output from running the above command against the sample:

```txt
*   Trying ::1...
* TCP_NODELAY set
* Connected to localhost (::1) port 5000 (#0)
> POST /withdraw HTTP/1.1
> Host: localhost:5000
> User-Agent: curl/7.64.1
> Accept: */*
> Content-Type: application/json
> Content-Length: 2
>
* upload completely sent off: 2 out of 2 bytes
< HTTP/1.1 400 Bad Request
< Date: Fri, 15 Jan 2021 22:53:27 GMT
< Content-Type: application/problem+json; charset=utf-8
< Server: Kestrel
< Transfer-Encoding: chunked
<
* Connection #0 to host localhost left intact
{"type":"https://tools.ietf.org/html/rfc7231#section-6.5.1","title":"One or more validation errors occurred.","status":400,"traceId":"|5e9d7eee-4ea66b1e144ce9bb.","errors":{"Id":["The Id field is required."]}}* Closing connection 0
```

Based on the HTTP 400 and JSON payload, this response indicates that the endpoint was reached but the request was rejected due to a validation error.

You should also look at the console output of the running application. This is example output with the Dapr logging headers stripped away for clarity.

```
info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/withdraw application/json 2
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint 'ControllerSample.Controllers.SampleController.Withdraw (ControllerSample)'
info: Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker[3]
      Route matched with {action = "Withdraw", controller = "Sample"}. Executing controller action with signature System.Threading.Tasks.Task`1[Microsoft.AspNetCore.Mvc.ActionResult`1[ControllerSample.Account]] Withdraw(ControllerSample.Transaction, Dapr.Client.DaprClient) on controller ControllerSample.Controllers.SampleController (ControllerSample).
info: Microsoft.AspNetCore.Mvc.Infrastructure.ObjectResultExecutor[1]
      Executing ObjectResult, writing value of type 'Microsoft.AspNetCore.Mvc.ValidationProblemDetails'.
info: Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker[2]
      Executed action ControllerSample.Controllers.SampleController.Withdraw (ControllerSample) in 52.1211ms
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[1]
      Executed endpoint 'ControllerSample.Controllers.SampleController.Withdraw (ControllerSample)'
info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished in 157.056ms 400 application/problem+json; charset=utf-8
```

The log entry of primary interest is the one coming from routing:

```txt
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint 'ControllerSample.Controllers.SampleController.Withdraw (ControllerSample)'
```

This entry shows that:

- Routing executed
- Routing chose the `ControllerSample.Controllers.SampleController.Withdraw (ControllerSample)'` endpoint

Now you have the information needed to troubleshoot this step.

### Option 0: Routing chose the correct endpoint

If the information in the routing log entry is correct, then it means that in isolation your application is behaving correctly.

Example:

```txt
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint 'ControllerSample.Controllers.SampleController.Withdraw (ControllerSample)'
```

You might want to try using the Dapr cli to execute send a pub/sub message directly and compare the logging output.

Example command:

```sh
dapr publish --pubsub pubsub --topic withdraw --data '{}'
```

**If after doing this you still don't understand the problem please open an issue on this repo and include the contents of your `Startup.cs`.**

### Option 1: Routing did not execute

If you don't see an entry for `Microsoft.AspNetCore.Routing.EndpointMiddleware` in the logs, then it means that the request was handled by something other than routing. Usually the problem in this case is a misbehaving middleware. Other logs from the request might give you a clue to what's happening.

**If you need help understanding the problem please open an issue on this repo and include the contents of your `Startup.cs`.**

### Option 2: Routing chose the wrong endpoint

If you see an entry for `Microsoft.AspNetCore.Routing.EndpointMiddleware` in the logs, but it contains the wrong endpoint then it means that you've got a routing conflict. The endpoint that was chosen will appear in the logs so that should give you an idea of what's causing the conflict.

**If you need help understanding the problem please open an issue on this repo and include the contents of your `Startup.cs`.**