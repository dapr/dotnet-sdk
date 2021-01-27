## Configure API token for authentication

Dapr runtime supports token based authentication wherein it requires every incoming API request to include an authentication token in the request headers. For more information, refer [here](https://docs.dapr.io/operations/security/api-token/).

Dotnet Sdk supports this by providing a mechanism to allow the user to specify a token.

# Dapr Client
You can use the code below to specify an api token:-
```
    var daprClient = new DaprClientBuilder()
                .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient })
                .UseDaprApiToken("your_token")
                .Build();
```

# Actors
With actors, you can specify the api token using ActorProxyOptions as below:-
```
    var actorId = new ActorId("abc");
    var factory = new ActorProxyFactory();
    factory.DefaultOptions.DaprApiToken = "your_token";
    
    // Make strongly typed Actor calls with Remoting.
    var remotingProxy = factory.CreateActorProxy<IDemoActor>(actorId, "DemoActor");

    // Making calls without Remoting, this shows method invocation using InvokeMethodAsync methods, the method name and its payload is provided as arguments to InvokeMethodAsync methods.
    var nonRemotingProxy = factory.Create(actorId, "DemoActor");
```
You need to configure the token in the actor itself as below:-
```
.....

    public void ConfigureServices(IServiceCollection services)
    {
        ....
        services.AddActors(options =>
        {
            options.DaprApiToken = "your_token";
            options.Actors.RegisterActor<YourActor>();
        });
        ....
    }
```

The calls made using the proxy created with the above code will have the request headers with:-
"dapr-api-token":"your_token"

Alternatively, the user can set an environment variable "DAPR_API_TOKEN" with the token value. In case the user has specified the token using code as well as the environment variable is set, the value set via code will be used.