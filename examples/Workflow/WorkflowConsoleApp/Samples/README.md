# Dapr workflow Retries Sample
This sample highlights the pattern for creating durable workflows with retries. 

 These retries are currently triggered by the durable task framework - the core framework that Dapr Workflow relies on. Retries wll be packaged with the dapr runtime in a future release but the functionality can be used for now vy calling the durable task framework. Configured retries will work via the built-in dapr workflow component, and do not use the Dapr resliency feature. To use Dapr resiliency policies, they will need to be configured for Dapr workflow seperately

## Using the Sample
For this retry sample, you'll need 3 seperate terminal tabs or windows. 

### Prequisites
- Dotnet repo is cloned
- [AuthServer1 Repo is cloned](https://github.com/workflowdemos/AuthServer)
- Docker containers up and running 

### Run the App
1. From the WorkflowConsoleApp directory, Start the Dapr sidecar in the 1st terminal tab

<!-- STEP
name: Start the dapr app
-->

```bash
cd ./WorkflowConsoleApp
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```

<!-- END_STEP -->

2. In a 2nd tab, manuver to the the WorkflowCOnsoleApp directory and then start the  workflow console app. Make sure your docker containers are running!

<!-- STEP
name: Start the WF app
-->

```bash
dotnet run
```

<!-- END_STEP -->

3. In a 3rd tab, manuver to the authserver directory and follow instructions to start the server. 
4. Go back to the 2nd tab running th workflow console app. To kick-off an authentication activity, order the `CompanyCar`. All other items that are orders will not trigger the need for authentication, only the company car. 

### Troubleshooting
If there are issues with a previous workflow triggering at the start of a new workflow console app try clearing redis

<!-- STEP
name: remove old keys from redis
-->

```bash
flushdb
```

<!-- END_STEP -->



