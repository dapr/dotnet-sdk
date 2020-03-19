FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /app
COPY ./Release/samples/SecretStoreConfigurationProviderSample /app

ENV PORT 80
EXPOSE 80

ENTRYPOINT dotnet SecretStoreConfigurationProviderSample.dll