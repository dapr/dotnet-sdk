# install Azure CLI extension for Container Apps
az config set extension.use_dynamic_install=yes_without_prompt
az extension add --name containerapp --yes

# install Node.js and NPM LTS
nvm install v18.12.1

# initialize Dapr
dapr init --runtime-version=1.10.0-rc.2