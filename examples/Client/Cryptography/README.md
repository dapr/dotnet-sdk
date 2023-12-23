# Dapr .NET SDK Cryptography example

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/installation)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)
- [Azure Key Vault instance](https://learn.microsoft.com/en-us/azure/key-vault/general/quick-create-portal)
- [Entra Service Principal](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)

### Service Principal/Environment Variables Setup
In your Azure portal, open Microsoft Entra ID and click `App Registrations`. Click the button at the top to create a new registration. Select a name for your service principal
and click register, noting this name for later.

Once the registration is completed, open it from the list and select Certificates & Secrets from the left navigation. Select "Client secrets" from the page body (middle column) 
and click the button to add a new client secret giving it an optional description and changing the expiry date as you desire. Click Add to create the secret. Record the secret 
value it shows you - it will not be shown to you again without creating another client secret.

Click Overview from the left navigation and record the "Application (client) ID" and the "Directory (tenant) ID" values.

On your computer (assuming Windows), open your start menu and type "Environment Variables". An option should appear named "Edit the system environment variables". Select this
and your System Properties window will open. Click the "Environment Variables" button in the bottom and said window will appear. Click the "New..." button under System variables 
to add the requisite service principal values to your environment variables. You can change these names as to want by updating the `./Components/azurekeyvault.yaml` names, but for now
configure as follows:

| Variable Name | Value |
|--|--|
| read_azure_client_id | Paste the value from your app registration overview for "Application (client) ID" |
| read_azure_client_secret | Paste the value of the client secret you generated for your app registration |
| read_azure_tenant_id | Paste the valeu from your app registration overview for "Directory (tenant) ID" |

Click OK to save your environment variables and to close your System Properties window. You may need to close restart your command line tool for it to recognize the new values.

### Azure Key Vault Setup

This example is implemented using the Azure Key Vault and will not work without it. Assuming you have a Key Vault instance configured, ensure that 
you have the `Key Vault Crypto Officer` role assigned to yourself as you'll need to in order to generate a new key in the instance. After selecting Keys
under the Objects header, click the `Generate/Import` button at the top of the instance panel.

Under options, select `Generate` and name your key. This example is pre-configured to assume a key name of 'mykey', but feel free to change this. The other default
options are fine for our purposes, so click Create at the bottom and if you've got the appropriate roles, it will show up in the list of Keys.

Update your `./Components/azurekeyvault.yaml` file with the name of your Key Vault under `vaultName` where it currently reads "changeMe". This sample assumes authentication
via a service principal, so you might also need to set this up.

Back in the Azure Portal, assign at least the `Key Vault Crypto User` role to the service principal you previously created in the last step. Do this by clicking 
`Access Control (IAM)` from the left navigation, clicking "Add" from the top and clicking "Add Role Assignment". Select `Key Vault Crypto User` from the list and click the Next
button. Ensuring that the "User, group or service principal" option is selected, click the "Select members" link and search for the name of the app registration you created. Click
Add to add this service principal to the list of members for the new role assignment and click Review + Assign twice to assign the role. This will take effect within a few seconds 
or minutes. This step ensures that while Dapr can authenticate as your service principal, that it also has permission to access and use the key in your Key Vault.

## Running the example

To run the sample locally, run this command in the DaprClient directory:

```sh
dapr run --resources-path ./Components --app-id DaprClient -- dotnet run <zero-indexed sample number>
```

Running the following command will output a list of the samples included:

```sh
dapr run --resources-path ./Components --app-id DaprClient -- dotnet run
```

Press Ctrl+C to exit, and then run the command again and provide a sample number to run the samples. 

For example, run this command to run the first sample from the list produced earlier (the 0th example):

```sh
dapr run --resources-path ./Components --app-id DaprClient -- dotnet run 0
```

## Encryption/Decryption with strings
See [EncryptDecryptExample.cs](./EncryptDecryptExample.cs) for an example of using `DaprClient` for basic string-based encryption and decryption operations as performed against UTF-8 encoded byte arrays.