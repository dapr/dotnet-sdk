FROM mcr.microsoft.com/dotnet/aspnet:6.0-jammy-chiseled AS base
WORKDIR /app
EXPOSE 5010

FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy AS build
WORKDIR /
# Copy base projects
COPY ["examples/Actor/DemoActor/", "examples/Actor/DemoActor/"]
# Copy shared dependencies
COPY ["examples/Actor/IDemoActor/", "examples/Actor/IDemoActor/"]
COPY ["src/Dapr.Actors/", "src/Dapr.Actors/"]
COPY ["src/Dapr.Actors.AspNetCore/", "src/Dapr.Actors.AspNetCore/"]
COPY ["src/Dapr.Client/", "src/Dapr.Client/"]
# Restore the project and build it
WORKDIR "/examples/Actor/DemoActor"
RUN dotnet restore "DemoActor.csproj"
RUN dotnet build "DemoActor.csproj" -c Release -o /app/build

# Publish the project
FROM build AS publish
RUN dotnet publish "DemoActor.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
# Set environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# Copy published files
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DemoActor.dll"]