# Dapr Secret Store Configuration Provider in ASP.NET Core

## Prerequistes
* [.Net Core SDK 3.1](https://dotnet.microsoft.com/download)
* [Dapr CLI](https://github.com/dapr/cli)
* [Dapr DotNet SDK](https://github.com/dapr/dotnet-sdk)
* [Kubernetes](https://kubernetes.io)

## Overview

This document describes how to use the Dapr Secret Store Configuration Provider sample to load Secrets into ASP.NET Core Configuration.

To load secrets into configuration call the _AddDaprSecretStore_ extension method with the name of the Secret Store and a  list of secrets to retrieve:

## Using the Dapr Secret Store Configuration Provider ASP.NET Core sample

### 1. Build a docker image with the application 

Build the ASP.NET application, build the docker image and push it to a container registry:

```shell
dotnet build -c release
docker build -t <image_tag> -f ./samples/AspNetCore/SecretStoreConfigurationProviderSample/Dockerfile ./bin
docker push <image_tag>
```

### 2. Deploy the application to Kubernetes

Replace <image_tag> with your image tag in the deployment.yaml kubernetes manifest and then deply it to kubernetes:

```shell
kubectl apply -f deployment yaml
```

Note that the manifest will create the secret that the application will read.

### 3. Test the application

Get the pod name and execute a port forward to test the API

``` shell
$pod = kubectl get po --selector=app=secret-store-test -n default -o jsonpath='{.items[*].metadata.name}'
kubectl port-forward $pod 80:80
```

Run the following command in other terminal:

``` shell
curl http://localhost/secret
```

The response should read: "your super secret"