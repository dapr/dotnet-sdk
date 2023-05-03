A simple Server to run.
Right now, it approves all the requests coming to it.
It provides back result in following form:

GET http://localhost:5074/RequestAuthentication HTTP/1.1
{"approved":true,"summary":"Chilly"}

`summary` will be replaced by `couponCode`.

Steps To Run:
1. Git clone this repo
2. Change directory to `AuthServer1/AuthServer1`
3. dotnet restore
4. dotnet build
5. dotnet run

When `dotnet run` is successful, you will see something like follwoing:
```
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5074
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: /home/deeagarwal/dev/daprDev/march/AuthServer1/AuthServer1/
```

By default, it will expose an endpoint at `http://localhost:5074`, on which `RequestAuthentication` can be triggered.
To stop server, simply exit the process.
To start server, simply `dotnet run` again.
