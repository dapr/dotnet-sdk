# Dapr workflow Retries Sample
This sample highlights the pattern for creating durable workflows with retries. These retries are triggered by the built-in workflow component and does not use the Dapr resliency feature. 

## Using the Sample
For this retry sample, you'll need 3 seperate terminal tabs or windows. 

1. Clone repo
2. From the WorkflowCOnsoleApp directory, Start the Dapr sidecar in the 1st terminal tab

<!-- STEP
name: Start the dapr app
-->

```bash
cd ./WorkflowConsoleApp
dapr run --app-id wfapp --dapr-grpc-port 4001 --dapr-http-port 3500
```

<!-- END_STEP -->

3. In a 2nd tab, manuver to the the WorkflowCOnsoleApp directory and then start the  workflow console app. Make sure your docker containers are running!


<!-- STEP
name: Start the WF app
-->

```bash
dotnet run
```

<!-- END_STEP -->

4. In a 3rd tab, manuver to the authserver directory and follow instructions to start the server. 

5. Go back to the 2nd tab running th workflow console app. To kick-off an authentication activity, order the `CompanyCar`. All other items that are orders will not trigger the need for authentication, only the company car. 

### Troubleshooting
If there are issues with a previous workflow triggering at the start of a new workflow console app try clearing redis

<!-- STEP
name: remove old keys from redis
-->

```bash
flushdb
```

<!-- END_STEP -->



